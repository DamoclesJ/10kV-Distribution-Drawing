# P0-7-E-1 Template Builder Runtime Design

> 状态：Template Builder Runtime 架构设计；不包含 Runtime Model、Builder、Template Library 或 UI 的生产实现。<br>
> 稳定基线：tag `v0.7-d4`，commit `8fe249a7d144e7fa0b7d772c7a5a63a7aaeaa701`。<br>
> 设计依据：`template-system-design.md`、`template-builder-design.md`、`template-runtime-model-design.md` 以及 P0-7-D 已冻结的 Bay Metadata 和 Persistence Version 3 边界。

## 1. Template Builder 总体架构

Template Builder Runtime 位于模板描述和现有生产创建链路之间：

```text
RingCabinetTemplate
        |
        v
Template Builder
        |
        v
RingCabinetDefinition
        |
        v
RingCabinet.Create
        |
        v
RingCabinet Domain Aggregate
        |
        v
RingCabinetLayoutFactory
        |
        v
RingCabinetLayout
        |
        v
Existing Rendering
```

Builder 的核心职责是把不可变 Template Runtime Model 转换为完整、无歧义的 Domain 创建输入，并协调现有 Domain Factory 与 Layout Factory 产生一次创建所需的结果。

Builder 不负责：

- DrawingScene、SceneElement 或 Symbol；
- Rendering；
- Selection、SelectionTransition 或 Inspector；
- Command、CommandStack、Undo/Redo 或 Dirty；
- 把创建结果加入 DrawingDocument 或 RuntimeLayoutDocument；
- Persistence、FormatVersion 或模板来源解析。

Builder 不手工复制 RingCabinet 内部拓扑规则。间隔、Switch、Terminal、ElectricalNode 与 SwitchAssembly 的合法结构仍由 `RingCabinet.Create` 建立和校验；布局仍由针对已创建聚合的 `RingCabinetLayoutFactory` 生成。

## 2. Template Runtime Model

### 2.1 RingCabinetTemplate

建议的第一版不可变模型为：

```text
RingCabinetTemplate
├── TemplateId
├── Name
├── CabinetType
├── Bays[]
├── LayoutRule
├── SecondaryConfiguration
└── RequiredCapabilities (derived)
```

字段职责：

| 字段 | 职责 |
| --- | --- |
| `TemplateId` | 模板目录中的逻辑身份，用于选择、诊断和版本管理 |
| `Name` | 面向用户的模板名称，不自动成为设备实例名称 |
| `CabinetType` | 模板分类与策略选择提示，不替代 Bays 的实际结构 |
| `Bays` | 有序的 Bay 描述；集合顺序决定物理 Sequence |
| `LayoutRule` | 引用或包含受控的初始布局规则 |
| `SecondaryConfiguration` | 预留二次配置描述；第一版不生成 DTU |
| `RequiredCapabilities` | 从 Bays、布局和二次配置派生的能力需求 |

模板集合与 Bays 集合在构造后不可变，并对输入集合进行防御性复制。

### 2.2 TemplateId 边界

`TemplateId` 不是 Domain ID，也不是某次创建操作的实例 ID。它：

- 不映射为 RingCabinetId；
- 不参与 IntervalId、SwitchId、TerminalId、ElectricalNodeId 或 SwitchAssemblyId 的生成；
- 第一版不写入工程 Persistence；
- 不允许作为确定性 GUID 的种子。

同一个模板被使用两次，必须生成两个彼此独立的 Domain 聚合。

### 2.3 不应进入 Runtime Model 的信息

RingCabinetTemplate 不保存：

- Domain Stable ID；
- Domain Object、DTO 或 ProjectRuntimeSession 引用；
- 实例 Placement Position；
- 最终 Bay、Switch 或 Label 坐标；
- DrawingScene、SelectionReference 或 Command；
- 用户确认的现场 SwitchState。

实例 DisplayName 和文档放置位置属于一次创建请求，而不是可复用模板定义。

## 3. BayTemplate 设计

第一版 Bay 模型保持已冻结的三字段结构：

```text
BayTemplate
├── Index
├── Function
└── EquipmentConfiguration
```

### 3.1 Index 与 Sequence

`Index` 映射到 `RingCabinetInterval.BayIndex`，表示现场业务编号。它必须为正整数，在同一模板内唯一，可以不连续。

```text
Index = 5
显示投影 = 负5间隔
```

禁止使用 `Index = -5` 表示“负5间隔”。

BayTemplate 不重复保存 Sequence。`RingCabinetTemplate.Bays` 的集合顺序决定物理顺序，Builder 按数组顺序构造 `RingCabinetIntervalDefinition`；当前 `RingCabinet.Create` 按定义顺序生成从 1 开始且连续的 `RingCabinetInterval.Sequence`。

