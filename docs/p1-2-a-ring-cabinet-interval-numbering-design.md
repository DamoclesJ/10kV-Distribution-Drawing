# P1-2-A RingCabinet Interval Numbering & Configuration Design

## 1. 目的与范围

本文冻结环网柜间隔的固定位置、间隔类型配置和业务编号合同，为后续
Interval Type Change、Inspector 和 Command 实现提供边界。

本阶段不重新设计环网柜一次拓扑，也不修改生产代码、Persistence 或 Rendering。

## 2. 当前 Domain 能力

当前 `RingCabinet` 已经具备以下事实：

- `Intervals` 是有序集合；集合顺序产生 `Sequence`，从 1 开始且连续；
- `BayIndex` 在同一柜内必须为正数且唯一，当前用于保存模板/业务间隔索引；
- `IntervalKind` 已支持 `LoadSwitchInterval`、`IntegratedFeederInterval` 和 `PTInterval`；
- 每个间隔拥有自己的开关、Terminal、ElectricalNode 和 `SwitchAssembly`；
- `IntervalId`、开关 ID、Terminal ID 和 ElectricalNode ID 是稳定身份；
- V6 DTO 已保存 `IntervalKind`、`Sequence`、`BayIndex` 和现有结构身份。

当前创建 API 以 `RingCabinetIntervalDefinition` 作为结构输入，创建后
`RingCabinetInterval` 的结构属性是不可变的。因而类型变更不能由 UI 直接改枚举，
必须通过后续 Domain/Application Command 完成受控替换。

当前 Domain 已对纯 PT 柜要求一个间隔，但混合柜场景尚未对多个 `PTInterval`
实施全局唯一校验。目标合同必须扩展为：任意一个 `RingCabinet` 最多一个
`PTInterval`，无论柜体是否混合其他间隔类型。

## 3. 固定 Position 与物理顺序

### 3.1 Position 合同

环网柜创建后，间隔的物理位置固定：

```text
Position 1 -> -1
Position 2 -> -2
Position 3 -> -3
...
Position N -> -N
```

在当前模型中，`Sequence` 是最接近物理 Position 的既有 Domain 字段：它来自
间隔集合顺序，表示柜内物理排列。后续实现应明确将其作为不可编辑的 Slot/Position
事实使用。

间隔不得：

- 单独拖动；
- 交换位置；
- 通过 `RingCabinetIntervalLayout` 改变业务编号；
- 因柜体在 Canvas 上移动而改变 `Sequence` 或 `BayIndex`。

柜体整体移动只改变 `RingCabinetLayout.Position`。

### 3.2 Position 与 BayIndex

当前历史/模板合同中，`BayIndex` 已表示间隔业务索引，并且被 V6 保存。因此，
本阶段推荐：

- `Sequence`：不可编辑的物理 Position；
- `BayIndex`：显式保存的业务编号数值，显示时格式化为 `-` 加数字；
- 在批准的常规柜模板中，初始 `BayIndex` 为 `1..N`，因此通常与 Position 一致；
- 非连续 `BayIndex` 仍然是合法的既有能力，不得在 Rendering 或 Layout 中自动连续化。

这避免把已经存在的 `BayIndex` 静默重解释为坐标，同时保持固定 Position 与业务编号
的清晰区分。

## 4. Interval Type 可配置模型

固定 Slot 上的间隔类型可以改变，但不会改变该 Slot 的业务编号。例如：

```text
-3 IntegratedFeederInterval
        ->
-3 PTInterval
```

PT 可以出现在任意合法 Slot，例如 `-1`、`-2`、`-3`、`-5` 或 `-7`，不得根据
“PT 通常位于某一端”添加位置限制。

类型变更必须同时满足：

- 新类型的 Domain 结构完整；
- 现有 `Sequence` 不变；
- 现有 `BayIndex` 不变；
- 同柜 `BayIndex` 唯一性不变；
- PT 全柜最多一个；
- 新结构通过 RingCabinet 的完整拓扑和开关组合校验。

## 5. PT 唯一性

