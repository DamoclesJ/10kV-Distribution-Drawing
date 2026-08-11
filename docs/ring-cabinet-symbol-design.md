# M2-D-3-A 环网柜 Symbol 渲染模型设计

> 文档状态：设计稿，仅定义渲染模型，不实现代码
> 编制日期：2026-08-11
> 依据：当前 `RingCabinet`、`RingCabinetInterval`、`SwitchAssembly`、`SymbolLibrary`，以及 `docs/ring-cabinet-design.md`、`docs/equipment-model.md`、`docs/drawing-rule.md`

## 1. 目标与边界

本阶段设计配电环网柜从 Domain、Layout 到 SymbolLibrary 和 DrawingScene 的映射。环网柜不能保存或渲染为一张固定图片，必须以 `RingCabinet.Intervals` 的有序结构为唯一组合来源。

设计保持以下边界：

- Domain 保存柜体、间隔、开关、SwitchAssembly、节点、端子和开关机械状态，不保存坐标、尺寸、颜色或 WPF 对象。
- Layout 保存柜体绝对位置、间隔相对位置、开关相对位置、标签位置和图形锚点，不保存电气状态或运行结论。
- SymbolLibrary 保存可复用且无状态的图元定义，根据一次性的渲染上下文生成 `SceneElement`。
- Rendering 读取 Domain 与 Layout，逐层组合 `RingCabinetSymbol`、`IntervalSymbol` 和 `SwitchSymbol`，最终交给现有 `DrawingSceneRenderer` 绘制。

本阶段不修改 Domain，不实现代码、拖放、属性编辑、保存、打印、自动布局、完整 PT Domain 或完整环网柜图元。

## 2. 总体映射

```text
RingCabinet
    ↓ 以 DeviceId 关联
RingCabinetLayout
    ↓
RingCabinetSymbol（复合图元）
    ├── CabinetFrame
    ├── MainBusSymbol
    ├── IntervalSymbol × N
    │       └── SwitchSymbol × M
    └── CabinetLabel / IntervalLabel / TerminalAnchor
            ↓
DrawingScene.SceneElement
            ↓
DrawingSceneRenderer
            ↓
DrawingVisual
```

`RingCabinetSymbol` 是组合协调器，不是固定几何定义。它必须按 `RingCabinet.Intervals.OrderBy(Sequence)` 逐个选择间隔图元，不能根据 `CompositionKind` 或柜体名称选择“整柜图片”。`CompositionKind` 可以用于显示说明或校验，但不能决定全部间隔的绘制类型。

## 3. RingCabinetLayout

### 3.1 柜体布局

建议新增独立的 `RingCabinetLayout`，以 `RingCabinet.Id` 为键：

| 字段 | 说明 |
| --- | --- |
| CabinetId | 对应 RingCabinet.Id，必须唯一且存在 |
| Position | 柜体左上角毫米文档坐标 |
| WidthMillimeters | 柜体总宽度，由已保存的间隔布局共同约束 |
| HeightMillimeters | 柜体总高度 |
| MainBusY | 主母线相对柜体顶部的 Y 坐标 |
| LabelOffset | 柜名相对柜体的标签偏移 |
| IntervalLayouts | 按 IntervalId 关联的有序间隔布局 |

柜体宽度不应由 `RingCabinetSymbol` 隐式改变。创建新布局时可以使用模板给出初始尺寸，但模板初始化结果必须成为明确的 Layout 数据；本阶段不设计自动布局算法。

### 3.2 间隔布局

建议统一使用 `RingCabinetIntervalLayout`，不为每种 IntervalKind 建立互不兼容的布局体系：

| 字段 | 说明 |
| --- | --- |
| IntervalId | 对应 RingCabinetInterval.IntervalId |
| RelativePosition | 相对 RingCabinetLayout.Position 的毫米坐标 |
| WidthMillimeters | 间隔宽度，允许特殊间隔与普通间隔不同宽 |
| HeightMillimeters | 间隔高度 |
| SequenceLabelOffset | 间隔序号标签偏移 |
| NameLabelOffset | DisplayName 标签偏移 |
| ExternalTerminalAnchor | ExternalTerminalId 对应的外部连接锚点 |
| SwitchLayouts | 以 SwitchDevice.Id 为键的相对位置、尺寸和标签锚点 |
| AuxiliaryAnchors | PT 等特殊间隔后续需要的非连接图形锚点 |

