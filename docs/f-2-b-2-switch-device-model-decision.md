# F-2-B-2 Switch Device Model Decision

> 状态：架构决策记录；本阶段不修改生产代码、测试、持久化格式或模板实现。
> 基线：`6149d5e feat: add conventional built-in ring cabinet template`。
> 目标：为具备基础电气拓扑识别能力的 10kV 工作票绘图工具冻结第一阶段开关设备模型。

## 1. Context

当前项目需要理解：

- 设备及端子的连接关系；
- 单台开关设备的分、合状态；
- 开关两端在分、合状态下的基础导通关系；
- 柜内组合的已确认联锁；
- 工作票绘图所需的基础停送电操作顺序。

项目不是完整电网仿真、继电保护或 SCADA 系统。本决策只冻结能够支持当前绘图、拓扑识别和基础操作校验的最小模型，不引入潮流、保护定值、遥测遥信或运行方式计算。

## 2. Decision Summary

第一阶段继续使用统一的 `SwitchDevice` 模型，并用 `SwitchKind` 保留以下设备类型区别：

| 业务名称 | 当前 Domain `SwitchKind` | 第一阶段状态模型 |
| --- | --- | --- |
| Breaker / 断路器 | `CircuitBreaker` | `Open` / `Closed` |
| LoadSwitch / 负荷开关 | `LoadSwitch` | `Open` / `Closed` |
| Disconnector / 隔离刀闸 | `IsolationSwitch` | `Open` / `Closed` |
| Fuse / 跌落式熔断器 | `DropoutFuse` | `Open` / `Closed` |

本文使用业务术语 Breaker、LoadSwitch、Disconnector、Fuse；代码继续遵循当前 enum 命名。本阶段不进行无价值的重命名。

杆塔侧对象关系冻结为：

```text
Pole = 主体设备
Attachment = 杆塔拥有的附属能力/安装关系
SwitchDevice = 具有独立状态和端子的可控制开断设备
CableTerminal = 不可操作的连接端子能力
```

`CableTerminal` / `CableTermination` 在目标模型中不作为独立设备类型。当前代码仍存在 `CableTermination : Device` 的历史实现；本决策记录目标边界，但本阶段不修改代码或 V4，后续必须通过独立迁移设计消除差异。

## 3. Preserve Device Type Distinctions

系统必须保留 Breaker、LoadSwitch、Disconnector 和 Fuse 四类设备身份，不把它们合并为一个无类型的“开关”。

虽然第一阶段 Breaker 和 LoadSwitch 都只提供分闸、合闸以及两端断开/导通行为，但它们仍有不同的：

- 真实设备语义；
- 工作票和设备名称；
- 图例表达；
- 安装及组合上下文；
- 后续保护、开断能力或操作规则扩展方向。

因此，行为暂时相同不构成合并类型的理由。类型信息是稳定设备事实，不从 Symbol、DisplayName 或安装位置反向推断。

## 4. Avoid Excessive Domain Modeling

第一阶段不建立复杂设备继承体系，例如：

```text
ElectricalDevice
  → SwitchingDevice
      → ProtectiveSwitch
          → CircuitBreaker
      → LoadBreakingDevice
          → LoadSwitch
      → VisibleIsolationDevice
          → Disconnector
      → FuseDevice
```

当前采用：

```text
SwitchDevice
  Stable ID
  SwitchKind
  SwitchInstallationType
  SwitchState
  TerminalIds
  DisplayName
  VoltageLevel
  optional DispatchNumber
```

`SwitchKind` 表达设备类型，`SwitchInstallationType` 表达 `CabinetInterval` 或 `Pole` 安装语境。统一模型避免复制 Stable ID、端子、状态、持久化、命令和 Inspector 逻辑。

只有当某类设备出现无法由 Kind、状态和受限规则表达的真实独立生命周期或数据合同，并已有明确消费者时，才重新评估专用类型。未来可能扩展不等于现在需要继承层次。

## 5. First-Phase Capabilities

### 5.1 Common Contract

所有 `SwitchDevice` 第一阶段具有：

- 工程内 Stable ID；
- 明确 `SwitchKind`；
- `Open` 或 `Closed` 状态；
- 两个不同的 Terminal IDs；
- Terminal 与 ElectricalNode 的连接关系；
- 安装语境；
- 可由 Command 修改并参与 Undo/Redo/Dirty；
- 可由 Persistence 保存并恢复同一 Stable ID。

普通两端开关的基础导通语义：

| SwitchState | 两端关系 |
| --- | --- |
| `Open` | 两端不导通 |
| `Closed` | 两端导通 |

