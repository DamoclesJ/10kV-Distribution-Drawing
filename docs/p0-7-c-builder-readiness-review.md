# P0-7-C Builder Implementation Readiness Review

> Review 基线：checkpoint commit `a5d67178d9e3a738d7160c3e999ae2ef11e499d9`。<br>
> 上游设计：`docs/template-system-design.md`、`docs/p0-7-a-review.md`、`docs/template-builder-design.md`。<br>
> 范围：只审查当前代码是否具备 Template Builder 实施条件，不实现 Builder、Template Runtime、Command、UI 或任何生产代码。

## 1. Review 结论

当前架构已经具备实现“现有 Domain 能力子集”的 Template Builder 条件：

- 可把 Template Bay 映射为现有 `RingCabinetIntervalDefinition`；
- 可由 `RingCabinet.Create` 一次创建完整聚合；
- 可由 `RingCabinetLayoutFactory` 根据真实聚合生成匹配布局；
- 可复用 `AddRingCabinetCommand` 原子提交 Domain + RuntimeLayout；
- 可直接进入现有 CommandStack、Selection、Inspector、Rendering 和 FormatVersion 2 Persistence。

该可实施子集包括：

- LoadSwitchInterval；
- IntegratedFeederInterval；
- 当前 Domain 允许的纯类型与混合 RingCabinet；
- 当前三种 GroundingStructureKind；
- 对应 RingCabinetLayout。

完整 P0-7 目标中的 PT Bay 当前尚不具备实施条件。项目没有 PTInterval Domain、PT 专用拓扑、PT Layout、Rendering、Selection/Inspector 投影及相应 Persistence 合同。Builder 不能用现有间隔、Attachment 或 CableTermination 代替 PT。

因此 Readiness 结论分为两级：

| 范围 | 结论 |
| --- | --- |
| 当前两类 Interval 的最小 Template Builder | 已具备实施条件 |
| 包含 PT Bay 的完整 Builder | 存在明确前置阻断，不可直接实现 |

此外，P0-7-B 设计中的“Builder 注入全部 Stable ID”与当前代码不完全一致。当前 RingCabinetId 由外部传入，内部 ID 由 Domain 工厂直接生成。该差异不影响 Undo/Redo、Selection 或 Save/Load 的稳定性，但必须在实现前收敛设计口径。

## 2. RingCabinet Domain 能力审查

### 2.1 当前创建入口

当前完整创建链路为：

```text
RingCabinetIntervalDefinition[]
→ RingCabinetDefinition.Create(cabinetId, displayName, definitions)
→ RingCabinet.Create(definition)
→ validated RingCabinet aggregate
```

`RingCabinet` 构造函数是 private，外部不能绕过工厂自由拼装聚合。`RingCabinet.Create` 接收定义，创建全部内部对象，最后调用聚合结构校验。

### 2.2 已支持能力

当前 Domain 可以创建：

- RingCabinet 根 Device；
- MainBus ElectricalNode；
- 有序 RingCabinetInterval；
- LoadSwitchInterval；
- IntegratedFeederInterval；
- 每个间隔要求的 SwitchDevice；
- SwitchAssembly；
- Circuit、Earth、Intermediate 等内部 ElectricalNode；
- 开关内部 Terminal；
- 每个间隔的 External Terminal；
- Terminal 与 ElectricalNode 的固定绑定关系；
- 三种 IntegratedFeeder GroundingStructureKind 对应的内部拓扑；
- 聚合内 Stable ID 唯一性和结构一致性校验。

LoadSwitchInterval 由 Domain 创建：

- LoadSwitch；
- GroundSwitch；
- CircuitNode；
- EarthNode；
- 开关端子；
- ExternalTerminal；
- LoadSwitchThreePosition SwitchAssembly。

IntegratedFeederInterval 由 Domain 创建：

- IsolationSwitch；
- CircuitBreaker；
- GroundSwitch；
- IntermediateNode；
- CircuitNode；
- EarthNode；
- 开关端子；
- ExternalTerminal；
- IntegratedFeeder SwitchAssembly。

