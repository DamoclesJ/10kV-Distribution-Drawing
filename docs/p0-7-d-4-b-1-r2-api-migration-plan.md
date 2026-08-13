# P0-7-D-4-B-1-R2 API Migration Plan

## 1. 目的与当前状态

P0-7-D-4-B-1-R1 已在当前未提交工作树中收紧 RingCabinet Domain 边界：

- 新建间隔必须显式提供 `BayIndex` 和 `BayFunction`；
- 新建流程拒绝 `Unknown`、`PT` 和未定义的 `BayFunction`；
- Restore 必须接收完整的当前格式 Bay Metadata；
- Restore 不再以 `Sequence` 补 `BayIndex`，也不再自动补 `Unknown`；
- 旧的无 Bay Metadata 创建重载已删除。

因此，仍调用旧 API 的上层项目当前无法编译。本计划盘点全部受影响调用点，并冻结上层迁移时元数据的来源。它不恢复任何 Domain 兼容重载，也不把版本迁移重新放回 Domain。

迁移后的统一边界为：

Template、UI、测试夹具或已迁移 DTO 提供完整 Bay Metadata → Domain Create/Restore。

`Sequence` 只表示物理排列顺序；`BayIndex` 是独立、稳定的现场业务编号；`Function` 是明确的电气功能。除 Version 2 Persistence Migration 外，任何调用方都不得以 `Sequence` 自动补 `BayIndex`，也不得自动补 `Unknown`。

## 2. 扫描范围与结论

本次扫描以下符号在 `src/` 和 `tests/` 中的引用：

- `CreateLoadSwitch`
- `CreateIntegratedFeeder`
- `CreateNormalLoadSwitchCabinet`
- `CreatePrimarySecondaryIntegratedCabinetBase`
- `RingCabinetIntervalRestoreDefinition`

直接受影响的未迁移调用点共三组：

1. Infrastructure 的 RingCabinet Persistence 恢复映射；
2. Rendering.Wpf 的生产级 RingCabinet 创建 Factory；
3. Desktop 的混合柜演示 Factory。

R1 已删除 `CreateNormalLoadSwitchCabinet` 和 `CreatePrimarySecondaryIntegratedCabinetBase`，当前工作树中已经没有剩余调用。Domain.Tests 中的 `CreateLoadSwitch`、`CreateIntegratedFeeder` 和 Restore 构造调用已迁移为显式 Bay Metadata，不再是编译阻断点。

## 3. Infrastructure 调用点

### 3.1 `ProjectDomainDto.RestoreRingCabinet`

文件：`src/DistributionDrawing.Infrastructure/Persistence/ProjectDomainDto.cs`

性质：生产路径；负责把持久化 DTO 恢复为当前 Domain aggregate。

当前调用：

- 为每个 `ProjectRingCabinetIntervalDto` 创建 `RingCabinetIntervalRestoreDefinition`；
- 仍使用旧构造参数，只传入 `Sequence`，没有 `BayIndex` 和 `Function`；
- 因 R1 已删除旧 Restore 构造器，该调用会产生编译错误。

新 API 所需数据：

- `Sequence`：DTO 中的物理顺序；
- `BayIndex`：当前格式 DTO 的显式业务编号；
- `Function`：当前格式 DTO 中解析得到的 `BayFunction`；
- 其余 Stable ID、节点、端子、开关及结构字段保持原映射。

BayIndex 来源：

- Version 3：`ProjectRingCabinetIntervalDto.BayIndex`；
- Version 2：只能由 Persistence Migration 显式生成 `BayIndex = Sequence`，然后再进入当前格式 Mapper；
- 当前 Mapper 不得检测缺字段并自行回退到 `Sequence`。

Function 来源：

- Version 3：`ProjectRingCabinetIntervalDto.Function`，按现有枚举解析方式解析；
- Version 2：只能由 Persistence Migration 显式生成 `BayFunction.Unknown`；
- 当前 Mapper 和 Domain Restore 不得根据 DisplayName、IntervalKind、设备组合或拓扑猜测 Function。