`SwitchLayouts` 只保存每台开关图元的相对位置和尺寸。开关种类、所属间隔、SwitchState、端子和节点关系仍来自 Domain。

### 3.3 布局校验

场景生成前至少校验：

- CabinetId 与 RingCabinet.Id 一致。
- 每个已实现的 Domain 间隔恰好有一个 IntervalLayout，且 IntervalId、顺序和父柜引用一致。
- IntervalLayout 不得引用其他柜的间隔。
- 每台需要显示的 SwitchDevice 恰好有一个 SwitchLayout。
- 外部端子锚点必须以 `ExternalTerminalId` 关联，不能按间隔名称或屏幕距离推断。
- 相邻间隔可以具有不同 IntervalKind 和宽度，但不得在柜体范围内重叠。

缺失或冲突的 Layout 应返回明确渲染错误，不由 Rendering 修改 Domain，也不静默补造电气对象。

## 4. SymbolLibrary 扩展

### 4.1 图元层级

SymbolLibrary 分为两类定义：

1. 叶子图元：现有 `ISymbolDefinition`，用于开关、线路、电缆终端、接地符号等可复用图元。
2. 复合图元：建议增加 `ICompositeSymbolDefinition<TDomain, TLayout>` 或等价接口，用于 RingCabinet 和 Interval。复合图元只能编排叶子图元和场景元素，不保存 Domain 状态。

建议预留以下注册键或定义：

| Symbol 定义 | 输入 | 责任 |
| --- | --- | --- |
| RingCabinetSymbol | RingCabinet + RingCabinetLayout | 柜体边框、共享母线、间隔编排、柜名 |
| LoadSwitchIntervalSymbol | RingCabinetInterval + IntervalLayout | 普通负荷开关间隔内部组合 |
| IntegratedFeederIntervalSymbol | RingCabinetInterval + IntervalLayout | 一二次融合间隔内部组合 |
| PTIntervalSymbol | 未来 PTInterval Domain + IntervalLayout | PT 特殊间隔组合，当前只预留 |
| SwitchSymbol | SwitchDevice + SwitchLayout + SymbolRenderContext | 单台开关及其拉开/合入变体 |
| CableTerminationSymbol | CableTermination + 对应 Layout | 电缆终端叶子图元 |

`RingCabinet`、`RingCabinetInterval` 不应直接实现 Symbol 接口，也不应引用 SymbolLibrary。

### 4.2 IntervalSymbol 选择

场景生成器按单个间隔的 `IntervalKind` 选择定义：

| IntervalKind | IntervalSymbol |
| --- | --- |
| LoadSwitchInterval | LoadSwitchIntervalSymbol |
| IntegratedFeederInterval | IntegratedFeederIntervalSymbol |
| PTInterval | PTIntervalSymbol，待 PT Domain 实现后启用 |

遇到未注册的 IntervalKind 时应停止生成该柜的正式输出并返回明确错误，不得回退为普通负荷开关间隔或空白图片。

## 5. 普通负荷开关间隔绘制

`LoadSwitchIntervalSymbol` 根据实际 Domain 对象组合以下内容：

```text
共享主母线
    │
LoadSwitchSymbol
    │
回路节点
    │
ExternalTerminalAnchor

回路/机构支路
    │
GroundSwitchSymbol
    │
接地符号
```

绘制规则：

- 主母线由 RingCabinetSymbol 统一绘制，IntervalSymbol 只绘制本间隔到主母线的接入段。
- 从 `SwitchDevices` 中按 `SwitchKind.LoadSwitch` 和 `SwitchKind.GroundSwitch` 分别取得唯一开关，不能依赖集合下标。
- 两台开关均复用 SymbolLibrary 中的 SwitchSymbol；IntervalSymbol 不复制开关几何。
- 负荷开关位于主回路，接地刀闸位于接地支路，位置由各自 SwitchLayout 给出。
- 回路节点、接地支路和外部端子锚点的连接关系必须与 Domain 的 ElectricalNode、Terminal 引用一致。
- SwitchAssembly 只用于确认组合身份和获取派生校验结果，不作为另一个可见开关绘制。
- 三工位的 Running、Disconnected、Grounded 是成员状态的派生结果，不保存为整间隔 Symbol 状态。

