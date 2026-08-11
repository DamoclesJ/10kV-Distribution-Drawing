# M2-D-3-C-A 一二次融合间隔 Symbol 渲染模型设计

> 文档状态：设计稿，仅定义渲染模型，不实现代码
> 编制日期：2026-08-11
> 依据：`docs/ring-cabinet-symbol-design.md`、`docs/ring-cabinet-design.md`、`docs/equipment-model.md`、当前 `RingCabinetInterval`、`SwitchAssembly`、`GroundingStructureKind` 和 `SymbolLibrary`

## 1. 目标与边界

本阶段设计 `IntegratedFeederIntervalSymbol`，用于在现有 `RingCabinetSymbol → IntervalSymbol → SwitchSymbol` 架构中绘制一二次融合断路器间隔。

设计必须保持：

- `RingCabinetInterval` 仍是环网柜内部聚合对象，不变成 Device。
- `GroundingStructureKind` 仍只属于单个 `IntegratedFeederInterval`，不复制到 RingCabinet、SwitchAssembly 或 Symbol。
- `SwitchAssembly` 仍表示多个 SwitchDevice 的组合关系，不绘制成一台额外设备。
- ElectricalNode 和 Terminal 的固定拓扑仍由 Domain 表达；Symbol 只把已确认拓扑投影为图形。
- `OperationalState`、`IsEffectivelyGrounded` 和联锁评估结果不保存到 Symbol 或 Layout。
- 三台开关各自通过一次性的 `SymbolRenderContext` 接收机械状态显示信息。

本阶段只设计，不修改 Domain、Rendering.Wpf 代码、Layout 结构、项目文件或电气规则。

## 2. 组合层次

```text
RingCabinet
    ↓
RingCabinetSymbol
    ↓ 按 IntervalKind 选择
IntervalSymbol
    ↓ IntervalKind=IntegratedFeederInterval
IntegratedFeederIntervalSymbol
    ├── IsolationSwitchSymbol
    ├── CircuitBreakerSymbol
    ├── GroundSwitchSymbol
    ├── CableTerminalAnchor / CableTerminationSymbol
    └── IntervalLabel
```

`IntegratedFeederIntervalSymbol` 是复合图元定义，不是 Domain 类型。它负责根据单个间隔的 `GroundingStructureKind` 编排三台开关和连接线，叶子开关图元全部复用 `SymbolLibrary` 已注册的 `SwitchSymbol` 定义。

`SwitchAssembly` 只参与成员完整性和状态评估读取，不生成单独的 AssemblySymbol；组合本身通过三个设备图元及其拓扑位置可视化。

## 3. 输入对象与布局关系

### 3.1 Domain 输入

`IntegratedFeederIntervalSymbol` 读取：

- `RingCabinetInterval.IntervalId`、`ParentCabinetId`、`Sequence`、`DisplayName`。
- `RingCabinetInterval.IntervalKind`，必须为 `IntegratedFeederInterval`。
- `RingCabinetInterval.GroundingStructureKind`，必须是三种已确认值之一。
- `RingCabinetInterval.SwitchDevices` 中唯一的 `IsolationSwitch`、`CircuitBreaker` 和 `GroundSwitch`。
- 每台 SwitchDevice 的 Terminal 引用和 `SwitchState`。
- `CircuitNodeId`、`IntermediateNodeId`、`EarthNodeId`、`ExternalTerminalId` 等拓扑身份。
- `SwitchAssembly` 的成员引用和评估接口；不把评估结果写入图元。

### 3.2 Layout 输入

继续复用 `RingCabinetIntervalLayout` 和 `RingCabinetSwitchLayout`，不为融合间隔另建一套坐标模型：

| Layout 数据 | 用途 |
| --- | --- |
| IntervalId、RelativePosition、Width、Height | 间隔外框与相对柜体位置 |
| SequenceLabelOffset、NameLabelOffset | 间隔编号和名称 |
| SwitchDeviceId | 将图形位置绑定到具体开关设备 |
| RelativePosition、Width、Height | 三台开关的相对位置与尺寸 |
| LabelOffset | 开关名称或调度编号标签位置 |
| ExternalTerminalAnchor | 间隔外部电缆连接锚点 |

布局不通过列表下标识别开关，不根据 `GroundingStructureKind` 自动覆盖用户已保存的位置。首次创建模板时可以提供三种结构的初始布局，但初始结果必须成为明确 Layout 数据。

### 3.3 Layout 校验

生成场景前必须校验：