必要的配套迁移点：

- `ProjectRingCabinetIntervalDto` 增加 `BayIndex` 和 `Function`；
- `ToDto(RingCabinetInterval)` 保存这两个 Domain 事实；
- `ProjectFileFormat` 升级到 Version 3；
- `ProjectFileContainer` 或独立 migration handler 在 DTO 进入当前 Mapper 前完成 Version 2 → Version 3 转换；
- Persistence 测试覆盖 Version 2 迁移和 Version 3 round trip。

这些配套点不属于本次 R2 文档任务的代码修改，但它们是 Infrastructure 调用迁移能够成立的前置条件。

### 3.2 Infrastructure 迁移边界

正确恢复链路应固定为：

Version 2 文件 → Persistence Migration（`BayIndex = Sequence`、`Function = Unknown`）→ 完整 Version 3 DTO → 当前 Mapper → 完整 `RingCabinetIntervalRestoreDefinition` → Domain Restore。

Version 3 文件应直接提供完整字段。缺少 Bay Metadata 的 Version 3 数据必须作为无效当前格式数据拒绝，不能再次套用 Version 2 兼容规则。

## 4. Rendering.Wpf 调用点

### 4.1 `RingCabinetCreationFactory.CreateIntervalDefinition`

文件：`src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/RingCabinetCreationFactory.cs`

性质：生产路径；Desktop 创建闭环中把不可变创建配置转换为 Domain Definition。

当前调用：

- LoadSwitch 分支调用旧 `CreateLoadSwitch(initialLoadSwitchState, initialGroundSwitchState, displayName)`；
- IntegratedFeeder 分支调用旧 `CreateIntegratedFeeder(groundingStructureKind, initial states, displayName)`；
- 两个分支都没有传 `BayIndex` 和 `Function`。

新 API 所需数据：

- 每个 interval configuration 的显式 `BayIndex`；
- 每个 interval configuration 的显式、非 `Unknown`、非 `PT` `BayFunction`；
- 当前已有的 IntervalKind、GroundingStructureKind、DisplayName 和技术初始化 SwitchState。

BayIndex 来源：

- 来自不可变的 `RingCabinetIntervalCreationConfiguration`；
- 创建 UI 可以在新增行时给出 `1..N` 的初始建议值，但确认配置时必须把它作为独立字段固化；
- 行重排只改变 Sequence，不得静默重写已配置的 BayIndex；
- UI 必须在提交前校验正整数和柜内唯一，最终仍由 Domain 校验兜底。

Function 来源：

- 来自不可变的 `RingCabinetIntervalCreationConfiguration`；
- 用户必须在当前支持的非 PT 功能中作明确选择；
- 不得根据 IntervalKind、DisplayName、行位置或 GroundingStructureKind 推断；
- PT 选项在当前 Domain 尚无 PT interval 实现时不得进入生产创建配置。

### 4.2 `RingCabinetCreationConfiguration`

文件：`src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/RingCabinetCreationConfiguration.cs`

性质：生产路径的输入合同；虽然不直接调用旧 Domain API，但当前合同无法向 Factory 提供新 API 的必需参数。

迁移要求：

- `RingCabinetIntervalCreationConfiguration` 增加 `BayIndex` 和 `Function`；
- 保持配置不可变，避免 Dialog 后续状态影响 pending placement；
- 不在配置模型中加入 Domain ID、Layout ID、SwitchState、TemplateReference 或 WPF 类型；
- Sequence 继续由 `Intervals` 集合顺序表达，不需要再复制为可变字段。

### 4.3 无需承担元数据来源的 Rendering 组件

以下组件不是旧 API 调用点，不应被用来补 Bay Metadata：

- `RingCabinetLayoutFactory`：只检查已创建 Domain aggregate 并生成几何；
- `DeviceCommandFactory`：协调 CreationFactory、LayoutFactory 和 Add Command，不推断 Function 或 BayIndex；
- Rendering Symbols：只读取 Domain/Layout，不创建或修复 Domain 事实。