## 6. 一二次融合间隔绘制

`IntegratedFeederIntervalSymbol` 必须按 `GroundingStructureKind` 选择内部拓扑排布，但仍复用同一组叶子 SwitchSymbol：

### 6.1 上刀上接地

```text
共享主母线
    │
IsolationSwitchSymbol
    │──── GroundSwitchSymbol ─── 接地符号
CircuitBreakerSymbol
    │
ExternalTerminalAnchor
```

接地刀闸连接断路器上游中间节点。有效接地需要由 Domain 的状态评估结果确认，图元本身不能仅因 GroundSwitch=Closed 就显示“已有效接地”的结论。

### 6.2 上刀下接地

```text
共享主母线
    │
IsolationSwitchSymbol
    │
CircuitBreakerSymbol
    │──── GroundSwitchSymbol ─── 接地符号
ExternalTerminalAnchor
```

接地刀闸连接断路器下游回路节点。

### 6.3 下刀下接地

```text
共享主母线
    │
CircuitBreakerSymbol
    │
IsolationSwitchSymbol
    │──── GroundSwitchSymbol ─── 接地符号
ExternalTerminalAnchor
```

断路器位于上方，隔离刀闸位于下方，接地刀闸连接隔离刀闸下游回路节点。

绘制时应从 `SwitchDevices` 按 SwitchKind 查找隔离刀闸、断路器和接地刀闸，并校验各 SwitchLayout 与 Domain 设备一一对应。`OperationalState`、`IsEffectivelyGrounded` 和联锁违规均为实时派生结果，只能作为渲染上下文或校验覆盖层输入，不能写回 IntervalSymbol。

## 7. PTInterval 绘制预留

目标 PTIntervalSymbol 预计组合隔离刀闸、PT 设备、接地刀闸、母线接入段和间隔标签。PT 是环网柜内部特殊间隔，不是整个 RingCabinet 的固定属性，也不能作为覆盖在柜体左右侧的图片。

当前 Domain 的 `RingCabinetInterval` 尚未支持 `IntervalKind.PTInterval`，因此本阶段只预留 IntervalSymbol 注册接口和布局槽位：

- 不创建虚构的 PT Domain 对象。
- 不把 PT 作为普通 SwitchDevice 绘制。
- 不自行确定 PT 的端子、节点、SwitchAssembly 或状态规则。
- 待 PT Domain 结构确认并实现后，PTIntervalSymbol 必须读取实际内部设备动态组合。

## 8. 混合柜组合

混合柜仍是一个 RingCabinet，不新增 HybridRingCabinet 或 HybridCabinetSymbol。组合流程如下：

1. 校验 RingCabinetLayout 与柜体、间隔和开关布局引用。
2. 按 Sequence 对 RingCabinet.Intervals 排序。
3. 绘制柜体背景和边框。
4. 根据主母线布局绘制一条共享母线。
5. 对每个间隔独立解析 IntervalKind，并调用对应 IntervalSymbol。
6. 由每个 IntervalSymbol 复用 SwitchSymbol 绘制本间隔成员开关。
7. 最后绘制柜名、间隔序号、间隔名称和端子连接锚点。

例如 `L、L、I、I、L、L` 六间隔柜依次组合两个 LoadSwitchIntervalSymbol、两个 IntegratedFeederIntervalSymbol、两个 LoadSwitchIntervalSymbol。不得先判断柜体为 Normal、Integrated 或 Hybrid，再把所有间隔强制绘成同一种结构。

## 9. 编号与文字标注

建议采用以下来源和位置：