接地刀闸仍是现有 `GroundSwitch`，其一端连接设备侧节点，另一端连接大地节点；它不属于本文列出的四类普通线路开关，但继续复用 `SwitchDevice`。

### 5.2 Breaker and LoadSwitch

Breaker 和 LoadSwitch 第一阶段行为一致：

- 支持分闸；
- 支持合闸；
- Open 时两端断开；
- Closed 时两端导通；
- 状态变化不隐式修改相邻设备或线路状态。

第一阶段暂不实现：

- 保护动作；
- 保护定值；
- 跳闸原因；
- 故障电流开断能力；
- 自动重合闸；
- 遥控、遥信和事件顺序记录。

这些缺失不应通过自由 metadata 字典或未验证的 nullable 字段提前占位。

### 5.3 Fuse

Fuse 第一阶段复用 `Open` / `Closed` 两端导通语义，以满足绘图和基础拓扑识别。暂不增加“熔断”“缺相”“拆除”等状态。

若未来专业需求确认 Fuse 的状态不应继续用 Open/Closed 表达，应单独扩展状态能力和迁移合同；不得仅修改图例而让 Domain 状态含义漂移。

## 6. Disconnector Semantics

Disconnector 必须保留独立 `SwitchKind`，因为它承担可见隔离语义，并存在不允许带负荷操作和操作顺序要求。

基础停电顺序：

```text
Breaker / LoadSwitch 分闸
        |
        v
Disconnector 分闸
```

基础送电顺序：

```text
Disconnector 合闸
        |
        v
Breaker / LoadSwitch 合闸
```

这是一条工作票操作语义，不授权系统做完整潮流或负荷计算。“不允许带负荷操作”不能简单等价为“相邻 Breaker 必须 Open”并应用于所有拓扑；实际校验必须基于明确组合、端子关系和已确认规则集。

第一阶段实施原则：

- 单台 Disconnector 仍独立保存状态；
- 修改 Disconnector 不自动修改 Breaker、LoadSwitch 或线路状态；
- 柜内已确认的互锁继续由 `SwitchAssembly` 受限规则处理；
- 柱上设备没有已确认组合规则时，不创建虚假的 `SwitchAssembly`；
- 未获得专业确认的全网操作序列只提供提示或保持未实现，不作为任意拓扑上的硬规则。

## 7. Domain and Symbol Separation

Domain 设备类型与 Rendering 图例必须分离：

```text
Domain SwitchKind.LoadSwitch
        |
        v
Rendering.Wpf 负荷开关图例

Domain SwitchKind.CircuitBreaker
        |
        v
Rendering.Wpf 断路器图例
```

Domain 保存设备事实、状态、端子和拓扑；Rendering.Wpf 负责：

- 将 `SwitchKind` 映射到 SymbolKind；
- 根据 `SwitchState` 选择分/合视觉状态；
- 线宽、颜色、尺寸、标签和 HitTest；
- 根据安装语境选择布局和显示样式。

Template 和 Domain 不保存 WPF 类型、SceneElement 或图元路径。未来同一种设备可以因厂家、图纸标准或安装语境使用不同显示样式，同时保持同一个 Domain `SwitchKind` 和 Stable ID。

显示样式差异不得反向产生新的设备类型；设备类型差异也不得只靠不同 Symbol 隐式表达。

## 8. Impact on Ring Cabinets

### 8.1 Structural Rule

环网柜间隔由固定 `IntervalKind` 决定其设备和 Terminal 拓扑，而不是由调用方自由拼装。

目标结构词汇包括：

- Disconnector；
- Breaker 或 LoadSwitch；
- GroundSwitch（当固定间隔结构要求时）；
- 柜内 switch terminals；
- 面向外部回路的 cable-side external terminal。

这里的 CableTerminal 指环网柜间隔面向外部电缆/线路的 `ExternalTerminal` 连接点，不是柱上 `CableTermination` 设备。

### 8.2 Current Implemented Variants

当前已实现的固定结构必须如实保留：

| IntervalKind | 当前设备结构 |
| --- | --- |
| `LoadSwitchInterval` | LoadSwitch + GroundSwitch + external terminal |
| `IntegratedFeederInterval` | IsolationSwitch + CircuitBreaker + GroundSwitch + external terminal |

因此，“每个环网柜间隔都包含 Disconnector”不是当前普遍不变量。若未来批准 Conventional LoadSwitch interval 必须增加独立 Disconnector，需要新的 Interval schema、Domain factory、Layout、Symbol、Persistence compatibility 和测试评审；本决策文档不修改已提交的 `Conventional10kVRingCabinetTemplate`。