Builder 不按 Index 排序，不以 Sequence 覆盖 Index，也不自动补齐 Index 缺号。

### 3.2 Function

`Function` 直接映射到 `RingCabinetInterval.Function`。第一版使用现有 Domain `BayFunction`：

- `Incoming`；
- `Outgoing`；
- `Tie`；
- `Metering`；
- `Reserve`。

`Unknown` 只用于旧工程迁移后的明确未知状态，新模板必须拒绝。`PT` 是已知的业务功能，但第一阶段 Builder 因缺少 PT 生产能力而返回 `UnsupportedCapability`。

Function 表示电气用途，不表示设备类型。Builder 不从 DisplayName、Index、IntervalKind 或设备结构猜测 Function。

### 3.3 EquipmentConfiguration

EquipmentConfiguration 只描述模板所需的受控设备组合。它不会作为重复字段进入 Domain；生成后真实的 IntervalKind、GroundingStructureKind、Switch、Terminal、ElectricalNode 与 SwitchAssembly 是事实源。

## 4. 普通环网柜模板

普通环网柜的模板间隔数量由 `Bays.Count` 决定，Runtime Model 本身不写死 4 间隔或 6 间隔。设计目标可表达 2、3、4、5、6 间隔以及未来经 Domain 允许的其他数量。

五间隔示例：

| Sequence | Bay Index | Function | EquipmentConfiguration |
| --- | --- | --- | --- |
| 1 | 1 | Incoming | LoadSwitch |
| 2 | 2 | Outgoing | LoadSwitch |
| 3 | 3 | Outgoing | LoadSwitch |
| 4 | 4 | Outgoing | LoadSwitch |
| 5 | 5 | Tie | LoadSwitch |

Builder 按表中顺序建立 Interval Definition；Bay Index 和 Function 原样传入 Domain 创建 API。

### 4.1 当前 Domain 数量边界

在稳定基线中，纯 LoadSwitch RingCabinet 的现有 Domain 规则只允许 3、4、5、6 间隔。因而：

- Runtime Model 可以表达 2 间隔模板；
- 第一阶段 Builder 不得绕过或复制 Domain 规则来强行生成 2 间隔聚合；
- 在 Domain 业务规则未另行确认和修改前，2 间隔 Build 必须返回明确的 Domain validation/capability failure；
- 后续若专业规则确认纯 LoadSwitch 两间隔合法，应独立修改 Domain 和测试，而不是在 Builder 中添加特例。

该差异是 E-2 实现前需要处理或明确接受的能力边界。

## 5. 一二次融合环网柜模板

一二次融合模板使用显式 `CabinetType = PrimarySecondaryIntegrated` 分类，并让每个 Bay 显式选择 `IntegratedFeederEquipmentConfiguration`。第一版目标实例为 4 间隔和 6 间隔，但数量仍由 Bays 集合决定，Builder 不写死分支或默认比例。

当前 Domain 对纯 IntegratedFeeder 柜的合法数量为 4 或 6。Builder 先做模板结构和能力预检，最终合法性仍以 Domain 创建 API 为准。

`CabinetType` 不能代替 EquipmentConfiguration：

- Builder 不因 CabinetType 自动把所有 Bay 改成 IntegratedFeeder；
- 实际间隔结构由每个 Bay 的 EquipmentConfiguration 决定；
- CabinetType 只用于模板分类、能力声明和布局策略选择；
- 混合柜必须通过显式 Bays 表达，不能根据名称或数量推断。

第一阶段不把 PT 或 DTU 塞入一二次融合生产模型。

## 6. EquipmentConfiguration 设计

第一版使用封闭、强类型的配置变体，不允许自由字符串设备类型：

```text
BayEquipmentConfiguration
├── LoadSwitchEquipmentConfiguration
└── IntegratedFeederEquipmentConfiguration
```

### 6.1 LoadSwitchEquipmentConfiguration

该配置映射到：

```text
RingCabinetIntervalDefinition.CreateLoadSwitch(
    bayIndex,
    function,
    initialLoadSwitchState,
    initialGroundSwitchState,
    displayName)
```

它表达 LoadSwitch + GroundSwitch 结构需求，但不包含 Domain Object 或 Stable ID。

### 6.2 IntegratedFeederEquipmentConfiguration

该配置至少包含强类型 `GroundingStructureKind`，并映射到：

```text
RingCabinetIntervalDefinition.CreateIntegratedFeeder(
    bayIndex,
    function,
    groundingStructureKind,
    initialIsolationSwitchState,
    initialCircuitBreakerState,
    initialGroundSwitchState,
    displayName)
```

GroundingStructureKind 必须是现有 Domain 已支持的三种合法值之一，Builder 原样传递，不从 Function 或 CabinetType 推断。

Domain 当前创建需要合法技术初始化状态。第一版可以由创建策略统一提供 `Open`，但该值不是模板用户确认的现场运行状态，也不应扩展成完整 Switch 操作配置。