| 标注 | 数据来源 | 布局来源 |
| --- | --- | --- |
| 柜体名称 | RingCabinet.DisplayName | RingCabinetLayout.LabelOffset |
| 间隔序号 | RingCabinetInterval.Sequence | IntervalLayout.SequenceLabelOffset |
| 间隔名称 | RingCabinetInterval.DisplayName | IntervalLayout.NameLabelOffset |
| 开关名称 | SwitchDevice.DisplayName | SwitchLayout.LabelOffset |
| 调度编号 | SwitchDevice.DispatchNumber，存在时显示 | SwitchLayout.DispatchLabelOffset |
| 外部连接锚点 | RingCabinetInterval.ExternalTerminalId | IntervalLayout.ExternalTerminalAnchor |

文字统一生成 `SceneText`，不烧录到 SymbolDefinition 的固定图片中。缺少业务字段时不得根据间隔序号自动编造调度编号或设备双重名称；应由校验层报告缺项。

## 10. 状态传递

SymbolDefinition 必须保持无状态。每次绘制单台开关时，Rendering 状态适配器创建独立的 `SymbolRenderContext`：

```text
SwitchDevice.SwitchState
        ↓ 映射
SymbolVisualState.Open / Closed
        ↓ 每次调用临时传入
SwitchSymbol.Create(SymbolRenderContext)
        ↓
SceneElement
```

状态传递规则：

- 每个 SwitchDevice 单独创建一个 SymbolRenderContext，不能用一个“柜体状态”覆盖全部开关。
- SymbolVisualState 只决定拉开/合入的图形变体，不成为新的持久化事实。
- `Stroke`、`Fill` 等显示属性由 Rendering 状态层根据人工 ElectricalState 和 `docs/drawing-rule.md` 计算后传入，不保存到 Domain 或 SymbolDefinition。
- SwitchAssembly.Evaluate() 或 RingCabinet 的间隔评估结果可以产生 `OperationalState`、`IsEffectivelyGrounded`、`ViolatedRuleCodes`，这些结果只用于提示、标注或覆盖层。
- 非法联锁组合不得由渲染层自动改动任何 SwitchState；正式输出是否阻止由应用规则决定。
- 接地刀闸合入图形与“线路有效接地”是两个不同概念。有效接地显示必须使用 Domain 派生结果，不能由图元自行推断。

对于复合图元，建议另设一次性的 `RingCabinetRenderContext` 或 `IntervalRenderContext`，其中只保存本次渲染所需的颜色策略、选中状态、校验提示和派生评估快照；它们不进入工程文件，也不替代每台开关的 SymbolRenderContext。

## 11. 场景分层与连接锚点

推荐生成顺序：

1. 柜体背景。
2. 共享主母线和间隔内部固定导线。
3. 柜体边框、间隔边框和分隔线。
4. SwitchSymbol、PT 等设备叶子图元。
5. 柜名、间隔编号、间隔名称、设备名称和调度编号。
6. 选择框、违规提示等交互覆盖层。

外部 Connection 的端点必须解析到 `ExternalTerminalId → ExternalTerminalAnchor`。柜内固定接线仍由 ElectricalNode 表达，渲染层只把其已确认拓扑投影为线条，不创建 Connection，也不因图形相交推导导通。

## 12. 后续实现建议顺序

1. 实现 RingCabinetLayout、RingCabinetIntervalLayout 和 SwitchLayout 的最小只读结构及校验。
2. 扩展 SymbolLibrary 的复合图元注册接口。
3. 实现 LoadSwitchIntervalSymbol，并以纯负荷开关柜验证动态间隔数量。
4. 实现 IntegratedFeederIntervalSymbol，并覆盖三种 GroundingStructureKind。
5. 实现混合柜场景测试，验证不同 IntervalKind 不互相套用结构。
6. 在 PT Domain 完成后实现 PTIntervalSymbol。

## 13. 本阶段不实现

- 任何 C# 代码或项目结构调整。
- 环网柜、间隔、开关的拖放、缩放、重排和属性编辑。
- 自动计算柜体宽度、自动分配间隔位置或自动布线。
- 工程文件保存、JPG、打印。
- WorkScope、GroundingPoint。
- PTInterval Domain 和完整 PT 图元。
- 根据图元颜色推导真实带电状态或自动修改开关状态。