PT 唯一性是 Domain 不变量，不是 Desktop 输入限制。

目标规则：

```text
RingCabinet.Intervals.Count(interval =>
    interval.IntervalKind == IntervalKind.PTInterval) <= 1
```

该规则必须覆盖：

- 新建环网柜；
- Restore；
- Interval Type Change；
- Undo/Redo 后的聚合状态。

后续实现应在 `RingCabinet.ValidateStructure` 或等价聚合校验边界统一执行，不能
只在 UI 或 Template Builder 中检查。现有混合柜多个 PT 的缺口必须作为实现阻断项
处理，不能通过 Rendering 忽略。

## 6. Stable ID 与 Business Number

Stable ID 与 Business Number 是两种不同合同：

| 概念 | 含义 | 是否因类型变更改变 |
|---|---|---|
| `IntervalId` | 间隔对象身份 | 否 |
| `Sequence` | 柜内物理 Slot | 否 |
| `BayIndex` | 显式业务编号数值 | 否 |
| `SwitchDevice.Id` | 具体开关身份 | 按结构替换策略决定 |
| Terminal/ElectricalNode ID | 拓扑对象身份 | 按结构兼容性决定 |

改变 `IntegratedFeederInterval` 为 `PTInterval` 不得因为编号格式变化而生成新的
柜体 ID、间隔 ID 或改变 `BayIndex`。

## 7. IntegratedFeeder 编号合同

以下编号来自当前冻结的一次结构合同。编号是 Domain 业务语义，Rendering 只显示
Domain 提供的编号，不根据名称、布局或 `SwitchKind` 自行推导。

| GroundingStructureKind | 主间隔/断路器 | IsolationSwitch | GroundSwitch |
|---|---:|---:|---:|
| `UpperIsolationGrounding` | `-X` | `-X-4` | `-X-47` |
| `UpperLowerGrounding` | `-X` | `-X-4` | `-X-7` |
| `LowerLowerGrounding` | `-X` | `-X-2` | `-X-7` |

这里的 `X` 是该间隔的业务编号数值，例如 `BayIndex = 3` 时主设备编号为 `-3`。
上表绑定的是一次拓扑位置：

- 上隔离上接地：GroundSwitch 位于 Isolation 与 CircuitBreaker 之间的节点；
- 上隔离下接地：GroundSwitch 位于 CircuitBreaker 下游节点；
- 下隔离下接地：CircuitBreaker 位于母线侧，Isolation 位于其下方，GroundSwitch
  位于 Isolation 下游节点。

后续 Domain 实现应提供明确的受控编号/设备角色映射，或提供足够的结构查询结果；
Rendering 不得用 `-X-47`、`-X-7` 等文本反推接地点。

## 8. PT 编号合同

PT 间隔没有 CircuitBreaker，其统一设备位置编号合同为：

| PT 结构角色 | 编号 |
|---|---:|
| PT 间隔 | `-X` |
| PT IsolationSwitch | `-X-2` |
| PT GroundSwitch | `-X-7` |

这不是“遇到 PT 就硬编码 `-2/-7`”，而是 PT Domain 结构中两个开关角色与其一次
位置的正式编号映射。后续 Domain 应向编号解析提供结构事实，Rendering 只消费结果。

PT 的 `-X` 仍来自固定间隔的 `BayIndex`，而不是由 PT 的柜内位置推导。

## 9. LoadSwitchInterval 现有规则

当前 Domain 已创建 `LoadSwitch` 和 `GroundSwitch`，并将 Cable-side 回路节点与
GroundSwitch 的设备侧 Terminal 置于同一回路节点；GroundSwitch 的业务编号合同为
`-X-7`。

当前代码、既有文档和测试没有发现足够明确的普通 LoadSwitch 主开关编号、其他设备
编号字段或可独立持久化的编号对象。因此本设计不猜测这些规则：

- 已确认的 Cable-side GroundSwitch 规则记录为 `-X-7`；
- 其他编号必须由后续专业合同补齐；
- 不得从 `DisplayName`、数组顺序、Layout 坐标或 `SwitchKind` 猜编号；
- 当前 Domain 的 `DisplayName`（例如“3号间隔负荷开关”）不能替代正式编号模型。