### 2.3 Connection 能力的准确边界

RingCabinet 聚合内部没有创建通用 `Connection` 对象。内部固定导通与设备接入通过 Terminal 绑定 ElectricalNode、SwitchDevice 端子及 SwitchAssembly 表达。

这不是当前 Builder 的缺失能力。Builder 不应为柜内固定结构额外创建 Connection，否则会建立与现有 Domain 拓扑重复的第二套事实。

RingCabinet 的 ExternalTerminal 允许未来接入 Cable 或 OverheadLine，但模板创建一个独立柜体时不应自动创建外部 Connection。外部连线仍属于后续独立用户动作和类型化 Command。

### 2.4 当前数量规则

聚合校验当前要求：

- 纯 LoadSwitchOnly：3、4、5、6 个间隔；
- 纯 IntegratedFeederOnly：4 或 6 个间隔；
- Mixed：不增加模板比例约束，但仍校验每个间隔自身结构。

P0-7-A 模板模型可以表达未来更开放数量，但第一版 Builder 必须服从当前 Domain 校验，不能因模板支持 2 个普通 Bay 就放宽生产 Domain。

### 2.5 缺失能力

当前 Domain 不支持：

- PTInterval；
- PT 专用 Terminal 和内部节点语义；
- PT 隔离刀方案；
- PT 断路器方案；
- DTU 二次配置的生产对象；
- 未确认的 Function/EquipmentConfiguration 兼容性规则。

### 2.6 建议

第一笔 Builder 只映射当前两类 Interval，调用现有 Domain 工厂。不得由 Builder 手工创建 Bay、Switch、Terminal、ElectricalNode 或 SwitchAssembly。

对 PT 和 DTU 模板应返回明确的 UnsupportedCapability 结果；在前置模型完成前不得降级或伪造。

## 3. Domain 创建边界审查

### 3.1 当前职责位置

对象创建职责明确位于 Domain 聚合工厂：

```text
External caller
→ creates RingCabinetDefinition
→ RingCabinet.Create
   ├── creates internal IDs
   ├── creates intervals
   ├── creates switches
   ├── creates nodes
   ├── creates terminals
   ├── creates switch assemblies
   ├── establishes fixed topology
   └── validates aggregate
```

Desktop/Rendering.Wpf 中的 `RingCabinetCreationFactory` 只把创建配置转换为 `RingCabinetDefinition`，没有手工拼装内部拓扑。这是 Builder 应复用的模式。

### 3.2 架构判断

由 Domain 自己创建内部对象更符合当前架构，原因包括：

- 聚合不变量与创建行为位于同一边界；
- 外部不能构造缺少 Terminal、Node 或 SwitchAssembly 的半聚合；
- GroundingStructureKind 与实际端子—节点拓扑由同一分支决定；
- Persistence Restore 可以使用完整 ID 定义恢复，但普通创建入口保持封装；
- Builder 只负责 Template 到 Domain Definition 的语义映射。

外部 Builder 逐个创建所有内部对象会复制 Domain 专业规则，并与 private 构造和 internal 创建边界冲突，因此不建议。

## 4. Stable ID 生成审查

### 4.1 当前 ID 来源

| ID | 当前生成位置 |
| --- | --- |
| RingCabinet DeviceId | 外部创建配置工厂传入 `RingCabinetDefinition`；当前生产入口使用 Guid.NewGuid |
| MainBus ElectricalNodeId | `RingCabinet.Create` 内部 Guid.NewGuid |
| IntervalId | `RingCabinet.Create` 的间隔创建分支内部 Guid.NewGuid |
| SwitchDeviceId | Domain 间隔创建分支内部 Guid.NewGuid |
| Switch TerminalId | Domain 间隔创建分支内部 Guid.NewGuid |
| Circuit/Earth/Intermediate NodeId | Domain 间隔创建分支内部 Guid.NewGuid |
| ExternalTerminalId | Domain 间隔创建分支内部 Guid.NewGuid |
| SwitchAssemblyId | Domain 间隔创建分支内部 Guid.NewGuid |
| AttachmentId | 与 RingCabinet 无关；由 CableTerminationAttachmentCreationFactory 创建 |

