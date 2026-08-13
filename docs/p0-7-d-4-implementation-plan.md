# P0-7-D-4 Domain/Persistence 最小补充实施计划

## 1. 计划目标与边界

本文把 P0-7-D-3 已冻结的 Domain/Persistence 设计转化为可执行的生产代码实施顺序。

本阶段计划的最终结果是：

- `RingCabinetInterval` 同时表达物理 `Sequence`、现场 `BayIndex` 和 `BayFunction`；
- 新建与恢复路径都经过 Domain 不变量校验；
- Persistence 可以保存和恢复两个新增长期事实；
- Version 2 工程可以确定性迁移到 Version 3；
- Stable ID、RuntimeLayout、CommandStack、Selection 和现有 Rendering 行为保持兼容；
- Inspector 第一阶段只读显示 BayIndex 与 BayFunction。

本文只描述实施计划，不修改生产代码、DTO、FormatVersion、Migration、Command、Selection、Rendering 或 UI。

## 2. 当前实际代码结构

### 2.1 Domain

当前相关文件位于：

- `src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinet.cs`
- `src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinetInterval.cs`
- `src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinetDefinition.cs`
- `src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinetIntervalDefinition.cs`
- `src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinetRestoreDefinition.cs`
- `src/DistributionDrawing.Domain/Devices/RingCabinets/IntervalKind.cs`
- `src/DistributionDrawing.Domain/Devices/RingCabinets/GroundingStructureKind.cs`

当前事实：

- `RingCabinetInterval.Sequence` 已存在，且从 Definition 集合顺序生成；
- `RingCabinet.Restore` 使用 RestoreDefinition 恢复 Stable ID；
- `RingCabinet.ValidateStructure` 校验 ParentCabinetId、Sequence、Node、Terminal、Switch 和 Assembly 关系；
- 当前没有 BayIndex；
- 当前没有 BayFunction；
- 当前 IntervalKind 表达设备结构，不可替代 BayFunction。

### 2.2 Persistence

当前相关文件位于：

- `src/DistributionDrawing.Infrastructure/Persistence/ProjectDomainDto.cs`
- `src/DistributionDrawing.Infrastructure/Persistence/ProjectFileFormat.cs`
- `src/DistributionDrawing.Infrastructure/Persistence/ProjectFileContainer.cs`
- `src/DistributionDrawing.Infrastructure/Persistence/ProjectFileDocument.cs`
- `src/DistributionDrawing.Infrastructure/Persistence/ProjectFileManifest.cs`
- `src/DistributionDrawing.Infrastructure/Persistence/ProjectLayoutDto.cs`
- `src/DistributionDrawing.Infrastructure/Persistence/ProjectService.cs`

当前 `ProjectRingCabinetIntervalDto` 已保存 Sequence、DisplayName、IntervalKind、GroundingStructureKind、拓扑 ID、SwitchAssemblyId 和 Switch DTO，但没有 Index 与 Function。

当前 `ProjectFileFormat` 为：

- `PreviousVersion = 1`；
- `CurrentVersion = 2`。

当前 `ProjectFileContainer` 直接按 Version 1/2 做 Manifest 校验，并在 Version 1 打开时补 Professional section。当前没有独立的通用 Migration Router 或 Migration Handler。

因此 Version 2 → 3 需要在现有 `ProjectFileContainer` 读取边界中增加显式迁移结构，不能假设仓库已经存在可复用的迁移框架。

### 2.3 Command 与 Undo/Redo

当前相关文件位于：

- `src/DistributionDrawing.Rendering.Wpf/Interaction/CommandStack.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/AddRingCabinetCommand.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/RemoveRingCabinetCommand.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/DeviceCommandFactory.cs`

`CommandStack` 只管理 ICommand 的 Execute、Undo、Redo、History 和 Dirty 状态，不感知 RingCabinet 字段。

`AddRingCabinetCommand` 保存已创建的 RingCabinet 和 RingCabinetLayout；Undo 移除同一对象，Redo 重新加入同一对象。新增 BayIndex 和 BayFunction 不应改变这一 Command 结构。