## 5. Desktop 调用点

### 5.1 `RingCabinetCompositionDemoFactory`

文件：`src/DistributionDrawing.Desktop/Demo/RingCabinetCompositionDemoFactory.cs`

性质：演示/Composition Root 辅助路径，不是用户创建工作流，但属于 Desktop 可编译代码。

当前调用：

- 两次调用旧 `CreateLoadSwitch`；
- 两次调用旧 `CreateIntegratedFeeder`；
- 仅通过 DisplayName 暗示进线、出线、联络语义。

新 API 所需数据：

- 为四个示例 interval 显式提供 BayIndex；
- 为每个 interval 显式提供合法 Function；
- 保留原有 SwitchState、GroundingStructureKind 和 DisplayName。

BayIndex 来源：

- 演示场景中明确声明的固定示例数据；
- 不通过数组位置在 Domain Factory 内自动补值；
- 示例可以选用 `1..4`，但必须以具名参数或清晰局部数据显式表达，并注明这只是演示实例，不是通用规则。

Function 来源：

- 演示场景中明确声明的固定示例数据；
- 需要由业务含义审查确认后写入，而不是运行时从中文 DisplayName 推断；
- 当前可表达 Incoming、Outgoing、Tie 等合法非 PT Function。

### 5.2 `RingCabinetCreationViewModel`

文件：`src/DistributionDrawing.Desktop/RingCabinetCreation/RingCabinetCreationViewModel.cs`

性质：生产路径上游；不直接调用旧 Domain API，但负责构造 `RingCabinetIntervalCreationConfiguration`。

迁移要求：

- row ViewModel 增加独立 BayIndex 和 Function 输入状态；
- BayIndex 校验正整数且柜内唯一；
- Function 校验为已定义、非 Unknown、非 PT 的当前可创建值；
- `TryCreateConfiguration` 将两个字段复制进不可变配置；
- `UpdateSequences` 仅更新物理顺序显示，不修改 BayIndex；
- 不从 IntervalKind 自动选择 Incoming/Outgoing/Tie。

BayIndex 来源：

- 用户确认的行数据；
- 新增行可以预填下一个可用正整数作为 UI 便利，但用户可覆盖；
- 预填动作属于创建 UI，不成为 Domain 默认规则。

Function 来源：

- 用户在创建 Dialog 中显式选择；
- 第一版只暴露当前 Domain 能创建的功能，不暴露 PT。

### 5.3 `RingCabinetCreationDialog.xaml`

文件：`src/DistributionDrawing.Desktop/RingCabinetCreation/RingCabinetCreationDialog.xaml`

性质：生产 UI；不是直接 API 调用点，但需要让用户输入新 API 所需事实。

迁移要求：

- 每行增加 BayIndex 输入；
- 每行增加 Function 选择；
- 保持 Sequence 为只读排列位置；
- 不增加自动专业命名、Function 推断或 PT 创建能力。

### 5.4 不需要迁移 Domain 调用的 Desktop 组件

MainWindow、PlacementController 和 DrawingToolCoordinator 继续传递已经确认的 immutable configuration；不应在这些层补 BayIndex、推断 Function 或直接调用 Domain Factory。

## 6. Tests 调用点

### 6.1 已迁移的 Domain.Tests

以下测试代码当前已经使用 R1 新 API，不是剩余编译阻断点：

- `tests/DistributionDrawing.Domain.Tests/TestFixtures.cs`
- `tests/DistributionDrawing.Domain.Tests/IntegratedFeederIntervalEvaluationTests.cs`
- `tests/DistributionDrawing.Domain.Tests/RingCabinetBayMetadataTests.cs`
- 通过 `TestFixtures.CreateLoadSwitchRingCabinet` 间接创建柜体的 `PoleAttachmentTests.cs` 和 `TopologyBoundaryTests.cs`

当前方式：