Persistence 恢复走 `RingCabinet.Restore(RingCabinetRestoreDefinition)`，使用 DTO 中已有的全部 ID，不生成替代 ID。

### 4.2 与 P0-7-B 的一致性

P0-7-B 推荐 Builder 注入 StableIdGenerator，并描述为模板生成全部对象 ID。当前实现只允许 Builder控制 CabinetId，内部 ID 仍由 Domain 工厂控制，因此严格意义上不一致。

但当前模式已经满足运行时稳定性：

1. `RingCabinet.Create` 只在首次 Build 时执行一次；
2. BuildResult 保存创建完成的 RingCabinet 和 RingCabinetLayout；
3. AddRingCabinetCommand 保存同一对象；
4. Undo 移除同一对象；
5. Redo 重新加入同一对象，不调用 RingCabinet.Create；
6. Save 保存全部内部 ID，Load 通过 Restore 恢复相同 ID。

所以内部随机 ID 不阻断 Undo/Redo、Save/Load、Selection 或对象引用。它主要影响：

- Builder 无法控制全部 ID 的生成来源；
- 测试无法通过注入序列精确预测所有内部 ID；
- 若未来要求可重复 Build 产生同一 ID 图，当前 API 不支持。

### 4.3 推荐调整

第一版采用最小方案：

- Builder/创建协调层生成 CabinetId；
- RingCabinet Domain 工厂继续生成内部 ID；
- Builder 只执行一次并返回固定对象；
- Redo 严禁重新 Build。

同时修正文档口径为“Builder 负责触发并固定本次创建的 Stable ID 图”，而不是“Builder 必须直接生成每一个内部 ID”。

如果未来确实需要全 ID 可注入性，应单独设计 Domain-owned `IStableIdSource` 或创建 ID 图入口，由 `RingCabinet.Create` 内部消费。不得让 Builder通过 Restore API 或手工对象拼装绕开聚合工厂。该增强不是当前最小 Builder 的功能阻断。

## 5. RingCabinetLayout 能力审查

### 5.1 当前 Layout 责任

`RingCabinetLayout` 是实例布局值对象/容器，保存：

- CabinetId；
- Position；
- Width/Height；
- MainBusY；
- LabelOffset；
- IntervalLayout；
- SwitchLayout。

它属于方案 A：保存实例布局，不负责解释 Template 或生成专业结构。

`RingCabinetLayoutFactory` 属于生成策略，读取已创建的 RingCabinet 聚合并生成初始布局。它当前：

- 根据实际间隔数量计算柜宽；
- 根据 IntervalKind 创建类型感知布局；
- 根据 GroundingStructureKind 排列 IntegratedFeeder 开关；
- 验证每个实际 SwitchId 恰有一个 SwitchLayout；
- 使用 DocumentPoint 放置整个柜体。

### 5.2 Template LayoutRule 接入可行性

未来链路可以成立：

```text
Template.LayoutRule
→ controlled LayoutRule resolution
→ RingCabinetLayoutFactory / layout strategy
→ RingCabinetLayout
```

但当前 `RingCabinetLayoutFactory` 的尺寸为内部常量，没有接收 LayoutRule。若第一版只支持一个默认规则，可以直接复用当前 Factory，把模板 LayoutRule 限定为受支持的默认规则引用。

若第一版必须支持可变 BayWidth、Spacing 或特殊尺寸，则需要在后续实现中增加受控 Layout strategy 输入。这属于 LayoutFactory 能力扩展，但不能让 Template 保存实例坐标，也不能把专业结构复制到 Layout。

### 5.3 职责冲突判断

不存在根本职责冲突：

- Template 保存规则引用；
- LayoutFactory 负责计算；
- RingCabinetLayout 保存结果；
- Rendering 消费结果。