### 2.4 Inspector 与 Selection

当前相关文件位于：

- `src/DistributionDrawing.Rendering.Wpf/PropertyInspector/PropertyProjector.cs`
- `src/DistributionDrawing.Rendering.Wpf/PropertyInspector/SelectionObjectResolver.cs`
- `src/DistributionDrawing.Rendering.Wpf/PropertyInspector/ResolvedSelection.cs`

`PropertyProjector.ProjectInterval` 当前显示 IntervalId、ParentCabinetId、Sequence、DisplayName、IntervalKind、GroundingStructureKind、ExternalTerminalId 和 SwitchCount。

Selection 通过 IntervalId 和 ParentId 解析对象。新增只读字段不改变 SelectionReference、Resolver 或 SelectionTargetKind。

### 2.5 现有测试

当前仓库已有测试工程：

- `tests/DistributionDrawing.Domain.Tests/DistributionDrawing.Domain.Tests.csproj`

当前测试覆盖 Domain、Topology、Pole、CableTermination、PoleAttachment 和 IntegratedFeeder 等内容，但没有单独的 Persistence.Tests 工程。Persistence round-trip 与迁移测试应先评估现有测试工程边界；不得为了本阶段无条件创建新的 WPF 测试基础设施。

## 3. RingCabinetInterval 修改计划

### 3.1 保留 Sequence

不新增第二个排序字段，也不把 Sequence 改成可由 Template 直接输入的业务编号。

保留当前规则：

- Sequence 类型为 `int`；
- 从 1 开始；
- 在 RingCabinet 内连续且唯一；
- 由 IntervalDefinitions 集合顺序产生；
- Restore 时必须等于集合位置加 1；
- Layout 和 Rendering 继续使用 Sequence 排列。

### 3.2 新增 BayIndex

推荐在 `RingCabinetInterval` 中新增只读属性 `BayIndex`，类型为 `int`。

职责：

- 表示稳定的现场业务编号；
- 用于 Inspector、工作票、台账和设备识别；
- 不作为 Layout 排序键；
- 不因 Sequence 变化而自动变化。

构造入口必须通过内部构造函数或 Domain Factory 传入，不能增加 public setter。构造时拒绝 `BayIndex <= 0`。

### 3.3 新增 Function

在 `src/DistributionDrawing.Domain/Devices/RingCabinets/` 新增 Domain enum 文件：

- `BayFunction.cs`

第一版值冻结为：

- `Unknown`；
- `Incoming`；
- `Outgoing`；
- `Tie`；
- `PT`；
- `Metering`；
- `Reserve`。

在 `RingCabinetInterval` 中新增只读 `Function` 属性，类型为 `BayFunction`。

构造时必须验证枚举值已定义。新建路径拒绝 `Unknown`；恢复路径允许 `Unknown` 作为旧数据迁移结果。

### 3.4 聚合内唯一性

`RingCabinetInterval` 自身只能校验 BayIndex 为正，不能校验兄弟 Interval 冲突。

以下位置必须增加柜内唯一性校验：

- `RingCabinetDefinition` 创建时；
- `RingCabinet.Restore` 或统一的聚合校验；
- `RingCabinet.ValidateStructure` 最终防御性校验。

重复 BayIndex 必须在任何内部 Stable ID 生成前拒绝。

## 4. Definition 与 Restore 修改计划

### 4.1 RingCabinetIntervalDefinition

修改文件：

- `src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinetIntervalDefinition.cs`

增加只读输入：

- `BayIndex`：`int`；
- `Function`：`BayFunction`。

更新以下入口，使新建路径必须显式传入两个值：

- `CreateLoadSwitch`；
- `CreateIntegratedFeeder`；
- 未来 Template Builder 到 Definition 的映射入口。

Sequence 不进入 Definition 作为重复输入，仍由 Definition 集合顺序决定。

### 4.2 RingCabinetDefinition

修改文件：

