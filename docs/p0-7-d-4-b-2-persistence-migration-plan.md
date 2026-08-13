# P0-7-D-4-B-2-A Persistence Migration Implementation Plan

## 1. 目标与边界

P0-7-D-4-B-1 已把 `Sequence`、`BayIndex` 和 `BayFunction` 冻结为 RingCabinetInterval 的 Domain 事实，并要求创建与 Restore 输入完整携带 Bay Metadata。当前 Persistence 仍使用 FormatVersion 2，无法保存 BayIndex/Function，Restore Mapper 也仍调用旧构造方式。

P0-7-D-4-B-2 的目标是：

- 将当前工程格式升级到 Version 3；
- 无损保存和恢复 BayIndex、Function；
- 保留 Version 1、Version 2 工程的读取能力；
- 只在 Persistence Migration 中执行历史数据补值；
- 保持全部 Stable ID、Layout、Professional 和拓扑事实不变。

本计划不修改 Domain 规则，不恢复旧 Domain API，不修改 Command、Selection、Rendering 或 Template Builder。

## 2. 当前 Persistence 结构

### 2.1 DTO 与 Mapper

实际文件：`src/DistributionDrawing.Infrastructure/Persistence/ProjectDomainDto.cs`。

当前相关类型和方法：

- `ProjectDomainDto`：工程 Domain 区根 DTO；
- `ProjectRingCabinetDto`：保存 CabinetId、DisplayName、MainBusNodeId、Intervals、柜内节点和端子；
- `ProjectRingCabinetIntervalDto`：保存 IntervalId、ParentCabinetId、Sequence、DisplayName、IntervalKind、GroundingStructureKind、节点/端子/Assembly Stable ID 和 Switch DTO；
- `ProjectDomainMapper.ToDto(DrawingDocument)`：Domain → DTO 总入口；
- `ProjectDomainMapper.ToDto(RingCabinetInterval)`：RingCabinetInterval 保存映射；
- `ProjectDomainMapper.ToDomain(ProjectDomainDto)`：DTO → Domain 总入口；
- `ProjectDomainMapper.RestoreRingCabinet(ProjectRingCabinetDto)`：构造 `RingCabinetIntervalRestoreDefinition` 并调用 Domain Restore。

当前缺口：

- `ProjectRingCabinetIntervalDto` 没有 BayIndex 和 Function；
- 保存映射会丢失这两个长期事实；
- Restore 映射没有数据可传给当前 Domain Restore definition；
- Infrastructure 因旧 Restore 构造调用而存在确定的编译阻断。

### 2.2 文件格式与容器

实际文件：

- `src/DistributionDrawing.Infrastructure/Persistence/ProjectFileFormat.cs`；
- `src/DistributionDrawing.Infrastructure/Persistence/ProjectFileContainer.cs`；
- `src/DistributionDrawing.Infrastructure/Persistence/ProjectFileManifest.cs`；
- `src/DistributionDrawing.Infrastructure/Persistence/ProjectFileDocument.cs`；
- `src/DistributionDrawing.Infrastructure/Persistence/ProjectService.cs`。

当前版本声明：

- `PreviousVersion = 1`；
- `CurrentVersion = 2`。

当前 `ProjectFileContainer.Open`：

1. 读取并校验 Manifest；
2. 直接把 `document.json` 反序列化为当前 `ProjectFilePayload`；
3. Version 1 缺少 Professional 时创建空 Professional DTO；
4. 把有效 Manifest 提升到 Version 2；
5. 返回 `ProjectFileDocument`。

当前没有独立 Migration Router。单一 `PreviousVersion` 只能表达一代历史版本，升级 Version 3 后不能继续使用该模型，否则会丢失 Version 1 的读取能力。

### 2.3 测试结构

仓库当前只有 `tests/DistributionDrawing.Domain.Tests`，其项目只引用 Domain。没有 Infrastructure/Persistence 自动化测试项目。