- IntervalLayout.IntervalId 与 Domain 间隔一致。
- 间隔中恰好存在三台要求的 SwitchDevice，且三台均有 SwitchLayout。
- SwitchLayout 的设备 ID 与 Domain 成员一一对应，不允许把相邻间隔开关混入。
- 外部端子锚点使用 `ExternalTerminalId`，不使用柜体边框坐标代替。
- 三种开关图元位于间隔边界内或经过明确的引出线连接到边界，不由渲染器静默移动。
- `GroundingStructureKind` 缺失、未定义或与 Domain 拓扑不匹配时，返回明确的渲染错误。

## 4. SymbolLibrary 映射

### 4.1 叶子 Symbol

| Domain SwitchKind | SymbolLibrary SymbolKind | 说明 |
| --- | --- | --- |
| `IsolationSwitch` | `IsolationSwitch` | 隔离刀闸图元 |
| `CircuitBreaker` | `CircuitBreaker` | 断路器图元 |
| `GroundSwitch` | `GroundSwitch` | 接地刀闸图元 |

三种图元共用相同的 `SymbolRenderContext` 协议，但由 `SymbolKind` 选择具体图形定义。`SwitchAssembly` 不映射为 `SymbolKind`。

外部电缆连接有两种情况：

1. 间隔自身只显示 `ExternalTerminalId` 的端子锚点，锚点由 IntervalLayout 保存，外部 `Connection` 由其他场景对象绘制。
2. 当电缆终端作为外部 Domain 设备存在时，使用已有 `CableTerminationSymbol` 在间隔外侧或线路侧绘制；不得把 CableTermination 自动复制进融合间隔内部。

### 4.2 IntervalSymbol 注册

`IntervalSymbol` 根据 `IntervalKind` 选择：

```text
LoadSwitchInterval        → LoadSwitchIntervalSymbol
IntegratedFeederInterval  → IntegratedFeederIntervalSymbol
PTInterval                → PTIntervalSymbol（后续）
```

`IntegratedFeederIntervalSymbol` 可以实现现有 `IIntervalSymbolDefinition` 或等价复合接口。它接收 Domain 间隔、IntervalLayout、柜体相对位置和共享 SymbolLibrary，并返回 `SceneElement` 列表。

未注册的融合间隔定义不能回退成普通负荷开关间隔；应报告不支持的 IntervalKind 或结构错误。

## 5. 三种接地结构的拓扑绘制

三种结构共享主母线、外部端子和三台开关，但开关垂直顺序、接地支路连接节点不同。图形差异必须来自 `GroundingStructureKind` 和 Domain 节点拓扑，不来自柜体名称或手工状态判断。

### 5.1 UpperIsolationGrounding（上刀上接地）

```text
主母线
  │
隔离刀闸
  │──── 接地刀闸 ─── 大地节点
  │
断路器
  │
回路节点
  │
外部电缆端子
```

组合关系：

- `IsolationSwitchSymbol` 位于主母线与断路器之间的上方。
- `GroundSwitchSymbol` 从隔离刀闸与断路器之间的上游中间节点引出。
- `CircuitBreakerSymbol` 位于接地支路下方。
- `ExternalTerminalAnchor` 连接回路节点；外部电缆或电缆终端图元不改变柜内拓扑。

有效接地是 Domain 派生结论。图形只能显示接地刀闸的机械状态和接地支路，不能仅依据接地刀闸合入标记有效接地。

### 5.2 UpperLowerGrounding（上刀下接地）

```text
主母线
  │
隔离刀闸
  │
断路器
  │──── 接地刀闸 ─── 大地节点
  │
回路节点
  │
外部电缆端子
```

组合关系：

- `IsolationSwitchSymbol` 位于最上方。
- `CircuitBreakerSymbol` 位于隔离刀闸下方。
- `GroundSwitchSymbol` 从断路器下游回路节点引出。
- 接地支路的视觉位置必须与实际下游节点一致，不能复用上刀上接地的上游支路位置。

### 5.3 LowerLowerGrounding（下刀下接地）

```text
主母线
  │
断路器
  │
隔离刀闸
  │──── 接地刀闸 ─── 大地节点
  │
回路节点
  │
外部电缆端子
```

组合关系：

- `CircuitBreakerSymbol` 位于主母线下方。
- `IsolationSwitchSymbol` 位于断路器下方。
- `GroundSwitchSymbol` 从隔离刀闸下游回路节点引出。
- `ExternalTerminalAnchor` 仍对应 Domain 的 `ExternalTerminalId`，不得因开关顺序改变而更换端子身份。

## 6. 图形布局规则

### 6.1 主回路

- `RingCabinetSymbol` 统一绘制共享主母线。
- `IntegratedFeederIntervalSymbol` 只绘制本间隔从主母线到外部端子的局部回路。
- 主回路以间隔中心或 Layout 指定的设备锚点连接，不能通过两个矩形相交推断导通。
- 三台 SwitchSymbol 的位置完全由对应 `RingCabinetSwitchLayout` 提供；结构类型只决定拓扑连接语义和默认模板，不覆盖已保存的布局。

