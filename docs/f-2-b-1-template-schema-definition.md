# F-2-B-1 Template Schema Definition

> 状态：设计阶段，不包含生产代码或测试实现。
> 稳定基线：`b5dfc3c docs: add f-2-b approved built-in templates design`。
> 目标模板：`Conventional10kVRingCabinet`。

## 1. Context and Schema Decision

F-2-B 已冻结 Approved Built-in Template 的来源、生命周期和分层边界。本阶段为第一个可实现的内置模板定义确定 schema，使后续实现不再需要猜测 Bay 数量、设备结构、Terminal、Layout 或 Symbol 映射。

`Conventional10kVRingCabinet` 冻结为：

- 10kV 常规三间隔环网柜；
- 三个同构物理 Bay；
- 三个 `LoadSwitchInterval`；
- 无二次 DTU 配置；
- 使用现有 Default LayoutRule；
- 不包含 BayFunction，也不赋予 Incoming、Outgoing、Tie 等运行分类。

“每个 Interval 的职责”在本 schema 中只表示稳定结构职责和物理排列位置，不表示电源方向、潮流方向或运行方式角色。

## 2. Template Identity

### 2.1 Identity Fields

| 字段 | 冻结值 | 说明 |
| --- | --- | --- |
| Schema name | `Conventional10kVRingCabinet` | 代码/文档中的模板定义名称 |
| TemplateId | `builtin:ring-cabinet/conventional/3-bay` | 稳定机器身份 |
| DisplayName / Template Name | `10kV 常规三间隔环网柜` | Library/UI 展示文本 |
| Schema version | `1` | 本文档定义版本，不新增 Runtime Model 属性 |
| CabinetType | `RingCabinetTemplateType.Conventional` | Application 模板分类 |
| SecondaryConfiguration | `NoSecondaryConfiguration.Instance` | 不包含 DTU |
| LayoutRule | `RingCabinetLayoutRule.Default` | `builtin:ring-cabinet/default-v1` |

### 2.2 Version Contract

当前 `RingCabinetTemplate` 没有独立 Version 属性，F-2-B-1 不为单个模板引入新的版本系统。Schema version `1` 是设计和测试合同，用于审查模板内容。

版本规则：

- 修正文案而不改变结构时，可保留 TemplateId；
- Bay 数量、IntervalKind、EquipmentConfiguration、SecondaryConfiguration 或 LayoutRule 的不兼容变化，不得悄然覆盖当前身份；
- 三、四、五、六间隔分别使用不同 TemplateId，不是同一模板的运行时参数；
- 若未来必须替换三间隔模板的专业结构，应发布新的 variant ID，并单独定义旧 ID 生命周期；
- Project Restore 不依赖 Template schema version。

### 2.3 Stable Identity Strategy

TemplateId：

- 使用小写 ASCII 和稳定 `builtin:` namespace；
- 不包含中文显示名、厂家、UI 顺序或发布日期；
- 不使用数组位置作为身份；
- 不在运行时生成 Guid；
- 不等同于 CabinetId 或任何 Domain entity ID。

模板被选择后，生成实例的 CabinetId、IntervalId、SwitchId、TerminalId、ElectricalNodeId 和 SwitchAssemblyId 仍由现有 Domain 创建链首次生成。两个 Create 调用产生不同实例 ID；同一次 Create、Undo、Redo 和 Persistence round-trip 始终保持同一组实例 ID。

## 3. Runtime Template Shape

后续实现应构造等价于以下内容的 immutable Runtime Template：

```text
RingCabinetTemplate
  TemplateId = builtin:ring-cabinet/conventional/3-bay
  Name = 10kV 常规三间隔环网柜
  CabinetType = Conventional
  LayoutRule = Default
  SecondaryConfiguration = NoSecondary
  Bays =
    [0] BayTemplate(Index = 1, LoadSwitchConfiguration)
    [1] BayTemplate(Index = 2, LoadSwitchConfiguration)
    [2] BayTemplate(Index = 3, LoadSwitchConfiguration)
```

派生 RequiredCapabilities 必须为当前模型自然计算的：

- `BasicRingCabinet`；
- `LoadSwitchBay`；
- `RingCabinetLayout`。

不得手工复制 Capability 集合，也不得加入 `DtuSecondary` 或任何 PT capability。

## 4. Interval Schema

### 4.1 Fixed Interval Set

模板包含恰好三个 Interval schema entry：

| Template order | Initial BayIndex | EquipmentConfiguration | Domain IntervalKind | 结构职责 |
| ---: | ---: | --- | --- | --- |
| 1 | 1 | `LoadSwitchConfiguration` | `LoadSwitchInterval` | 左侧物理间隔；连接共享母线与本间隔 circuit node，并提供接地和外部连接点 |
| 2 | 2 | `LoadSwitchConfiguration` | `LoadSwitchInterval` | 中间物理间隔；连接共享母线与本间隔 circuit node，并提供接地和外部连接点 |
| 3 | 3 | `LoadSwitchConfiguration` | `LoadSwitchInterval` | 右侧物理间隔；连接共享母线与本间隔 circuit node，并提供接地和外部连接点 |