- `src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinetDefinition.cs`

在构造或 `Create` 时校验：

- Definition 非空且至少包含一个 Interval；
- 每个 BayIndex 大于 0；
- BayIndex 在集合内唯一；
- Function 是已定义枚举值；
- Function 不为 Unknown。

这些校验必须早于 `RingCabinet.Create` 内部生成 MainBus、Interval、Terminal 和 ElectricalNode 的步骤。

### 4.3 RingCabinetRestoreDefinition

修改文件：

- `src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinetRestoreDefinition.cs`

在 `RingCabinetIntervalRestoreDefinition` 中增加：

- `BayIndex`：`int`；
- `Function`：`BayFunction`。

Restore 输入必须保留 Sequence 和新增字段，确保恢复时：

- Sequence 仍与集合位置一致；
- BayIndex 原样恢复；
- Function 原样恢复；
- Unknown 可以作为迁移恢复值；
- 所有原有 Stable ID 不变。

### 4.4 RingCabinet.Create/Restore

修改文件：

- `src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinet.cs`

创建时把 Definition 的 BayIndex/Function 传入 Interval 构造器。恢复时把 RestoreDefinition 的 BayIndex/Function 传入恢复 Interval 构造器。

不得在 Create 或 Restore 中：

- 根据 DisplayName 生成 BayIndex；
- 根据 IntervalKind 猜 Function；
- 生成新的恢复 ID；
- 改变现有 Switch、Terminal、Node 和 Assembly 创建顺序。

## 5. Persistence DTO 与 Mapper 修改计划

### 5.1 DTO

修改文件：

- `src/DistributionDrawing.Infrastructure/Persistence/ProjectDomainDto.cs`

在 `ProjectRingCabinetIntervalDto` 增加：

- `Index`：`int`；
- `Function`：`string`。

Sequence 已存在，不重复增加。

DTO 只保存数据，不包含 Domain 校验或 Template 对象引用。

不修改：

- `ProjectLayoutDto.cs`；
- `ProjectProfessionalDto.cs`；
- Cable、PoleAttachment 或其他 DTO。

### 5.2 保存 Mapper

修改文件：

- `ProjectDomainDto.cs` 内的 `ProjectDomainMapper.ToDto(RingCabinetInterval)`。

保存时：

- `Sequence` 继续写出 `interval.Sequence`；
- `Index` 写出 `interval.BayIndex`；
- `Function` 使用 Domain 枚举的规范字符串编码；
- 其余字段保持现有映射。

### 5.3 恢复 Mapper

修改文件：

- `ProjectDomainDto.cs` 内的 `RestoreRingCabinet`。

恢复时：

- 解析 DTO Function；
- 将 Index、Function 和 Sequence 传入 `RingCabinetIntervalRestoreDefinition`；
- 继续恢复现有 Stable ID 和拓扑对象；
- 由 RingCabinet.Restore 执行最终聚合校验。

Domain Mapper 不负责识别旧格式。旧格式先经过 Migration 转换为当前 DTO，再进入 Mapper。

### 5.4 Layout 边界

`ProjectLayoutDto` 不需要修改。RingCabinetLayoutFactory 和现有 Layout DTO 继续按照 Sequence 生成和保存几何；BayIndex/Function 不属于 Layout 事实。

## 6. FormatVersion 与 Migration 修改计划

### 6.1 Version 升级

修改文件：

- `src/DistributionDrawing.Infrastructure/Persistence/ProjectFileFormat.cs`

计划：

- 保留 Version 1 的可读能力；
- 保留 Version 2 的可读能力；
- 将 `CurrentVersion` 升级为 3；
- 不再用单一 PreviousVersion 表达所有历史版本。

建议增加明确的 SupportedVersions 或版本判断方法，但不在本计划阶段定义具体 API 名称。

### 6.2 Migration 入口

当前没有独立 Migration Router。最小实现可以先修改：

- `src/DistributionDrawing.Infrastructure/Persistence/ProjectFileContainer.cs`

将 Open 流程拆成：