需要避免同时保留“Factory 内硬编码默认值”和“LayoutRule 中另一套默认值”作为两个权威来源。第一版应明确唯一默认规则来自现有 Factory，或把这些常量提炼成一个受控策略；不能两者并存并可能产生不同结果。

PT/DTU 仍是阻断：当前 Factory 没有 PT Interval 分支，也没有 DTU RuntimeLayout。

## 6. Command 架构适配审查

### 6.1 当前 Command 层级

当前编辑 Command 位于 `DistributionDrawing.Rendering.Wpf.Interaction` 及其 Devices 子命名空间。现有模式包括：

- AddRingCabinetCommand；
- RemoveRingCabinetCommand；
- AddCableTerminationAttachmentCommand；
- RemoveCableTerminationAttachmentCommand；
- MoveAttachmentCommand；
- MoveRingCabinetCommand。

虽然程序集名称包含 Rendering.Wpf，但该 Interaction 层已经是当前生产编辑命令的事实位置。Template Builder 第一版应遵循当前结构，不为模板建立第二套 Command 系统。

### 6.2 CreateRingCabinetFromTemplateCommand 的位置判断

不建议新增一个在 Execute/Redo 内运行 Builder 的 `CreateRingCabinetFromTemplateCommand`。

安全流程是：

```text
Template Builder.Build
→ fixed BuildResult(RingCabinet, RingCabinetLayout)
→ existing AddRingCabinetCommand
→ CommandStack.ExecuteCommand
```

Builder 在 Command 创建前执行；Command 只负责原子提交固定结果。

如果未来确有模板专用 Command，它也应位于现有 Interaction/Devices 层，并且构造时接收已完成的 BuildResult，不得保存 Template 后在 Redo 时重建。但对当前 RingCabinet 来说，这与现有 AddRingCabinetCommand 重复，没有新增必要。

### 6.3 原子 Command 与多个 AddCommand 比较

方案 A，一个原子 Command：

- 一次用户操作只有一个 History 项；
- Domain + Layout 原子提交；
- Undo 一次移除完整柜体；
- Redo 恢复同一对象；
- SelectionTransition 只关联一个 Command；
- 失败不会留下半柜。

方案 B，多个 AddCommand：

- History 被内部实现细节污染；
- Undo 需要多次操作；
- 中途失败可能留下半状态；
- Dirty、Selection 和 Redo 顺序复杂；
- 与 RingCabinet 聚合边界冲突。

推荐方案 A，并直接复用现有 AddRingCabinetCommand。

## 7. Undo/Redo 数据保存策略

### 7.1 方案 A：Command 保存创建结果对象

Command 保存：

- 创建完成的 RingCabinet 聚合；
- 创建完成的 RingCabinetLayout。

Execute 加入两者，Undo 移除两者，Redo 再次加入同一对象。

这正是当前 AddRingCabinetCommand 的行为，符合现有 Command 架构。

### 7.2 方案 B：Command 保存 Template + Parameters

Redo 时重新 Build 会导致：

- 新 CabinetId 或内部 ID；
- Layout 可能因规则版本变化而漂移；
- Template 后续修改影响历史 Command；
- SelectionReference 指向旧对象；
- 外部对象引用失效；
- Undo/Redo 不再是同一状态快照。

不推荐。

### 7.3 推荐

采用方案 A。Template 和 BuildContext 只用于首次 Build；Command 保存固定 BuildResult 中的真实对象，不需要保存 Template，也不把 Template 写入 Persistence。

几十个内部对象已经封装在 RingCabinet 聚合内，不需要 Command 再保存一份扁平对象列表。布局子对象同样由 RingCabinetLayout 持有。

## 8. Selection 能力审查

### 8.1 当前能力

`SelectionTargetKind` 当前支持：

- `RingCabinet`；
- `RingCabinetInterval`；
- `Device`；
- 其他现有目标。

`SelectionObjectResolver` 能解析：