## 7. PT 边界

`BayFunction.PT` 已是 Domain 枚举值，但当前创建 API明确拒绝把 PT Function 与 LoadSwitchInterval 或 IntegratedFeederInterval 组合。

第一阶段 Builder 遇到 PT Bay 时必须在创建任何 Domain 对象或 RuntimeLayout 前返回：

```text
UnsupportedCapability(PTBay)
```

原因是当前缺少完整的：

- PT Domain；
- PT Layout；
- PT Rendering；
- PT Persistence。

不得用 IntegratedFeeder、LoadSwitch 或 CableTermination 模拟 PT，不得生成伪 PT Terminal，也不得留下部分创建结果。

## 8. DTU 与 SecondaryConfiguration 边界

`SecondaryConfiguration` 是模板中的预留扩展位置，用于未来描述 DTU 等二次设备需求。DTU：

- 不属于一次 Bay；
- 不参与 Primary topology；
- 不创建一次 Connection、Terminal 或 ElectricalNode；
- 不与 PT 处于同一建模层级。

第一阶段 Builder 对非空 DTU 配置返回 `UnsupportedCapability(DTU)`。仅保留字段不等于已支持生成；不得只创建无 Domain 事实的图形占位。

## 9. Builder 输入、处理与输出

### 9.1 一次 Build 请求

模板本身不包含设备实例信息，因此 Builder 还需要最小 Creation Context：

```text
RingCabinetTemplateBuildRequest
├── Template
├── CabinetDisplayName
└── PlacementPosition
```

PlacementPosition 只用于创建本次 RingCabinetLayout，不写回 Template。DrawingDocument、CommandStack、SelectionManager、Scene 或 ProjectRuntimeSession 不进入 Builder 输入。

### 9.2 Builder 分阶段职责

Builder 内部推荐保持三个清晰阶段：

1. 校验 Template 结构和 RequiredCapabilities；
2. 将 Bays 映射为 `RingCabinetIntervalDefinition[]`，再形成 `RingCabinetDefinition`；
3. 调用 `RingCabinet.Create` 得到完整聚合，再调用 `RingCabinetLayoutFactory` 得到初始布局。

Builder 负责协调，不复制 Domain 内部拓扑构造，也不复制 LayoutFactory 的几何实现。

### 9.3 Build Result

成功结果建议至少包含：

```text
RingCabinetTemplateBuildResult
├── Definition
├── Cabinet
├── Layout
└── RequiredCapabilities
```

- `Definition` 是本次模板映射形成的完整 Domain 创建输入，可用于诊断和测试；
- `Cabinet` 是已创建、已验证的 RingCabinet 聚合；
- `Layout` 是与 Cabinet Stable ID 完全匹配的 RingCabinetLayout；
- `RequiredCapabilities` 是本次实际消费的能力集合。

不能只返回 `RingCabinetDefinition + RingCabinetLayout`，因为当前 LayoutFactory 必须检查真实 RingCabinet 的 Interval、Switch 和 GroundingStructureKind，并绑定实际 SwitchId。Command 也需要真实 Cabinet 聚合。Builder 不返回 DrawingScene，也不直接把结果写入工程。

失败应返回明确类别，例如：

- `InvalidTemplate`；
- `UnsupportedCapability`；
- `DomainValidationFailed`；
- `LayoutGenerationFailed`。

失败结果不得暴露可被加入工程的半聚合或半布局。

## 10. Command、Undo 与 Selection 集成边界

模板创建最终复用现有原子命令边界：

```text
Template
   |
   v
Builder
   |
   v
BuildResult(Cabinet + Layout)
   |
   v
AddRingCabinetCommand
   |
   v
CommandStack
```

Builder 不执行 Command。Desktop/Application 创建协调层在 Build 成功后，用 BuildResult 中同一 Cabinet 和 Layout 构造现有 `AddRingCabinetCommand` 并执行。

Undo 删除整个 Cabinet 聚合及其 RingCabinetLayout。Redo 恢复首次 Build 得到的同一 Cabinet 和 Layout 对象；禁止在 Redo 时重新运行 Builder。

模板创建后的 Selection 由 Desktop 层决定。推荐与现有 RingCabinet 创建一致，成功刷新 Scene 后选择整个 RingCabinet，并由 `SelectionTransition.ForAdd` 记录执行前后 Selection。Selection 不进入 BuildResult，也不进入 Template。

## 11. Stable ID 策略

Stable ID 只在首次 Build 中生成一次。

当前稳定基线的实际职责为：

- Builder/创建协调逻辑为 RingCabinetDefinition 提供新的 CabinetId；
- `RingCabinet.Create` 为 Interval、Switch、Terminal、ElectricalNode 和 SwitchAssembly 创建内部 Stable ID；
- `RingCabinetLayoutFactory` 使用已生成的 CabinetId、IntervalId 和 SwitchId 建立布局引用。