1. 读取原始 Manifest；
2. 按 Manifest Version 读取兼容输入；
3. Version 1 先应用既有 Professional 补充语义；
4. Version 2 应用 Version 2 → 3 迁移；
5. Version 3 严格读取当前 DTO；
6. 迁移完成后统一交给 Domain Mapper；
7. 用当前格式构造 ProjectFileDocument。

如果在实现中新增独立迁移类，应保持最小职责，例如：

- `src/DistributionDrawing.Infrastructure/Persistence/ProjectFormatMigration.cs`

该文件是建议新增文件，不是当前已有文件。是否拆出独立类应以避免 `ProjectFileContainer` 继续膨胀为准，不得创建第二套持久化入口。

### 6.3 Version 2 → 3 规则

对每个旧 `ProjectRingCabinetIntervalDto`：

- `Index = Sequence`；
- `Function = Unknown`。

必须保持不变：

- CabinetId；
- IntervalId；
- SwitchId；
- SwitchAssemblyId；
- TerminalId；
- ElectricalNodeId；
- Sequence；
- DisplayName；
- IntervalKind；
- GroundingStructureKind；
- Layout；
- Connection、OverheadLine 和 Professional 数据。

禁止：

- 从 DisplayName 推断 Index；
- 从设备类型推断 Function；
- 从拓扑推断 Function；
- 从 Layout 坐标推断 Index；
- 在迁移中重新生成任何 Stable ID。

### 6.4 Version 3 严格读取

Version 3 当前 DTO 必须具有：

- 正整数 Index；
- 非空且可解析的 Function；
- Function 为已定义值。

缺失 Index、缺失 Function 或未知 Function 字符串应明确失败，不使用 C# 默认值 `0`/`null` 静默恢复。

只有规范值 `Unknown` 才表示迁移兼容状态；未知字符串不能映射为 Unknown。

### 6.5 保存与 Dirty

迁移在加载时仅生成内存中的当前版本数据，不把迁移本身写入 CommandStack，也不通过 Command 记录 Dirty。

用户后续保存时，`ProjectFileContainer.Save` 写出 Version 3 和完整 Index/Function。原文件不应在加载阶段被就地覆盖。

现有 `CommandStack` Dirty 规则保持不变；迁移造成的“当前文档字段完整化”不作为用户编辑动作。

## 7. Stable ID 风险分析

新增 BayIndex 与 Function 只是 Interval 的业务属性，不是新对象集合。因此不应影响：

- CabinetId；
- IntervalId；
- SwitchId；
- SwitchAssemblyId；
- TerminalId；
- ElectricalNodeId；
- ConnectionId；
- Layout 对象 ID。

创建时，新增字段由 Definition 传入，不改变现有 `Guid.NewGuid()` 调用的对象数量与顺序。

恢复时，新增字段由 DTO/RestoreDefinition 传入，不生成替代对象。

必须通过 Save/Load round-trip 测试确认所有 Stable ID 不变。迁移测试也必须比较 Version 2 原始对象 ID 与迁移后当前 Domain 对象 ID。

## 8. Command、Undo/Redo 与 Dirty 影响

### 8.1 现有 Add/Remove Command

无需修改：

- `src/DistributionDrawing.Rendering.Wpf/Interaction/CommandStack.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/AddRingCabinetCommand.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/RemoveRingCabinetCommand.cs`

原因：Command 保存的是完整 RingCabinet 聚合对象；新增字段随同一对象被 Execute、Undo 和 Redo 保留。

### 8.2 模板生成边界

未来 Template Builder 应先生成完整的 RingCabinet + RuntimeLayout，再由一个原子 Add Command 进入 CommandStack。

本次 Domain/Persistence 补充不新增 Template Command，不改变：

- 一个 RingCabinet 生成动作对应一个原子 Command；
- Undo 完整移除对象；
- Redo 恢复同一对象和 Stable ID；
- Dirty 由 CommandStack 管理。

### 8.3 不应新增字段 Command