## 10. Type Change 语义

### 10.1 方案比较

**方案 A：原地修改同一 Interval 对象**

优点是身份连续；但当前 `RingCabinetInterval` 保存结构只读，且不同类型拥有不同
开关、Terminal、ElectricalNode 和 `SwitchAssembly`，原地修改会暴露大量不完整中间状态。

**方案 B：用新 Definition 替换旧结构**

由 Domain 聚合在一次原子操作中使用同一 `IntervalId`、`Sequence`、`BayIndex` 和
显示名构造新结构。旧结构整体退场，新结构整体通过 Domain 工厂和校验进入聚合。

### 10.2 推荐

推荐方案 B 的“聚合内原子结构替换”：

- 对外语义仍是同一个固定 Slot 上的同一个 Interval；
- `IntervalId`、`Sequence`、`BayIndex` 保持；
- 不让调用方逐个修改内部开关或节点；
- 不产生可观察的不完整拓扑状态；
- 由 Domain 统一执行 PT 唯一性、结构合法性和编号合同校验。

### 10.3 ID、拓扑与操作历史

由于 IntegratedFeeder 与 PT 的设备结构不同，推荐类型变更时：

- 保留 `IntervalId`、`Sequence`、`BayIndex` 和 Selection 对象身份；
- 旧结构中不再存在的 SwitchDevice、Terminal、ElectricalNode、SwitchAssembly
  退场；
- 新结构需要的新 SwitchDevice、Terminal、ElectricalNode、SwitchAssembly 生成新 ID；
- 不为保持表面相似而复用不兼容的旧 Terminal 或 Node ID；
- Cabinet ID、其他间隔 ID 和外部文档 ID 不变；
- Command 保存完整 Before/After 聚合快照，Undo 恢复旧结构，Redo 重用首次生成的
  After ID，不重新生成；
- Selection 在成功变更后继续指向同一 `IntervalId`，其内部设备选择若已不存在则清除
  或重新解析。

如果后续专业确认某个开关角色和 Terminal 拓扑完全兼容，可以在实现阶段单独定义
角色级 ID 保留规则；本设计不默认复用。

## 11. Business Number 存储策略

### 11.1 方案 A：显式保存 BusinessNumber

优点是兼容当前 `BayIndex` 已保存、可非连续、可与物理顺序分离的合同；类型变更和
Persistence 都能直接保持编号。

缺点是需要防止用户或多个字段同时修改 Position 与 Number。

### 11.2 方案 B：按集合顺序实时派生

优点是简单；缺点是重排、Restore 顺序或旧文件异常都可能改变业务编号，不能满足
编号稳定和固定 Slot 合同。

### 11.3 方案 C：保存稳定 Position/Slot，再派生 BusinessNumber

优点是避免重复保存；缺点是会与当前 `Sequence`、`BayIndex` 的历史语义发生冲突，
并要求一次迁移重新定义现有 `BayIndex` 文件含义。

### 11.4 推荐

推荐方案 A，直接沿用现有 `BayIndex` 作为显式业务编号数值，同时把 `Sequence` 固定
为物理 Slot。当前不新增第二个 `BusinessNumber` 字段，也不改变 V6 DTO 合同。

用户不可编辑 `BayIndex`/业务编号；类型变更只改变结构，不改变编号。未来若专业规则
确认所有柜体都严格满足 `BayIndex == Sequence`，仍应保留现有字段兼容性，不通过 Layout
或数组顺序重写历史编号。

## 12. Persistence 影响

当前 V6 已保存：

- Interval ID；
- `Sequence`；
- `BayIndex`；
- `IntervalKind`；
- SwitchDevice、Terminal、ElectricalNode、SwitchAssembly 及其 Stable ID；
- 现有拓扑字段。

因此本设计暂不要求 V7 或新增编号字段：