- helper 和测试显式传入 BayIndex；
- helper 和测试显式传入合法 Function；
- Restore 测试显式构造包含 Sequence、BayIndex 和 Function 的完整 definition。

BayIndex 来源：测试场景显式数据或测试生成范围。Function 来源：测试场景显式数据。它们不得作为生产默认策略复用。

### 6.2 已删除旧 API 的扫描结论

当前 `src/` 和 `tests/` 中没有 `CreateNormalLoadSwitchCabinet` 或 `CreatePrimarySecondaryIntegratedCabinetBase` 调用。无需为它们恢复兼容方法；未来若发现遗漏，应迁移调用者，不应恢复旧入口。

### 6.3 后续测试迁移与新增

上层迁移需要补充或调整：

- Infrastructure：Version 3 Bay Metadata save/load round trip；
- Infrastructure：Version 2 文件只在 Migration 层获得 `Index = Sequence` 和 `Function = Unknown`；
- Infrastructure：Version 3 缺字段、非法 Function、重复或非正 BayIndex 被拒绝；
- Rendering.Wpf/可测试 Factory：配置中的 BayIndex/Function 原样进入 Domain；
- Desktop ViewModel：非连续 BayIndex、重复 BayIndex、非法 Function、重排行为；
- Desktop Demo：只需编译和静态结构验证，不把演示值测试成业务默认。

## 7. 调用点汇总

| 分类 | 文件 | 路径性质 | 当前问题 | BayIndex 来源 | Function 来源 |
| --- | --- | --- | --- | --- | --- |
| Infrastructure | `ProjectDomainDto.cs` | 生产 Save/Restore | Restore 使用旧构造器，DTO 不含新字段 | V3 DTO；V2 仅由 Migration 从 Sequence 映射 | V3 DTO；V2 仅由 Migration 设 Unknown |
| Rendering.Wpf | `RingCabinetCreationFactory.cs` | 生产创建 | 两类 interval 调用旧 Factory 重载 | immutable creation configuration | immutable creation configuration |
| Rendering.Wpf | `RingCabinetCreationConfiguration.cs` | 生产输入合同 | 无法承载新 API 必需字段 | Desktop 确认后的显式值 | Desktop 确认后的显式值 |
| Desktop | `RingCabinetCreationViewModel.cs` | 生产创建 UI | 行模型与配置构造缺少字段 | 用户输入；可有 UI 预填但不随排序改写 | 用户显式选择 |
| Desktop | `RingCabinetCreationDialog.xaml` | 生产创建 UI | 无对应输入控件 | 用户输入 | 用户选择 |
| Desktop | `RingCabinetCompositionDemoFactory.cs` | 演示辅助 | 四处调用旧重载 | 演示场景显式常量 | 演示场景显式常量 |
| Tests | Domain.Tests 相关文件 | 测试辅助 | R1 已迁移，无剩余旧调用 | 测试显式数据 | 测试显式数据 |

## 8. 推荐迁移顺序

### 8.1 第一阶段：保持 Domain R1 边界

1. 保留已删除旧重载的状态；
2. 不向 Domain Restore 加回缺字段补值；
3. 不允许新建流程使用 Unknown 或 PT；
4. 先以当前编译错误清单作为上层迁移检查表。

### 8.2 第二阶段：完成 Persistence Version 3

1. DTO 增加 BayIndex 和 Function；
2. Save Mapper 写出完整字段；
3. Current Restore Mapper 读取完整字段；
4. FormatVersion 升级到 3；
5. 在 Persistence 层实现 Version 2 → Version 3 Migration；
6. 补 round trip、migration 和非法输入测试。

Persistence 必须先建立明确版本边界，否则 Infrastructure 为了恢复编译而直接在 Mapper 中补值，会重新制造 R1 已删除的边界错误。

### 8.3 第三阶段：迁移生产创建配置