Version 3、迁移和 ZIP round trip 都属于 Infrastructure 行为，不应把 Infrastructure 引用倒灌进 Domain.Tests。建议新增独立的 Infrastructure.Tests 项目。

## 3. DTO 修改设计

### 3.1 `ProjectRingCabinetIntervalDto`

在现有 `Sequence` 后增加：

- `int BayIndex`；
- `string Function`。

字段职责：

- `Sequence`：物理排列顺序；
- `BayIndex`：稳定的现场业务编号；
- `Function`：`BayFunction` 的规范字符串表示。

Function 应沿用当前 Mapper 的枚举字符串编码方式，不把枚举数值写入 JSON，以便文件可读并避免枚举顺序成为持久化合同。

### 3.2 默认值策略

Version 3 当前 DTO 不定义业务默认值：

- BayIndex 缺失不能默认为 Sequence；
- Function 缺失不能默认为 Unknown；
- 未识别的 Function 字符串不能容错成 Unknown；
- BayIndex 为 0 或负数必须失败。

旧文件补值只由显式 migration step 完成。Version 3 数据反序列化后，Mapper 必须在进入 Domain Restore 前检查 BayIndex 和 Function；语言默认值 `0`/`null` 只能触发损坏文件错误，不能触发兼容分支。

### 3.3 保存 Mapper

修改 `ProjectDomainMapper.ToDto(RingCabinetInterval)`：

- `Sequence = interval.Sequence`；
- `BayIndex = interval.BayIndex`；
- `Function = Encode(interval.Function)`；
- 其余 Stable ID 和结构字段保持当前顺序与来源。

不修改 Layout DTO。BayIndex/Function 是 Domain 事实，不是布局几何。

### 3.4 Restore Mapper

修改 `ProjectDomainMapper.RestoreRingCabinet`：

- 将 DTO Sequence、BayIndex、解析后的 Function 一并传入 `RingCabinetIntervalRestoreDefinition`；
- Function 使用现有严格枚举解析方式；
- Unknown 字符串可恢复，因为它是迁移后的明确兼容值；
- 未定义字符串、缺失字段、非法 BayIndex 应抛出明确 `InvalidDataException` 或由 Domain 聚合校验拒绝；
- Mapper 不读取 Manifest 版本，也不执行旧格式补值。

## 4. FormatVersion 3 设计

### 4.1 版本声明

将 `ProjectFileFormat.CurrentVersion` 从 2 改为 3。

不再用一个 `PreviousVersion` 表达所有历史版本。建议明确声明或判断受支持版本集合：

- Version 1：可读取，需顺序迁移到 3；
- Version 2：可读取，需迁移到 3；
- Version 3：当前严格格式；
- 小于 1 或大于 3：拒绝。

具体 API 可以是版本常量与 `IsSupportedVersion(int)`，不需要建立通用插件式 migration framework。

### 4.2 顺序迁移链

迁移必须按版本逐步执行：

1. Version 1 → Version 2：沿用现有补充空 Professional section 的语义；
2. Version 2 → Version 3：补充 RingCabinet interval BayIndex/Function；
3. Version 1 文件依次执行上述两步；
4. Version 3 不执行任何历史补值。

完成迁移后的内存 `ProjectFileDocument.Manifest.FormatVersion` 为 3。打开旧文件不能立即覆盖原文件；只有用户正常保存时才写出 Version 3。

## 5. Migration 实现边界

### 5.1 推荐入口

Migration 应接在 `ProjectFileContainer.Open` 的文件读取边界：

1. 读取并校验原始 Manifest 和 archive；
2. 读取 `document.json` 原始 JSON；
3. 根据原始 Manifest 版本执行顺序迁移；
4. 将迁移后的 payload 反序列化为当前 Version 3 DTO；
5. 执行现有 ProjectId、Metadata、Domain、Layout、Professional 校验；
6. 返回当前 Version 3 `ProjectFileDocument`；
7. `ProjectService` 再把当前 DTO 恢复为 Domain。