本阶段不实现 BayIndex/Function 编辑 Command。第一版 Inspector 只读，未来编辑必须另行设计唯一性冲突、专业校验、SelectionTransition 和审计语义。

## 9. Inspector 影响评估

修改文件：

- `src/DistributionDrawing.Rendering.Wpf/PropertyInspector/PropertyProjector.cs`

在 `ProjectInterval` 的专业属性区域增加只读行：

- BayIndex；
- BayFunction。

不修改：

- `SelectionReference`；
- `SelectionObjectResolver`；
- `ResolvedSelection` 对象身份字段；
- `SelectionTargetKind`；
- RingCabinet Symbol；
- RuntimeLayout。

Selection 仍以 IntervalId 和 ParentId 定位同一个对象。新增字段不改变对象身份，也不要求新的 SelectionTarget。

第一版禁止普通 Inspector 编辑 BayIndex/Function：

- BayIndex 修改可能造成柜内重复；
- Function 修改可能影响工作票和专业分析；
- 当前没有专用编辑规则与审计合同；
- 只读展示可以先验证持久化和迁移结果。

## 10. Desktop 创建入口影响

虽然本阶段不实现 UI，但生产实现必须同步检查当前创建入口：

- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/RingCabinetCreationConfiguration.cs`
- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/RingCabinetCreationFactory.cs`
- `src/DistributionDrawing.Desktop/RingCabinetCreation/RingCabinetCreationViewModel.cs`
- `src/DistributionDrawing.Desktop/RingCabinetCreation/RingCabinetCreationDialog.xaml`
- `src/DistributionDrawing.Desktop/RingCabinetCreation/RingCabinetCreationDialog.xaml.cs`

当前配置只有 DisplayName、IntervalKind 和 GroundingStructureKind，不能满足新建 Domain 必须提供 Function 与 BayIndex 的规则。

因此 Domain/Persistence 实现与当前创建闭环不能割裂发布：后续创建入口必须显式提供每行 BayIndex 和 BayFunction，普通模板可以默认生成连续 Index，但不得根据 IntervalKind 或名称猜 Function。

这属于后续生产实现的必需配套，不属于本轮文档计划之外的业务扩展。

## 11. 实际修改文件清单

### 11.1 必须修改文件

| 文件 | 修改目的 | 风险 |
| --- | --- | --- |
| `Domain/.../RingCabinets/RingCabinetInterval.cs` | 增加 BayIndex、Function 与局部校验 | 构造调用点较多，需保持拓扑不变 |
| `Domain/.../RingCabinets/RingCabinetIntervalDefinition.cs` | 承载新建输入 | 现有 Factory 签名会受影响 |
| `Domain/.../RingCabinets/RingCabinetDefinition.cs` | 柜内 BayIndex 唯一性与新建 Unknown 拒绝 | 必须在生成 ID 前校验 |
| `Domain/.../RingCabinets/RingCabinetRestoreDefinition.cs` | 承载恢复字段 | Stable ID 恢复边界变化 |
| `Domain/.../RingCabinets/RingCabinet.cs` | Create/Restore 映射与最终聚合校验 | 影响所有 RingCabinet 创建/恢复路径 |
| `Infrastructure/Persistence/ProjectDomainDto.cs` | DTO、保存、恢复映射 | 需要与 Version 3 迁移配套 |
| `Infrastructure/Persistence/ProjectFileFormat.cs` | 支持当前版本 3 | 旧版本兼容不能丢失 |
| `Infrastructure/Persistence/ProjectFileContainer.cs` | 读取分支和迁移接入 | 版本判断与当前 DTO 读取顺序敏感 |
| `Rendering.Wpf/PropertyInspector/PropertyProjector.cs` | BayIndex/Function 只读投影 | 只应影响 Inspector 展示 |
| 当前 RingCabinet 创建入口相关文件 | 提供显式 BayIndex/Function | UI 配置与 Domain 合同需同步 |

### 11.2 建议新增文件