1. 扩展 `RingCabinetIntervalCreationConfiguration`；
2. 修改 `RingCabinetCreationFactory`，把完整元数据传给新 Domain API；
3. 扩展 Desktop row ViewModel 和 Dialog；
4. 保持 Sequence 与 BayIndex 独立；
5. 保持 Placement、Command 和 Layout Factory 的职责不变。

### 8.4 第四阶段：迁移演示与测试

1. 为 Demo Factory 写入显式示例 BayIndex/Function；
2. 复查 Domain.Tests 没有旧入口；
3. 增加上层配置和 Persistence 测试；
4. 全仓库再次搜索旧符号；
5. 执行 solution build 和可用测试。

## 9. 编译与提交策略

R1 已删除公共创建重载和旧 Restore 构造器，而 Infrastructure、Rendering.Wpf、Desktop 仍有旧调用。因此当前工作树不是可独立通过全 solution build 的提交状态。

推荐策略：

- 不提交一个已知会破坏上层编译的 Domain-only commit 到主线；
- 先完成调用迁移，至少保证所有项目编译；
- Persistence Version 3 与迁移应保持为边界清晰、可测试的切片；
- 如果必须拆分提交，应在同一集成分支连续完成，并在合入 main 前验证最终组合，而不是恢复旧 Domain API 作为过渡。

## 10. 风险与防护

### 10.1 Function 被错误推断

风险：为快速迁移，从 IntervalKind、DisplayName 或位置推断 Incoming/Outgoing/Tie。

防护：生产新建由用户或未来 Template 显式提供；旧数据只迁移为 Unknown，不猜测。

### 10.2 Sequence 与 BayIndex 再次耦合

风险：Desktop 行重排时同步重写 BayIndex，或 Factory 继续以数组位置生成业务编号。

防护：Sequence 由集合顺序表达；BayIndex 是独立输入。仅新行初始预填可以建议 `1..N`，确认后按显式值进入 Domain。

### 10.3 Version 3 缺字段被静默接受

风险：JSON 默认值使缺失 BayIndex 变成 0、Function 变成 Unknown，然后错误进入 Domain。

防护：Migration 只处理明确的 Version 2；Version 3 Mapper/Domain 对缺失或非法字段严格失败。

### 10.4 演示数据变成隐式业务规则

风险：Demo 中 `1..4` 或某组 Function 被复制为生产默认。

防护：Demo 使用显式场景数据并保持在 Demo 层；生产配置由用户或未来 Template 提供。

### 10.5 PT 伪建模

风险：为支持 `BayFunction.PT`，使用现有 LoadSwitch 或 IntegratedFeeder interval 冒充 PT Bay。

防护：当前 Create/Restore 均拒绝 PT；上层 UI 不提供 PT；未来必须先完成 PT Domain、Layout、Rendering 和 Persistence 能力。

## 11. 完成判定

R2 API 迁移完成需同时满足：

- 全仓库不存在旧创建重载或旧 Restore 构造器调用；
- 所有生产新建路径显式提供正数且柜内唯一的 BayIndex；
- 所有生产新建路径显式提供合法、非 Unknown、非 PT Function；
- Version 2 兼容只存在于 Persistence Migration；
- Current Mapper 和 Domain Restore 不补缺失元数据；
- Sequence 与 BayIndex 在 UI、配置、Domain 和 DTO 中保持独立；
- Domain、Infrastructure、Rendering.Wpf、Desktop 均可编译；
- Domain 与 Persistence 相关测试通过；
- Stable ID、Command、Layout、Selection 和 Rendering 行为未因元数据迁移改变。

## 12. 结论

当前 R1 Domain 边界是正确的，不应通过恢复旧 API 解除编译阻断。R2 应迁移 Infrastructure、Rendering.Wpf 和 Desktop 的调用者，使每条生产链路在进入 Domain 前都拥有完整 Bay Metadata。

唯一允许的兼容补值是 Version 2 Persistence Migration 中的 `BayIndex = Sequence` 和 `Function = Unknown`。生产创建、当前格式 Restore、Demo 和测试均必须显式提供 BayIndex 与 Function。