推荐在容器旁新增最小的 `ProjectFormatMigration`，只负责内存 payload 的版本转换。`ProjectFileContainer` 继续负责 ZIP、安全校验、读写和迁移编排，不建立第二套保存/加载入口。

### 5.2 为什么在反序列化当前 DTO 前迁移

Version 2 JSON 没有 BayIndex/Function。如果直接反序列化为 Version 3 positional record，缺失字段可能先成为 `0`/`null`，从而混淆“旧格式缺字段”和“当前格式损坏”。

推荐 migration 处理原始 `JsonNode`/`JsonObject`：

- 版本由 Manifest 决定；
- Version 1/2 在 JSON 层补充明确字段；
- Version 3 JSON 不修改；
- 完成后统一反序列化为当前 DTO。

这比复制整套 legacy Domain DTO 更小，也能保持旧 payload 中所有不相关字段原样。Migration 只能增加已冻结的兼容字段，不能重排、删改或重新生成对象。

### 5.3 Version 2 → 3 精确规则

对 `domain.ringCabinets[].intervals[]` 中每个旧 interval：

- `bayIndex = sequence`；
- `function = "Unknown"`。

只在该 migration step 执行。

必须保持：

- ProjectId、CabinetId、IntervalId；
- SwitchId、SwitchAssemblyId；
- TerminalId、ElectricalNodeId；
- ConnectionId 和 OverheadLine 数据；
- Sequence、DisplayName、IntervalKind、GroundingStructureKind；
- Layout 和 Professional 数据。

禁止：

- 从 DisplayName 提取数字或功能；
- 从 IntervalKind、Switch 组合或拓扑猜 Function；
- 从 Layout 坐标推断编号；
- 创建新的 Stable ID；
- 在 Domain Restore 中重复补值。

### 5.4 Version 3 严格性

Version 3 必须拒绝：

- 缺少 bayIndex；
- bayIndex 非正或柜内重复；
- 缺少 function；
- function 为空或为未知字符串；
- function 为 PT，而当前 PT Domain 尚不存在。

Version 3 显式 `Unknown` 可以恢复，用于旧工程迁移后保存的兼容状态；新建 Domain 仍拒绝 Unknown。这一区分继续由 Create 和 Restore 两条 Domain 边界保证。

## 6. Save/Load 流程

### 6.1 保存

保存链路：

DrawingDocument → `ProjectDomainMapper.ToDto` → Version 3 `ProjectDomainDto` → `ProjectFileContainer.Save` → Version 3 manifest + `document.json`。

要求：

- 所有 interval 写出 Sequence、BayIndex、Function；
- Manifest 无论来源版本如何，保存时写 `CurrentVersion = 3`；
- 不修改 Layout、Professional 或其他 DTO 合同；
- Save 后现有 reopen-and-validate 流程必须成功；
- 保存不重新生成 Stable ID。

### 6.2 加载 Version 3

Version 3 文件 → 当前 payload DTO → `ProjectDomainMapper` → 完整 RestoreDefinition → `RingCabinet.Restore` → DrawingDocument。

该路径不执行默认补值。

### 6.3 加载 Version 2

Version 2 文件 → Persistence Migration → Version 3 payload DTO → `ProjectDomainMapper` → 完整 RestoreDefinition → `RingCabinet.Restore`。

迁移后的每个旧 interval 明确得到：

- BayIndex 等于原 Sequence；
- Function 等于 Unknown。

### 6.4 加载 Version 1

Version 1 文件 → Version 1→2 Professional 补充 → Version 2→3 Bay Metadata 补充 → 当前 DTO → Domain。

不得因升级到 Version 3 丢失既有 Version 1 兼容能力。

### 6.5 Dirty 与原文件

迁移是加载兼容，不进入 CommandStack，也不是用户编辑命令。`ProjectService.LoadProject` 仍建立 clean session。