| 文件 | 目的 | 是否必须 |
| --- | --- | --- |
| `src/DistributionDrawing.Domain/Devices/RingCabinets/BayFunction.cs` | Domain 枚举 | 是 |
| `src/DistributionDrawing.Infrastructure/Persistence/ProjectFormatMigration.cs` | 隔离 Version 1/2 → 3 迁移逻辑 | 推荐，取决于实现时的 Container 复杂度 |
| Persistence migration 测试文件 | 覆盖 Version 2 与 Version 1 迁移 | 必须有测试覆盖，具体测试工程需先确认 |

### 11.3 明确不修改文件

本实施切片不应修改：

- `src/DistributionDrawing.Rendering.Wpf/Interaction/CommandStack.cs`；
- `AddRingCabinetCommand.cs`；
- `RemoveRingCabinetCommand.cs`；
- `SelectionReference`、`SelectionObjectResolver` 和 `SelectionTargetKind`；
- `ProjectLayoutDto.cs`；
- `ProjectProfessionalDto.cs`；
- RingCabinet Symbol、LayoutFactory 和 TerminalAnchor；
- Domain Topology、Terminal、ElectricalNode、Connection；
- PT/DTU Domain；
- Template Builder Runtime 代码。

Desktop 创建文件只有在 Domain 合同落地时作为必要配套修改；不应把业务逻辑放入 MainWindow。

## 12. 推荐实施顺序

### 阶段 1：Domain 枚举和字段

1. 新增 `BayFunction`。
2. 为 Interval、Definition、RestoreDefinition 增加 BayIndex/Function。
3. 更新 RingCabinet Create/Restore 的字段传递。
4. 增加 Definition 和聚合校验。
5. 更新所有现有 Domain Factory 和测试 Fixture，使其显式提供 Function。

验证：

- 现有 Domain 测试仍可运行；
- 正数/唯一/非连续 BayIndex 规则正确；
- 新建 Unknown 被拒绝；
- Restore Unknown 可用；
- 拓扑和 Stable ID 不变。

### 阶段 2：Persistence 当前 DTO

1. DTO 增加 Index/Function。
2. ToDto 写出新字段。
3. Restore mapper 读取新字段。
4. Version 3 当前格式严格校验。

验证：

- Version 3 Domain round-trip；
- 缺失字段失败；
- 未知 Function 字符串失败。

### 阶段 3：Migration

1. 保留 Version 1 读取能力。
2. 继续保留 Version 2 读取能力。
3. 增加 Version 2 → 3 迁移。
4. Version 1 依次经过既有补充逻辑和 Version 2 → 3 迁移。
5. 迁移结果交给统一 Domain Mapper。

验证：

- Version 2：Index=Sequence、Function=Unknown；
- Version 1：迁移链完整；
- Stable ID、Layout、Professional 数据保持；
- 原文件不会被打开操作覆盖。

### 阶段 4：Inspector 只读

1. PropertyProjector 增加 BayIndex/Function。
2. 不改变 Selection 和 Resolver。
3. 不增加编辑 Command。

验证：

- Interval Selection 显示新增字段；
- Unknown 显式显示；
- Selection ID 与现有行为不变。

### 阶段 5：创建入口配套

1. 扩展创建配置模型。
2. ViewModel 做正数、唯一和 Unknown 前置校验。
3. CreationFactory 映射显式 BayIndex/Function。
4. 保持 MainWindow 薄。

验证：

- 连续与非连续 Index 创建；
- 两类现有 IntervalKind 结构保持正确；
- Save/Load、Undo/Redo、Selection 和 Dirty 闭环不回归。

## 13. 测试计划

### 13.1 Domain 创建测试

在现有 `tests/DistributionDrawing.Domain.Tests/` 测试结构中补充：

- Sequence 仍生成 `1..N`；
- BayIndex 正数成功；
- BayIndex 为 0 或负数失败；
- 柜内重复 BayIndex 失败；
- `1、2、5、7` 等非连续 BayIndex 成功；
- 新建 Function 为 Unknown 失败；
- 未定义 Function 值失败；
- Function 与 IntervalKind 分离，不改变现有拓扑校验。