- RingCabinet：通过 CabinetId 获取 Domain 根对象与 RingCabinetLayout；
- RingCabinetInterval：通过 IntervalId 和可选 ParentId 获取所属柜体、Interval 与 IntervalLayout；
- 柜内 Switch：通过 Device Target 和 Parent Interval 解析。

因此当前已经支持选择整个柜体和 Bay/Interval，无需为 Template Builder 修改 Selection。

### 8.2 创建后选择建议

推荐方案 A：自动选择整个柜体。

理由：

- 用户动作是创建一个完整 RingCabinet；
- RingCabinet 是聚合根和 Add Command 根对象；
- CabinetId 在首次 Build 后稳定；
- 选择第一个 Bay 会把数组首项变成额外 UX 规则；
- 不选择会弱化创建成功反馈。

成功顺序应沿用现有创建体验：

```text
Execute AddRingCabinetCommand
→ RebuildScene / update InspectionSource
→ Select RingCabinet
→ Record SelectionTransition.ForAdd(before, after)
```

Builder 本身不创建 SelectionReference。Desktop 根据 BuildResult.RootObject.Id 使用现有 `SelectionTargetKind.RingCabinet` 构造选择。

## 9. Persistence 影响审查

### 9.1 当前支持

FormatVersion 2 当前已保存并恢复：

- RingCabinetId、DisplayName、MainBusNodeId；
- IntervalId、ParentCabinetId、Sequence、DisplayName、IntervalKind；
- GroundingStructureKind；
- Intermediate/Circuit/Earth NodeId；
- ExternalTerminalId；
- SwitchAssemblyId；
- SwitchDeviceId、TerminalId、SwitchState 等；
- 聚合 ElectricalNode 和 Terminal；
- RingCabinetLayout、IntervalLayout 和 SwitchLayout。

恢复路径使用 RingCabinet.Restore 保持全部 Stable ID，并验证 Domain 与 Layout 覆盖一致。

### 9.2 Builder 引入后的影响

对于当前两类 Interval，Builder 生成的是现有 Domain + RuntimeLayout 对象，因此：

- 不需要修改 DTO；
- 不需要升级 FormatVersion；
- 不需要保存 Template 或 BuildContext；
- Save/Load 可直接复用现有合同。

PT/DTU 不适用该结论。它们会引入当前 DTO 无法表达的新事实，必须在各自模型完成后单独评估 Persistence 与 FormatVersion，不能由 Builder 隐式绕过。

## 10. 当前架构状态汇总

### 10.1 已支持能力

- 完整 RingCabinet 聚合创建；
- 两类现有 Interval 及混合聚合；
- Switch、Terminal、ElectricalNode、SwitchAssembly 和固定内部拓扑创建；
- 类型感知 RingCabinetLayout；
- Domain + Layout 原子 Add Command；
- Undo/Redo Stable ID；
- RingCabinet 与 Interval Selection；
- Inspector 和 Existing Rendering 消费；
- FormatVersion 2 Save/Load。

### 10.2 缺失能力

- Template Runtime 输入类型；
- Template 到 RingCabinetIntervalDefinition 的 Builder 映射；
- Function/EquipmentConfiguration 支持矩阵；
- Builder 错误模型；
- LayoutRule 的运行时解析；
- PT 与 DTU 全套生产模型。

前四项属于 Builder 实现本身，不是现有架构缺陷。后两项中，LayoutRule 可通过首版单一默认规则收敛；PT/DTU 是完整范围前置阻断。

### 10.3 阻断问题

对“当前两类 Interval 的最小 Builder”：无生产架构阻断。

对“包含 PT Bay 的 Builder”：存在以下阻断：

- 无 PTInterval Domain；
- 无 PT 专用 Terminal/Node/拓扑；
- 无 PT LayoutFactory 分支；
- 无 PT Rendering；
- 无 PT Persistence 合同；
- 无 DTU RuntimeLayout（若模板同时要求 DTU）。

### 10.4 非阻断问题