Open 只迁移内存表示，不覆盖旧文件。用户下一次正常 Save 时，才把工程写成 Version 3。保存后的 session 继续遵循现有 clean/save-point 语义。

## 7. 测试计划

### 7.1 测试项目

建议新增：

- `tests/DistributionDrawing.Infrastructure.Tests/DistributionDrawing.Infrastructure.Tests.csproj`；
- 将其加入 `src/DistributionDrawing.sln`。

项目引用 Infrastructure，并复用当前 xUnit 版本。不要让 Domain.Tests 反向引用 Infrastructure。

### 7.2 Version 3 Round Trip

创建包含非连续 BayIndex 和多个合法 Function 的 mixed RingCabinet，执行：

Domain → DTO → 文件保存 → 文件打开 → Domain Restore。

断言：

- Sequence、BayIndex、Function 完全一致；
- mixed interval 顺序与类型一致；
- Manifest 为 Version 3；
- Layout、Professional 与现有事实保持；
- 文件 JSON 明确包含 bayIndex/function。

### 7.3 Version 2 Migration

构造真实 Version 2 archive，interval 不含 BayIndex/Function，打开后断言：

- Manifest 的内存有效版本为 3；
- BayIndex 等于各 interval 原 Sequence；
- Function 为 Unknown；
- 原文件未被 Open 改写；
- 后续 Save 写出 Version 3 完整字段。

测试使用显式 Version 2 fixture，不通过当前 Version 3 Save 后删除字段来掩盖 manifest/contract 差异。

### 7.4 Version 1 顺序迁移

构造 Version 1 archive，验证：

- Professional 仍按既有规则补为空 section；
- 随后补 BayIndex/Unknown；
- 最终能够恢复 Domain；
- Version 1 支持没有因 `CurrentVersion = 3` 丢失。

### 7.5 Stable ID

Version 3 round trip 与 Version 2 migration 均断言保持：

- CabinetId；
- IntervalId；
- MainBusNodeId、内部 ElectricalNodeId；
- TerminalId；
- SwitchId；
- SwitchAssemblyId；
- Connection、Layout 引用 ID。

### 7.6 不推断 Function

准备名称包含“进线”“出线”“联络”等文本、且设备结构不同的 Version 2 intervals。迁移后统一断言 Function 为 Unknown，证明没有依据 DisplayName、IntervalKind、Switch 或拓扑推断。

### 7.7 Version 3 损坏输入

至少覆盖：

- 缺少 bayIndex；
- bayIndex 为 0；
- 重复 bayIndex；
- 缺少 function；
- function 为未知字符串；
- function 为 PT。

所有情况必须在候选工程完整建立前失败，不替换 `ProjectService.Current`，不留下部分 Domain 状态。

### 7.8 Save/Load 与 Dirty

验证旧工程加载后 session 为 clean；修改并保存后仍按现有保存点规则变为 clean。Migration 本身不创建 Command，也不标记 Dirty。

## 8. 实际修改文件清单

### 8.1 预计新增文件

| 文件 | 目的 | 必要性 |
| --- | --- | --- |
| `src/DistributionDrawing.Infrastructure/Persistence/ProjectFormatMigration.cs` | 按版本顺序迁移原始 JSON payload | 必须；避免继续膨胀容器和把迁移放入 Domain Mapper |
| `tests/DistributionDrawing.Infrastructure.Tests/DistributionDrawing.Infrastructure.Tests.csproj` | 建立 Persistence 测试边界 | 必须 |
| `tests/DistributionDrawing.Infrastructure.Tests/ProjectFormatMigrationTests.cs` | Version 1/2 → 3、严格输入与不推断测试 | 必须 |
| `tests/DistributionDrawing.Infrastructure.Tests/ProjectPersistenceRoundTripTests.cs` | Version 3 round trip、Stable ID、Save/Load 测试 | 必须 |

如 fixture 构造明显重复，可在 Infrastructure.Tests 内增加一个最小 fixture helper；不要建立生产迁移数据生成器。