### 6.2 接地支路

- 接地支路起点必须映射到该结构定义的中间节点、回路节点或隔离刀闸下游节点。
- 接地线末端绘制接地符号或调用 SymbolLibrary 的 `GroundingLine` 定义。
- 接地支路是固定拓扑的可视化，不是工作地线 `GroundingPoint`；二者不能混用。
- 不创建额外 Connection 来表达柜内固定接线。

### 6.3 编号与名称

- 间隔序号来自 `RingCabinetInterval.Sequence`，显示位置来自 `SequenceLabelOffset`。
- 间隔名称来自 `DisplayName`，显示位置来自 `NameLabelOffset`。
- 隔离刀闸、断路器、接地刀闸名称来自各自 `SwitchDevice.DisplayName`；调度编号如存在，使用独立标签偏移。
- 不根据接地结构名称替换或简化设备双重名称。
- 缺失业务名称或调度编号时由校验层报告，不由 Symbol 编造文本。

## 7. SymbolRenderContext 状态传递

每台开关单独创建一个临时 `SymbolRenderContext`：

```text
SwitchDevice.SwitchState
        ↓
SymbolLibrary.ResolveVisualState
        ↓
SymbolRenderContext.State
        ↓
IsolationSwitchSymbol / CircuitBreakerSymbol / GroundSwitchSymbol
```

具体规则：

- `Open` 和 `Closed` 只表达单台设备的机械位置图形。
- `SymbolRenderContext` 是调用期间的渲染输入，不是保存对象；SymbolDefinition 不缓存状态。
- `OperationalState`、`IsEffectivelyGrounded`、`ViolatedRuleCodes` 不写入 Symbol、IntervalLayout 或 RingCabinetLayout。
- 若未来显示运行方式或联锁提示，应通过独立的 `IntervalRenderContext` 或覆盖层输入，不能改变三个 SwitchSymbol 的设备事实。
- 非法联锁组合不由渲染层自动纠正，也不通过图形反写 SwitchState。
- 接地刀闸显示“合入”不等于图形自动标记“有效接地”；有效接地标识必须使用 Domain 已计算结果，并且本阶段不实现该覆盖层。

## 8. 混合柜组合方式

混合柜仍由一个 `RingCabinetSymbol` 按间隔顺序组合：

```text
RingCabinetSymbol
├── LoadSwitchIntervalSymbol
├── LoadSwitchIntervalSymbol
├── IntegratedFeederIntervalSymbol
│   └── GroundingStructureKind=UpperIsolationGrounding
├── IntegratedFeederIntervalSymbol
│   └── GroundingStructureKind=LowerLowerGrounding
├── LoadSwitchIntervalSymbol
└── LoadSwitchIntervalSymbol
```

- 每个融合间隔独立读取自己的 `GroundingStructureKind`。
- 相邻普通负荷开关间隔继续使用自己的 LoadSwitchIntervalSymbol 和 SwitchAssembly。
- 融合间隔的接地结构不得影响同柜其他间隔的开关顺序、状态图形或联锁规则。
- 柜体只共享主母线和外框布局；各间隔的内部开关、接地支路、外部端子和标签仍由各自 IntervalSymbol 生成。
- RingCabinetSymbol 不根据 `CompositionKind` 选择统一柜体图片，也不把混合柜复制为新的 Domain 类型。

## 9. 与现有架构的关系

```text
RingCabinet / RingCabinetInterval / SwitchAssembly
                    ↓
RingCabinetLayout / RingCabinetIntervalLayout / RingCabinetSwitchLayout
                    ↓
RingCabinetSymbol
                    ↓
IntegratedFeederIntervalSymbol
                    ↓
SymbolLibrary.Create(SymbolKind, SymbolRenderContext)
                    ↓
DrawingScene.SceneElement
                    ↓
DrawingSceneRenderer → DrawingVisual
```

Domain 不引用 WPF；Layout 不保存电气状态；SymbolLibrary 不成为 Domain 事实源。该模型可以与现有普通负荷开关间隔实现并存，并为后续 PTIntervalSymbol 注册保留相同扩展点。

## 10. 本阶段不实现

- IntegratedFeederIntervalSymbol 的 C# 实现。
- PTInterval、DTU 和环网柜完整 PT 图元。
- OperationalState、IsEffectivelyGrounded、联锁结果的可视覆盖层。
- 自动布局、拖放、编辑、保存、JPG、打印。
- WorkScope、GroundingPoint 和工作地线业务模型。
- 根据图形状态自动推导现场带电、停电或有效接地结论。