因此第一版不应声称 Builder 直接分配所有内部 ID，也不应为此改写 Domain Factory。若未来要求可注入、可预测的 ID Generator，必须作为独立 Domain API 设计审查，不属于 E-1。

满足 Stable ID 的关键不是确定性重建，而是：

1. Build 只执行一次；
2. BuildResult 保存首次创建的完整聚合和布局；
3. Add Command 保存该结果；
4. Undo/Redo 始终移除和恢复同一对象图；
5. Save/Load 使用现有 Persistence Version 3 保存和恢复这些 ID。

TemplateId 不参与任何 Stable ID 生成。

## 12. LayoutRule 边界

LayoutRule 只描述初始布局策略，例如：

- 默认 Bay 宽度；
- Bay 间距；
- 不同 EquipmentConfiguration 的布局策略键；
- 未来 PT 特殊宽度；
- 未来 DTU 左右位置规则。

它不保存某个实例的最终坐标，例如 `Bay1.X = 100`，也不保存 IntervalId、SwitchId 或 SceneElement。

Builder 将 Template 的规则与本次 PlacementPosition 交给受控的 Layout 生成边界，最终产生 `RingCabinetLayout`。RingCabinet 是独立设备，不生成 `AttachmentLayout`；AttachmentLayout 继续只用于 PoleAttachment。

第一版可先使用现有 `RingCabinetLayoutFactory` 的确定性策略。若要让模板的 LayoutRule 真正改变尺寸或间距，应扩展 LayoutFactory 的规则输入，而不是在 Builder 中复制几何公式。

## 13. 第一版明确不支持

P0-7-E 第一版明确不支持：

- PT 模板的真实生成；
- DTU 自动生成；
- JSON 模板加载、Schema 或热更新；
- 厂家差异模板；
- 自动电气计算或网络拓扑分析；
- 根据名称、设备结构或排列位置推断 BayIndex、BayFunction；
- 自动专业命名和自动编号；
- 模板实例与 Domain 的持久化 TemplateReference；
- 运行时修改已创建柜体结构；
- Builder 直接生成 DrawingScene。

## 14. P0-7-E 后续实施阶段

### P0-7-E-1：Runtime Design

冻结本文的 Runtime Model、Builder 输入输出、能力失败与架构边界。只形成设计，不实现生产代码。

### P0-7-E-2：Runtime Model 与 Builder Implementation

目标：

- 实现不可变 RingCabinetTemplate、BayTemplate 和强类型 EquipmentConfiguration；
- 实现能力预检；
- 映射完整 BayIndex、BayFunction 与 GroundingStructureKind；
- 复用 RingCabinetDefinition、RingCabinet.Create 和 RingCabinetLayoutFactory；
- 补普通、IntegratedFeeder、mixed、UnsupportedCapability 与 Stable ID 测试。

进入实现前必须决定如何处理“模板可表达两间隔、现有纯 LoadSwitch Domain 只允许 3–6 间隔”的差异。未经专业确认不得自行放宽 Domain。

### P0-7-E-3：Built-in Template Library

目标：以 C# 内建不可变对象提供一组已确认模板，包括不同数量的普通柜和一二次融合柜。模板库只提供描述，不执行 Build，不引入 JSON。

### P0-7-E-4：Desktop Template UI

目标：提供模板选择和实例 DisplayName/Placement 输入，经 Builder 产生 BuildResult，再进入现有 AddRingCabinetCommand、CommandStack、Scene 刷新与 SelectionTransition 流程。

UI 不直接创建 Domain、Layout 或 Command 内部对象。

## 15. 设计结论与进入实现条件

P0-7-E-1 推荐采用以下结论：

- Template Runtime Model 是不可变描述，不是工程状态；
- Bays 集合顺序产生 Sequence，BayTemplate.Index 原样映射 BayIndex；
- BayFunction 原样映射 Domain，Unknown 和 PT 在新建路径被拒绝；
- 第一版 EquipmentConfiguration 只支持 LoadSwitch 与 IntegratedFeeder 强类型变体；
- Builder 通过现有 Domain 和 Layout Factory 创建聚合与布局，不复制专业事实或几何实现；
- BuildResult 必须同时包含实际 RingCabinet 和 RingCabinetLayout；
- Command 保存首次 BuildResult，Redo 不重新 Build；
- TemplateId 不参与 Stable ID；
- PT、DTU 和 JSON 明确留在后续能力阶段。

进入 E-2 前还需确认一个专业边界：是否正式放宽纯 LoadSwitch RingCabinet 的 Domain 数量规则以支持两间隔。若不放宽，Builder 必须对两间隔模板返回明确失败，不能通过 Runtime 层绕过 Domain。