- 类型变更后的当前 `IntervalKind` 直接保存；
- `Sequence`、`BayIndex` 和 `IntervalId` 原样保存；
- 新结构的 ID 由 Type Change Command 生成并由 V6 DTO 保存；
- 旧 V6 文件继续按现有结构恢复；
- 旧文件若包含多个 PTInterval，严格 Domain 校验应拒绝并报告结构错误，不能静默
  选择一个或自动删除其他 PT；
- Migration 不得根据编号文本推断类型或生成缺失结构。

## 13. Inspector 与 Command 边界

未来 Inspector 可以显示：

- Interval Number（只读）；
- Interval Type；
- GroundingStructureKind（仅在业务规则允许时）；
- 当前结构对应的设备编号；
- Stable ID 作为诊断信息。

用户不可直接编辑 Interval Number，也不可编辑内部 Terminal、ElectricalNode 或
Connection。Type Change 必须经过类型化 Command，例如：

```text
ChangeRingCabinetIntervalTypeCommand
    Execute -> Domain 原子结构替换
    Undo    -> 恢复旧 Interval 结构
    Redo    -> 重用首次生成的新结构 ID
```

Command 成功后标记工程 Dirty、保持 Interval Selection，并触发 Layout/Rendering
按新 `IntervalKind` 重建；本阶段不实现该 Command 或 UI。

## 14. Rendering 边界与后续增强

当前 Rendering 使用 `GroundingStructureKind` 选择接地支路位置是合法现状，因为该枚举
代表已冻结的一次结构合同。

后续可增强为直接读取：

```text
GroundSwitch Terminal
    -> ElectricalNodeId
    -> Domain Node / topology validation
```

这样可以用真实 Terminal/Node 关系验证或驱动支路位置。该增强不属于 P1-2-A，不能在
本阶段修改 Rendering，也不能通过编号文本实现。

## 15. 推荐实施切片

1. **P1-2-B Domain Interval Numbering Contract**
   - 明确 `Sequence`、`BayIndex` 与业务编号格式；
   - 补齐任意混合柜最多一个 PT 的 Domain 校验；
   - 增加三种 IntegratedFeeder 编号映射和 LoadSwitch 已确认规则的测试。

2. **P1-2-C Interval Type Change Domain/Application**
   - 实现聚合内原子结构替换；
   - 保留 IntervalId/Sequence/BayIndex；
   - 新结构使用新设备/节点/端子 ID；
   - 支持 Undo/Redo 与失败原子性。

3. **P1-2-D Persistence and Compatibility**
   - 验证 V6 保存当前类型和 Stable ID；
   - 增加旧 V6 读取、类型变更 round-trip 和非法多 PT 拒绝测试；
   - 只有发现 DTO 无法表达新事实时才评估格式升级。

4. **P1-2-E Inspector/Command Integration**
   - 类型化 Inspector 输入；
   - Type Change Command 接入 Selection、Dirty、Undo/Redo；
   - Interval Number 保持只读。

5. **P1-2-F Rendering Contract Verification**
   - 验证任意合法 Slot 的 PT Rendering；
   - 验证编号来自 Domain 映射；
   - 后续再评估基于 Terminal/ElectricalNode 的接地支路验证。

## 16. 决策摘要

- `Sequence` 是固定物理 Position，不可编辑、不可交换；
- `BayIndex` 作为当前显式业务编号数值保存，格式化显示为 `-X`；
- Interval Type 可以在固定 Slot 上变更，编号和 IntervalId 保持；
- Type Change 推荐由 Domain 聚合执行原子结构替换，而不是修改只读 Interval 内部字段；
- 不兼容的旧 SwitchDevice、Terminal、ElectricalNode 使用新 Stable ID；
- Undo/Redo 恢复完整结构，Redo 不重新生成 ID；
- RingCabinet 最多一个 PTInterval，该约束必须在 Domain 实施；
- IntegratedFeeder、PT、LoadSwitch 编号是 Domain 业务合同，Rendering 不推导；
- 当前 V6 字段已足够表达编号和类型，暂不升级格式；
- 本阶段不修改 Domain、Persistence、Rendering、Inspector 或 Command。