### 13.2 Persistence Round-trip

需要建立可执行的 Infrastructure Persistence 测试覆盖，优先复用仓库现有测试工程模式；如果当前没有合适工程，应在实施阶段单独评估测试工程边界，不创建无法验证的 WPF 测试主机。

必须验证：

- Version 3 保存/加载 Index 与 Function；
- Sequence、Index、Function 全部保持；
- CabinetId、IntervalId、SwitchId、TerminalId、NodeId、SwitchAssemblyId 保持；
- Layout、Connection、OverheadLine 和 Professional 数据保持。

### 13.3 Version 2 Migration

准备真实或最小合法 Version 2 fixture，验证：

- 每个 Interval 的 Index 等于原 Sequence；
- Function 为 Unknown；
- 不解析 DisplayName；
- 不根据 IntervalKind 猜 Function；
- 所有 Stable ID 保持；
- 迁移后保存为 Version 3。

### 13.4 Version 1 Migration

验证 Version 1：

- 先补充缺失 Professional section；
- 再完成 Index/Function 迁移；
- 不丢失既有 Domain、Layout、Professional 数据；
- 不改变 Stable ID。

### 13.5 Command 与 Dirty 回归

不修改 Command 实现，但需要回归：

- AddRingCabinet Execute/Undo/Redo 保留新字段；
- RemoveRingCabinet Execute/Undo/Redo 保留新字段；
- CommandStack History 与 Dirty 行为不变；
- 模板生成未来仍作为一个原子 RingCabinet Command。

## 14. 风险与控制措施

### 14.1 旧入口签名扩散

Definition 工厂签名增加两个必填业务值，会影响 Desktop、Demo、测试 Fixture 和未来 Builder。应按编译器错误逐一更新，不使用默认 Function 或自动猜测绕过。

### 14.2 Version 兼容回归

当前只有 PreviousVersion/CurrentVersion 两个常量，升级时容易丢失 Version 1 支持。实现必须先加入版本矩阵测试，再替换读取分支。

### 14.3 DTO 默认值误恢复

`int` 缺失可能落为 0，字符串可能落为 null。当前 Version 3 Mapper 必须在 Domain 创建前显式验证，而不是把 DTO 默认值交给 Domain 产生模糊错误。

### 14.4 业务事实被名称替代

旧数据迁移不能从 DisplayName 恢复 Index/Function。尤其是“负1间隔”等文本可能是展示名称，不一定是稳定业务事实。

### 14.5 Function 与设备结构混淆

IntervalKind 只表示 LoadSwitch/IntegratedFeeder 等结构。不得把它映射为 Outgoing 或 Reserve，除非新的专业兼容规则另行确认。

### 14.6 Stable ID 与迁移对象重建

迁移只能复制/补充 DTO 值，不能通过重新调用普通 Create 生成新聚合。恢复必须继续使用 RestoreDefinition 和原始 ID。

## 15. 结论

P0-7-D-4 的最小生产实施切片是：

1. 新增 Domain `BayFunction`；
2. 在现有 RingCabinetInterval 创建/恢复边界增加 BayIndex/Function；
3. 保留 Sequence 和现有拓扑不变量；
4. DTO 增加 Index/Function；
5. FormatVersion 从 2 升级到 3；
6. 实现 Version 1/2 到 Version 3 的显式迁移；
7. Inspector 只读显示新增事实；
8. 同步当前创建配置，使新建路径可以显式提供字段；
9. 用 Domain、Persistence、Migration 和回归测试证明 Stable ID 与现有闭环不变。

不需要修改 CommandStack、Add/Remove Command、SelectionReference、Resolver、RuntimeLayout、Rendering Symbol 或 Topology。PT/DTU、TemplateReference、JSON Template 和 Builder Runtime 不属于该最小补充。

只有在 Domain 与 Persistence 能无损承载 BayIndex/Function 后，才可以进入 Template Builder 生产实现。