### 8.3 Preserved Boundaries

本决策保持：

- Project FormatVersion 4；
- Cabinet、Interval、Switch、Terminal、Node 和 Assembly Stable IDs；
- Approved Built-in Template 体系；
- Template → Domain Builder → Layout → Command 链；
- 固定 Interval factory 和 topology validation；
- Undo/Redo 复用原对象。

不得重新引入 `BayFunction`，也不得用 Incoming、Outgoing、Tie 或左右位置替代设备结构。设备类型来自 `SwitchKind`，间隔结构来自 `IntervalKind`。

## 9. Impact on Overhead Equipment

### 9.1 Pole as Device

Pole 继续是 `DeviceType.Pole` 的设备，保存杆号、杆型、Stable ID 和必要的架空锚点 Terminal 引用。Pole 本体不是 SwitchDevice，也不因为安装了开关而改变 DeviceType。

Pole 可以通过 Attachment 能力承载：

- CircuitBreaker；
- LoadSwitch；
- IsolationSwitch；
- DropoutFuse；
- CableTerminal 连接端子能力。

柱上四类开关统一使用：

```text
SwitchDevice
  InstallationType = Pole
  SwitchKind = CircuitBreaker | LoadSwitch | IsolationSwitch | DropoutFuse
```

每台柱上 SwitchDevice 具有两个线路 terminals，通过 OverheadLine connections 参与电气拓扑。SwitchDevice 通过 `PoleAttachment` 与 Pole 建立安装关系；屏幕距离或图形重叠不能替代该关系。

三者职责严格分离：

- Pole 是主体设备，保存杆塔身份和杆塔自身能力；
- Attachment 是“什么能力/设备附属于哪根 Pole”的安装关系或能力承载，不是可操作开关；
- SwitchDevice 是可控制开断设备，独立保存 Stable ID、SwitchKind、SwitchState 和 terminals；
- Attachment 不复制 SwitchState，也不替代 SwitchDevice identity；
- SwitchDevice 不因安装在 Pole 上而变成 Pole 的内嵌状态字段。

### 9.2 Cable Terminal Capability Boundary

目标模型中 CableTerminal / CableTermination 是 Pole 的附属连接端子能力，不是独立设备，也不是 SwitchDevice。它用于表达电缆与架空侧在该杆位的连接边界。

该能力至少需要：

- cable-side terminal；
- overhead-side terminal；
- 表达两侧固定电气关系的 ElectricalNode；
- 由 Attachment 归属于具体 Pole；
- Terminal、Node 和 Attachment 各自需要的稳定身份。

它不具有：

- 独立 DeviceType；
- SwitchKind；
- SwitchState；
- 分闸或合闸 Command；
- 保护、联锁或可控制开断语义。

CableTerminal 的固定连接关系由 terminals 和 ElectricalNode 表达，不创建一个可操作“电缆终端设备”。Attachment 表达它附属于哪根 Pole，不等于电气导通；Connection 仍连接明确 terminals。

当前生产代码中的 `CableTermination : Device` 与此目标边界不一致。处理原则是：

1. 本文只冻结目标决策，不在 F-2-B-2 修改代码；
2. 后续单独审查当前 V4、Command、Selection、Rendering 和测试对 `CableTermination` Device identity 的依赖；
3. 若实施迁移，旧 V4 文件必须继续可读，并把原 identity 安全映射为 Attachment/Terminal/Node identity；
4. 在迁移完成前，不得声称当前运行时代码已经符合本节目标模型；
5. 不通过删除校验或丢弃 Stable ID 快速绕过兼容问题。

### 9.3 Transformer Future Extension Boundary

Transformer 保持为未来 Attachment 扩展能力，不进入当前 10kV 工作票第一阶段实现范围。

当前不新增：

- `Transformer` Domain 类型；
- TransformerKind、容量、变比、接线组别或分接头；
- Transformer terminals 或内部拓扑；
- Transformer Symbol、Command、Persistence DTO 或 Template；
- 将 Transformer 塞入 `SwitchKind`。

未来若需求确认，Transformer 应作为非 SwitchDevice 的独立专业能力接入 Pole/Attachment 扩展边界，并通过自己的 terminals 参与拓扑。届时需单独设计 Domain、Rendering、Persistence 和工作票语义；当前只保留架构扩展位置，不预埋字段或空类型。

## 10. Basic Operation Logic Boundary

系统可以基于 SwitchState 和 Terminal/Node 关系理解单台设备的局部导通状态，并对已确认组合执行有限联锁校验。