- Domain 内部 ID 不能由 Builder 注入；
- RingCabinetLayoutFactory 当前使用固定常量，不解析 LayoutRule；
- Template Builder 尚无独立自动化测试位置和测试矩阵；
- Builder 层具体放置命名空间需在实现审查时选择，但不得形成反向依赖。

## 11. P0-7-D 推荐实施路线

### P0-7-D-1：最小 Template Runtime 与能力矩阵

目标：只定义当前可执行子集所需的不可变输入模型。

建议内容：

- RingCabinetTemplate；
- BayTemplate；
- BayFunction；
- LoadSwitchEquipmentConfiguration；
- IntegratedFeederEquipmentConfiguration；
- 默认 LayoutRule 引用；
- UnsupportedCapability / InvalidConfiguration 错误语义。

要求：PT 配置可以保留为目标契约或明确错误分支，但不能生成伪对象；不增加 Persistence。

### P0-7-D-2：RingCabinet Template Builder

目标：完成 Template 到现有创建定义的纯映射。

流程：

```text
Template
→ capability validation
→ RingCabinetIntervalDefinition[]
→ RingCabinetDefinition
→ RingCabinet.Create
→ RingCabinetLayoutFactory.Create
→ fixed BuildResult
```

第一版建议接受 Domain-owned internal IDs，只由 Builder 生成 CabinetId。测试验证所有内部 ID 非空、唯一且在 BuildResult 生命周期内固定。

### P0-7-D-3：现有 Command 接入

目标：通过 DeviceCommandFactory 或等价现有创建边界，把 BuildResult 转为 AddRingCabinetCommand。

要求：

- 一个用户动作一个 History 项；
- Command Execute 前已完成 Build；
- Redo 不重新 Build；
- Domain/Layout 失败不产生半状态；
- 保持当前 Dirty 语义。

### P0-7-D-4：Desktop 创建入口

目标：提供最小模板选择/参数确认和放置流程。

要求：

- MainWindow 保持薄；
- Template/Dialog 状态不进入 PlacementController；
- 成功后选择 RingCabinet；
- 登记 SelectionTransition.ForAdd；
- Cancel 不创建 BuildResult、Command 或 Dirty。

### P0-7-D-5：验证

至少覆盖：

- 纯 LoadSwitch 模板；
- 纯 IntegratedFeeder 模板；
- Mixed 模板；
- 三种 GroundingStructureKind；
- 当前 Domain 数量规则；
- 不支持的 PT/DTU 明确失败；
- Domain/Layout ID 覆盖；
- Add、Undo、Redo Stable ID；
- SelectionTransition；
- Save/Reload round trip；
- 与手工 RingCabinet 创建结果等价。

### PT 后续独立路线

PT 不应被塞入 Builder 实现提交。建议先完成独立的 PT Domain → Layout → Rendering → Selection/Inspector → Persistence 设计和实现，再扩展 Builder 能力矩阵。DTU 同理在二次配置与 RuntimeLayout 边界明确后接入。

## 12. 推荐下一步

推荐下一步进入 P0-7-D-1 实现前设计审查，范围限定为当前 Domain 已支持的 RingCabinet Template Runtime 输入模型与能力矩阵。

编码前应明确以下决策：

1. 第一版接受 RingCabinet Domain 内部生成 ID，Builder 只触发并固定结果；
2. 第一版 LayoutRule 仅支持现有 RingCabinetLayoutFactory 的默认规则；
3. PT/DTU 返回 UnsupportedCapability，不纳入第一笔 Builder 实现；
4. 复用 AddRingCabinetCommand，不新增模板专用 Command；
5. Template 和 BuildResult 均不进入 Persistence。

在这五点确认后，当前架构可以直接进入最小 Builder 实现。完整 PT-capable Builder 仍需等待 PT 专业模型前置阶段。

## 13. Review 范围确认

本次只新增：

- `docs/p0-7-c-builder-readiness-review.md`。

未修改 src、Domain、Persistence、FormatVersion、CommandStack、Selection、Rendering、Existing Commands、UI 或 ProjectRuntimeSession；未创建 Builder 或 Template Runtime 代码。