三者结构职责相同，差异只有 Template order、生成后的 Sequence、初始 BayIndex 和布局位置。左/中/右仅描述物理排列，不代表 Incoming、Outgoing、Tie、Reserve 或 Metering。

### 4.2 Mapping to Existing Domain

Application Domain Builder 按 `Bays` 集合顺序映射：

```text
Template order 1 → Domain Sequence 1, BayIndex 1
Template order 2 → Domain Sequence 2, BayIndex 2
Template order 3 → Domain Sequence 3, BayIndex 3
```

每个 `LoadSwitchConfiguration` 映射为：

```text
RingCabinetIntervalDefinition.CreateLoadSwitch(
  bayIndex,
  initialLoadSwitchState = Open,
  initialGroundSwitchState = Open)
```

随后 `RingCabinet.Create` 为每个 Interval 创建：

- 一个 `SwitchKind.LoadSwitch`；
- 一个 `SwitchKind.GroundSwitch`；
- 一个 `SwitchAssemblyType.LoadSwitchThreePosition`；
- 一个独立 Circuit node；
- 一个独立 Earth node；
- 一个 external terminal；
- 两个开关各自的内部 terminals。

三个 Interval 共享 Cabinet MainBus node，但不共享各自的 Circuit node、Earth node、external terminal 或 SwitchAssembly。

### 4.3 Sequence and BayIndex

- Sequence 是 Domain 根据模板集合顺序生成的物理排列序号；
- BayIndex 是模板提供的实例初始业务编号；
- 本模板初始二者均为 `1, 2, 3`，但语义仍独立；
- Builder 不按 BayIndex 排序；
- 未来 BayIndex 编辑不得改变 Sequence；
- F-2-B-1 不设计 BayIndex editing command。

## 5. Terminal Schema

### 5.1 Terminal Types per Interval

每个 LoadSwitch Interval 的 Terminal schema 为：

| Terminal | Owner | ElectricalNode | External | Connection rule |
| --- | --- | --- | --- | --- |
| LoadSwitch bus-side | LoadSwitch device | shared MainBus node | No | 柜内固定连接，不接受外部连接 |
| LoadSwitch circuit-side | LoadSwitch device | interval Circuit node | No | 柜内固定连接，不接受外部连接 |
| GroundSwitch device-side | GroundSwitch device | interval Circuit node | No | 柜内固定连接，不接受外部连接 |
| GroundSwitch ground-side | GroundSwitch device | interval Earth node | No | 柜内固定接地连接，不接受外部连接 |
| Interval external terminal | Interval aggregate | interval Circuit node | Yes | 允许 Cable 或 OverheadLine；不允许多重连接 |

Template 本身不包含这些 Terminal objects 或 IDs。表中规则是现有 Domain Aggregate 的生成合同，不应复制为 Application Template 字段。

### 5.2 Connection Rules

- MainBus node 连接三个 LoadSwitch 的 bus-side terminals；
- 每个 Interval 的 Circuit node 连接本间隔 LoadSwitch circuit-side、GroundSwitch device-side 和 external terminal；
- 每个 Interval 的 Earth node 只承载本间隔 GroundSwitch ground-side terminal；
- internal terminal 的 `AllowedConnectionTypes` 为空；
- external terminal 允许 `ConnectionType.Cable` 与 `ConnectionType.OverheadLine`；
- external terminal 的 `AllowsMultipleConnections` 为 `false`；
- 模板实例创建后不自动生成 Cable、OverheadLine 或 Connection；
- 外部连接只能由现有连接命令另行创建。

### 5.3 ElectricalNode Ownership

- MainBus node 由 RingCabinet device 拥有；
- Circuit/Earth nodes 由各自 Interval aggregate 拥有；
- Terminal 的 ElectricalNodeId 必须指向对应节点；
- Restore 时由 V4 中保存的 Stable IDs 重建相同关系；
- TemplateId 和 schema version 不参与节点或 terminal identity。

## 6. Layout Schema

### 6.1 Default Arrangement

三个 Interval 按 Domain Sequence 从左到右排列：

```text
[Sequence 1 / BayIndex 1]
        [Sequence 2 / BayIndex 2]
                [Sequence 3 / BayIndex 3]
```

布局只使用 `RingCabinetLayoutRule.Default`。Position 是整个 Cabinet layout 的文档坐标原点，由创建请求提供。

### 6.2 Geometry Rules

F-2-B-1 复用当前 `RingCabinetLayoutFactory` 的 Default-v1 几何合同：