系统第一阶段可以支持：

- 为工作票记录目标开关及分/合操作；
- 区分断路器、负荷开关、隔离刀闸和熔断器；
- 检查明确 `SwitchAssembly` 内的已确认非法组合；
- 在固定结构中验证基本停送电顺序；
- 保持每次状态修改的 Command、Undo/Redo 和 Dirty 语义。

系统第一阶段不应宣称：

- 自动判断任意设备是否正在带负荷；
- 自动推导全网带电范围；
- 自动生成完整工作票步骤；
- 自动验证所有跨设备操作顺序；
- 根据颜色、位置或名称推导拓扑。

任何硬校验都必须有明确拓扑范围、设备组合和专业规则来源。

## 11. Persistence and Stable ID

本决策阶段不修改 V4。当前已实现的 V4 继续按现有代码保存：

- SwitchDevice Stable ID；
- `SwitchKind`；
- `SwitchInstallationType`；
- `SwitchState`；
- Terminal IDs；
- Parent interval 或 PoleAttachment 等所属关系；
- ElectricalNode 和 Connection 关系。

对于目标 CableTerminal 能力，未来迁移后的持久化事实应是 Pole/Attachment 归属、Terminal IDs、ElectricalNode ID 和 Connection endpoints，而不是独立 CableTermination Device identity。该变化必须提升或明确迁移格式，不能在当前 V4 含义下静默替换。

更换 Symbol style 不改变 Domain 或 Stable ID。状态 Command 不重建设备。Save/Reload、Undo/Redo 必须保持同一 SwitchDevice 和 Terminal identity。

若未来新增设备状态或强制操作规则，需要单独评估 V4 compatibility；不能通过重新解释既有 enum 值悄然改变历史文件语义。

## 12. Non-Goals

本阶段暂不实现：

- 继电保护模型；
- 保护定值和动作逻辑；
- SCADA、遥控、遥信和事件记录；
- 潮流计算；
- 短路计算；
- 完整配网分析；
- 自动带电传播；
- 任意拓扑规则引擎；
- 自由脚本联锁；
- 自动生成完整停送电票；
- Fuse 的熔断、缺相或拆除状态；
- 新的设备继承体系；
- BayFunction 或方向角色模型；
- Transformer Domain、Rendering、Persistence 或工作票行为；
- CableTermination Device 到 CableTerminal capability 的代码迁移。

## 13. Consequences

### 13.1 Benefits

- 保留真实设备类型和图例差异；
- 复用统一 Stable ID、状态、Terminal、Command 和 Persistence 逻辑；
- 支持当前基础拓扑识别和工作票绘图；
- 避免因未来可能性建立复杂继承体系；
- 允许 Rendering 样式独立演进；
- 为 Disconnector 操作顺序保留明确扩展边界。

### 13.2 Risks

- Open/Closed 对 Fuse 是第一阶段简化，未来可能需要状态迁移；
- Breaker 与 LoadSwitch 暂时行为相同，调用方仍必须按 Kind 区分，不能删除类型；
- “Disconnector 不允许带负荷操作”若脱离明确组合直接实现，可能产生错误硬校验；
- 把环网柜 external terminal 与柱上 CableTerminal capability 混为同一个 owner，可能造成所有权和拓扑错误；
- 把 PoleAttachment 当作电气连接，会混淆安装关系和导通关系。
- 当前 `CableTermination : Device` 与目标决策存在实现差异，后续迁移若未保护 V4 和 Stable IDs 会造成兼容风险；
- 为“未来 Transformer”提前增加空字段或复用 SwitchKind，会污染当前模型。

## 14. Final Decision

第一阶段正式采用：

```text
one SwitchDevice model
        +
explicit SwitchKind
        +
Open / Closed state
        +
two-terminal topology
        +
limited, confirmed assembly rules
```

Breaker、LoadSwitch、Disconnector 和 Fuse 保留独立设备类型，但不建立复杂继承体系。Domain 与 Rendering 图例保持分离。环网柜使用固定 IntervalKind 生成受控内部结构。

杆塔侧最终关系为：Pole 是主体设备；Attachment 是附属能力和安装关系；SwitchDevice 是具有独立状态、端子和控制行为的开断设备；CableTerminal 是不可操作的连接端子能力，不是独立设备。Transformer 只保留为未来 Attachment 扩展边界，不进入当前实现。

该模型满足当前 10kV 工作票绘图和基础电气拓扑识别目标，同时明确排除完整电网仿真、继保和 SCADA 范围。