### 8.2 预计修改文件

| 文件 | 修改目的 | 风险 |
| --- | --- | --- |
| `src/DistributionDrawing.Infrastructure/Persistence/ProjectDomainDto.cs` | DTO 增加 BayIndex/Function；保存与 Restore Mapper 适配 | 字段顺序、严格解析和 Stable ID 映射错误 |
| `src/DistributionDrawing.Infrastructure/Persistence/ProjectFileFormat.cs` | CurrentVersion 升至 3并显式支持 1/2/3 | 误删 Version 1 支持 |
| `src/DistributionDrawing.Infrastructure/Persistence/ProjectFileContainer.cs` | 读取原始 payload、编排 migration、返回当前文档 | 迁移顺序、当前格式严格性、ZIP 原子边界 |
| `src/DistributionDrawing.sln` | 加入 Infrastructure.Tests | 仅工程注册风险 |

### 8.3 预计无需修改

- `ProjectFileManifest.cs`：已经通过 `ProjectFileFormat.CurrentVersion` 创建 manifest；
- `ProjectFileDocument.cs`：当前结构足以承载迁移后的 DTO；
- `ProjectService.cs`：已有候选加载、保存后重开校验和 clean session 流程，原则上无需改变；
- `ProjectLayoutDto.cs`、`ProjectProfessionalDto.cs`：没有新增对应事实；
- Domain、Rendering、Desktop、Command、Selection：本阶段不修改。

如果实现发现 `ProjectService` 必须变化，应先证明现有 candidate/validation 流程无法承载迁移；不得顺手重构。

## 9. 推荐实施顺序

1. 新增 Infrastructure.Tests 项目和最小 Version 2/3 archive fixtures；
2. 先编写 Version 3 round trip、Version 2 migration、Version 1 chain 和损坏输入测试；
3. 修改 `ProjectRingCabinetIntervalDto` 及保存/Restore Mapper，解除 Infrastructure 旧构造编译阻断；
4. 将 CurrentVersion 升级为 3，并把受支持版本从单一 PreviousVersion 改成明确的 1/2/3 判断；
5. 新增 `ProjectFormatMigration`，实现 Version 1→2 和 Version 2→3 顺序迁移；
6. 将迁移接入 `ProjectFileContainer.Open`，保证 Version 3 不走历史补值；
7. 执行 Infrastructure.Tests、Domain.Tests 和全 solution build；
8. 搜索确认 Domain Restore、Mapper 或 Desktop 中不存在历史补值；
9. 检查 `git diff --check` 和实际提交范围。

## 10. 验收标准

P0-7-D-4-B-2 完成必须满足：

- FormatVersion 3 保存完整 BayIndex/Function；
- Version 3 round trip 不丢失任何 Domain 或 Stable ID；
- Version 2 只在 Persistence Migration 中得到 `BayIndex = Sequence`、`Function = Unknown`；
- Version 1 仍可经顺序迁移打开；
- 不根据名称、设备结构、拓扑或布局猜 Function；
- Version 3 缺失或非法字段严格失败；
- Domain Restore 不包含版本兼容逻辑；
- Open 不覆盖旧文件，Migration 不产生 Command 或 Dirty；
- Infrastructure、Domain、Rendering.Wpf、Desktop 全部编译；
- Domain.Tests 与新增 Infrastructure.Tests 通过。

## 11. 结论

推荐采用 FormatVersion 3 + 文件读取边界顺序迁移。当前 DTO 只表达 Version 3 事实；Version 1/2 的缺失字段在反序列化为当前 DTO 前，由最小 `ProjectFormatMigration` 明确补齐。

该方案保持职责清晰：Persistence 负责版本兼容，Mapper 负责当前 DTO 与 Domain 转换，Domain Restore 只恢复完整当前事实。完成 B-2 后，当前 B-1/R1/R2 才具备完整编译与 Save/Reload 提交条件。