| 几何项 | 值 |
| --- | ---: |
| Cabinet padding | 10 mm |
| Interval gap | 5 mm |
| Interval width | 60 mm |
| Interval height | 125 mm |
| Cabinet height | 145 mm |
| Main bus Y | 25 mm |
| Switch width | 16 mm |
| Switch height | 10 mm |

三间隔 Cabinet width：

```text
2 × 10 + 3 × 60 + 2 × 5 = 210 mm
```

这些数值属于 `builtin:ring-cabinet/default-v1` 的 Rendering 实现，不复制到 Built-in Template object。文档记录它们是为了冻结 schema 与当前实现的映射。

### 6.3 Coordinate Generation

以 Cabinet `Position = (X, Y)` 为基准：

```text
intervalX(sequence) = 10 + (sequence - 1) × (60 + 5)
intervalY = 10
```

因此三个 Interval 相对 Cabinet 原点的位置为：

| Sequence | Relative X | Relative Y |
| ---: | ---: | ---: |
| 1 | 10 mm | 10 mm |
| 2 | 75 mm | 10 mm |
| 3 | 140 mm | 10 mm |

LoadSwitch Interval 内部 switch 相对位置：

| SwitchKind | Relative X | Relative Y |
| --- | ---: | ---: |
| LoadSwitch | 23 mm | 35 mm |
| GroundSwitch | 23 mm | 72 mm |

实际文档坐标由 Cabinet Position、Interval relative position 和 switch relative position相加得到。Layout Builder 必须使用 Sequence，不得使用 BayIndex 计算水平位置。

## 7. Symbol Mapping

### 7.1 Cabinet and Interval Mapping

```text
RingCabinet
  → RingCabinetSymbol
  → SymbolKind.RingCabinet

IntervalKind.LoadSwitchInterval
  → IntervalSymbol dispatcher
  → LoadSwitchIntervalSymbol
  → SymbolKind.RingCabinetInterval
```

每个 Interval 的开关映射为：

```text
SwitchKind.LoadSwitch
  → SymbolLibrary.ResolveSwitchKind
  → load-switch visual symbol

SwitchKind.GroundSwitch
  → SymbolLibrary.ResolveSwitchKind
  → grounding-switch visual symbol
```

开关的开/合视觉状态由 `SymbolLibrary.ResolveVisualState` 根据 Domain SwitchState 决定。模板只给出初始 Open state 所需的结构配置，不保存视觉状态枚举。

### 7.2 Rendering.Wpf Responsibility Boundary

Rendering.Wpf 负责：

- Layout geometry；
- SymbolKind 解析；
- 颜色、线宽、文字和状态视觉；
- SceneElement 创建；
- HitTest 数据；
- Domain/Layout identity 检查。

Application Built-in Template 不保存：

- WPF 类型；
- SymbolKind；
- SceneElement；
- geometry constants；
- HitTest shape；
- switch visual state；
- Terminal visual coordinates。

因此更换 Renderer 不需要修改模板 schema；改变 Default-v1 几何语义则必须通过明确 LayoutRule 版本处理。

## 8. Creation Parameters

### 8.1 User Inputs

用户创建此模板实例时只需要提供：

| 参数 | 类型 | 必填 | 默认值 | 说明 |
| --- | --- | --- | --- | --- |
| Template selection | `TemplateId` selection | Yes | UI 可预选首项，但不能隐式替换未知 ID | 从 Library 取得固定模板实例 |
| DisplayName | `string` | Yes | 无业务默认值 | 新 RingCabinet 实例名称，必须非空 |
| Position | `DocumentPoint` | Yes | 由画布点击或 placement context 提供 | Cabinet layout 文档坐标 |

`RingCabinetTemplateBuildRequest` 继续实际携带 Template、DisplayName 和 Position；Library lookup 在构造 Request 之前完成。

### 8.2 Schema Defaults

以下是模板/Builder 固定默认值，不是用户输入：

- CabinetType = Conventional；
- Bay count = 3；
- BayIndex = 1, 2, 3；
- EquipmentConfiguration = LoadSwitchConfiguration for all bays；
- LoadSwitch state = Open；
- GroundSwitch state = Open；
- SecondaryConfiguration = NoSecondary；
- LayoutRule = Default-v1。

用户不能在此模板创建对话中改变 Bay 数量、IntervalKind、开关组合、Terminal 拓扑或 LayoutRule。需要不同结构时应选择另一个 Approved Template，而不是修改该模板定义。

## 9. Creation Pipeline

```text
Select builtin:ring-cabinet/conventional/3-bay
        |
        v
Library returns Conventional10kVRingCabinet template
        |
        v
BuildRequest(Template, DisplayName, Position)
        |
        v
Domain Builder creates Definition + Aggregate once
        |
        v
Layout Builder creates RuntimeLayout once
        |
        v
Full BuildResult
        |
        v
AddRingCabinetCommand / CommandStack
        |
        v
Selection / Scene / Undo / Redo / Dirty
```

Builder、Layout Builder 和 Command 的既有 API 不因 Built-in schema 改变。

## 10. Persistence Considerations

### 10.1 V4 Save Contract

Project FormatVersion 4 保存生成后的工程事实：

- RingCabinet identity 和 display name；
- MainBus node；
- 三个 Interval 的 Sequence、BayIndex 和 `IntervalKind.LoadSwitchInterval`；
- SwitchDevices 及 states；
- SwitchAssemblies；
- Terminals、AllowedConnectionTypes 和 ElectricalNode relationships；
- Runtime RingCabinetLayout、IntervalLayouts 和 SwitchLayouts；
- 其他现有 V4 必要结构字段。

V4 不保存 BayFunction，也不要求保存 TemplateId、Template DisplayName 或 schema version。工程打开时不查询 TemplateLibrary，不从模板重新 Build。

### 10.2 Stable ID Strategy

- Domain Builder 每次首次 Build 为新实例生成 Stable IDs；
- Layout 使用同一次 Build 的 Cabinet/Interval/Switch IDs；
- AddRingCabinetCommand 保存第一次 Build 的 Domain 和 Layout 对象；
- Undo 移除同一对象；
- Redo 重新加入同一对象，不重新 Build；
- V4 save/reload 恢复原 CabinetId、MainBusNodeId、IntervalId、SwitchId、TerminalId、ElectricalNodeId 和 SwitchAssemblyId；
- Migration 和 Restore 不生成替代 ID；
- TemplateId 永远不作为 Domain Stable ID seed。

### 10.3 Future Creation Metadata

若未来需要审计实例来源，可单独设计 optional `CreationMetadata.TemplateId`。该信息只能用于显示或审计，不能成为 Project Restore、Rendering、Undo/Redo 或 Domain topology 的必要输入。本阶段不修改 V4 schema。

## 11. Validation and Implementation Tests

后续实现应至少验证：

1. TemplateId、Name、CabinetType、LayoutRule 和 SecondaryConfiguration 精确匹配本文；
2. 模板包含恰好三个 Bays，Index 为 1、2、3；
3. 三个 Bays 均使用 `LoadSwitchConfiguration`；
4. RequiredCapabilities 精确包含 Basic、LoadSwitch、Layout；
5. Domain Build 成功并产生 Sequence 1、2、3；
6. 每个 Interval 的 BayIndex、IntervalKind、SwitchKinds 和 Terminal topology 正确；
7. Layout width 为 210 mm，Interval positions 为 `(10,10)`、`(75,10)`、`(140,10)`；
8. Cabinet/Layout、Interval/Layout 和 Switch/Layout IDs 一致；
9. Rendering 产生 Cabinet、三个 interval 及对应 switch symbols；
10. Command execute、Undo、Redo 保持同一 Stable IDs；
11. V4 round-trip 保持所有 Stable IDs 与 topology；
12. 保存 JSON 不包含 Function，也不依赖 TemplateId；
13. 两次 Create 产生不同 Cabinet instances；
14. Library 返回同一个 immutable Template instance。

## 12. Explicit Boundaries

本 schema 明确：

- 不重新引入 `BayFunction`；
- 不引入 Direction、Role、Purpose、Incoming、Outgoing 或 Tie 替代字段；
- 不通过 Sequence、BayIndex 或左右位置推断运行分类；
- 不引入自由拓扑生成；
- 不允许用户改变模板内部结构；
- 不设计用户模板编辑器；
- 不实现 PT、DTU 或 IntegratedFeeder；
- 不自动创建 Cable 或 OverheadLine；
- 不修改 Project FormatVersion 4；
- 不让 Persistence 或 Redo 重新执行 Template Build。

## 13. Final Schema Decision

`Conventional10kVRingCabinet` schema version 1 是一个固定三物理间隔的 Approved Built-in Template：

```text
TemplateId: builtin:ring-cabinet/conventional/3-bay
Name: 10kV 常规三间隔环网柜
CabinetType: Conventional
Bays: 3 × LoadSwitchConfiguration
Initial BayIndex: 1, 2, 3
Secondary: None
Layout: builtin:ring-cabinet/default-v1
```

三个 Interval 是结构同构的 LoadSwitch intervals，只按物理 Sequence 和初始 BayIndex 区分。Terminal、ElectricalNode、Stable ID、Layout geometry 和 Symbol 映射分别由现有 Domain 与 Rendering.Wpf 负责，Template 不复制这些事实源。

该 schema 可以直接进入后续 Built-in Template Application 实现切片，无需修改 Domain、Persistence、Builder、Coordinator 或 Command 架构。
