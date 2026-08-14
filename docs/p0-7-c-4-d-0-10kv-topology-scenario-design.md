# P0-7-C-4-D-0 10kV Topology Scenario Design

> 状态：场景设计；本阶段只冻结业务场景，不修改生产代码、测试、Persistence 或 UI。
> 目标：为 Cable、OverheadLine 和工作票拓扑验证建立共同的真实 10kV 场景基准。

## 1. Design Goal

本项目是具有基础电气拓扑识别能力的 10kV 工作票绘图工具。场景模型必须能够表达：

- 环网柜及其固定内部间隔结构；
- 电缆外部连接；
- 电缆终端杆；
- 柱上开关及其分合状态；
- 架空线路；
- 电缆拆分和改接后的端子/连接关系。

本阶段冻结的是工作票中需要描述的设备、端子、连接和状态事实，不建立配网运行仿真。场景中的 A、B、X 等标识都是稳定的测试语义名称，真正实现仍使用各对象的 Stable ID。

## 2. Common Modeling Rules

### 2.1 Object Layers

```text
Device / Aggregate
        ↓ owns
Terminal
        ↓ endpoint of
Connection / CableSegment / OverheadLine
```

- `Device` 表达设备主体或设备能力的身份；
- `Terminal` 表达可连接端点；
- `ElectricalNode` 表达固定内部等电位关系；
- `Connection` 表达两个 Terminal 之间的外部连接；
- `SwitchDevice` 的 Closed 状态在 Connectivity Graph 中形成动态边；
- `PoleAttachment` 只表达安装关系，不自动形成电气边。

所有外部连接必须显式引用两个 Terminal。不得由画布位置、线条接触、杆塔顺序或 Symbol 自动推断端点。

### 2.2 Graph Interpretation

Electrical Connectivity Graph 继续采用 Terminal-centric 模型：

- ElectricalNodeInternal Edge：同一固定 Node 中的 Terminal；
- Connection Edge：Cable 或 OverheadLine 等外部连接；
- ClosedSwitch Edge：Closed SwitchDevice 的两个 Terminal。

Graph 只读取当前 Domain 快照。工作票查询可以回答“当前是否存在结构性路径”，但不回答电压传播、潮流方向、带电范围或保护动作。

## 3. Minimum Complete Scenario

### 3.1 Business Topology

第一版完整线路场景建议冻结为：

```text
RingCabinet A
    │ external Terminal
    │ Cable Connection / CableSegment
    │
Pole P1
    ├─ CableTerminal Attachment
    │    Cable-side Terminal ─ fixed internal ElectricalNode ─ Overhead-side Terminal
    └─ SwitchDevice Attachment
         Switch Terminal 1 ─ Closed/Open Switch ─ Switch Terminal 2
    │
    │ OverheadLine Connection
    │
Pole P2 / equipment pole / RingCabinet B
```

实际端点应拆成明确的 Connection：

```text
Cabinet A external Terminal
  ─ Cable Connection ─
P1 CableTerminal cable-side Terminal
  ─ fixed ElectricalNode ─
P1 CableTerminal overhead-side Terminal
  ─ OverheadLine Connection ─
P1 SwitchDevice terminal 1
  ─ ClosedSwitch Edge when closed ─
P1 SwitchDevice terminal 2
  ─ OverheadLine Connection ─
P2 / Cabinet B external Terminal
```

如果现场方案中开关前后分别连接不同杆塔，则将每一段 OverheadLine 的两个 endpoint 绑定到实际杆塔 Terminal；不因图形上属于同一杆塔而合并端子。

### 3.2 Object Inventory

| 场景对象 | Domain 类型/边界 | 需要的拓扑事实 |
| --- | --- | --- |
| Cabinet A/B | `RingCabinet` aggregate | external Terminal、Interval、内部 Node、SwitchDevice |
| P1/P2 | `Pole` Device | Pole identity、PoleType、anchor Terminal |
| P1 cable terminal | `CableTermination` Device + `PoleAttachment` | Cable-side Terminal、Overhead-side Terminal、内部 ElectricalNode |
| P1 switch | `SwitchDevice` + `PoleAttachment` | 两个 Terminal、`SwitchKind`、`SwitchState` |
| Cable | Connection/CableSegment 连接事实 | 两个 Cable-compatible Terminal |
| OverheadLine | `Connection` 的架空线路语义及其明细 | 两个 OverheadLine-compatible Terminal、支撑杆塔信息 |

## 4. RingCabinet Scenario

RingCabinet 继续是固定拓扑聚合，包含：

- Interval；
- Interval 内的 SwitchDevice；
- 内部 Terminal；
- MainBus、Circuit、Earth 等 ElectricalNode；
- Interval external Terminal；
- 现有 SwitchAssembly。

每个 Interval 的真实内部结构由 `IntervalKind` 和既有 Domain 创建/恢复规则决定。场景设计不得自由拼装柜内开关，也不得重新引入 BayFunction、Incoming、Outgoing 或 Tie。

Connectivity Graph 对 RingCabinet 只读取：

- Terminal；
- ElectricalNode；
- 外部 Connection；
- SwitchDevice 当前 SwitchState。

Graph 不复制 SwitchAssembly 的联锁、接地或结构规则。状态变化仍通过 `DrawingDocument.ChangeSwitchState` 和现有 `SwitchAssembly` 规则完成。

## 5. Pole and Attachment Composition

Pole 是物理主体设备，附件是能力组合：

```text
Pole P1
├── PoleAttachment → SwitchDevice
└── PoleAttachment → CableTermination
```

同一个 Pole 可以同时具备多个 Attachment：

- SwitchDevice Attachment 提供可操作的两端开关能力；
- CableTermination Attachment 提供电缆侧与架空侧端子及固定内部连接；
- 未来可增加 Transformer 或其他杆上设备 Attachment。

Attachment 不自动表示导通。只有以下事实才会生成 Graph 边：

- 两个 Terminal 指向同一 ElectricalNode；
- 两个 Terminal 被 Connection 显式连接；
- SwitchDevice 当前为 Closed。

例如，CableTermination 与 SwitchDevice 同属 P1 并不意味着二者自动连接；必须存在明确的 OverheadLine Connection 端点。

## 6. Cable Segment Boundary

### 6.1 No CableJoint

当前不引入 `CableJoint` 作为独立 Device，也不把电缆接头当作具有独立设备生命周期的对象。电缆中间改接点使用 Terminal/Connection 结构表达。

### 6.2 CableSegment

未来 `CableSegment` 应属于连接层的电缆段事实，而不是独立设备。最小结构为：

```text
Terminal A
    │
CableSegment S1
    │
Terminal B
```

它至少需要明确：

- Segment Stable ID；
- 起点 TerminalId；
- 终点 TerminalId；
- Cable segment 的工程属性，例如型号、长度或状态；
- 与工作票操作相关的连接生命周期。

CableSegment 不拥有 Pole、RingCabinet 或 SwitchDevice，不生成设备 Terminal。其 endpoint Terminal 必须由设备或拓扑结构创建并明确注册。

第一版实现应优先复用现有 `Connection` 两端 Terminal 语义。若 CableSegment 需要独立专业字段，应作为 Connection 的明确电缆段明细或受控连接类型设计，不能并行创建第二套隐式连通模型。

### 6.3 Intermediate Terminal

复杂电缆可表达为：

```text
Terminal A
    │
CableSegment S1
    │
Intermediate Terminal X
    │
CableSegment S2
    │
Terminal B
```

`Intermediate Terminal X`：

- 是拓扑端点，不是 `Device`；
- 拥有自己的 Stable TerminalId；
- 不自动成为 ElectricalNode；
- 是否固定连通由显式 Connection/CableSegment 关系决定；
- 不携带 Pole、CableTermination 或设备显示语义。

如果现场改接点需要固定内部导通，应另行明确 ElectricalNode 事实；不能仅凭 Terminal 名称或相同位置推断。

## 7. Cable Split and Reconnect Scenario

### 7.1 Before Construction

原始拓扑：

```text
Terminal A ───── CableSegment S0 ───── Terminal B
```

Graph 中存在一条由 S0/Connection 表示的外部连接边。

### 7.2 After Construction

施工后，原连接被拆分并改接到 C：

```text
Terminal A ─ CableSegment S1 ─ Intermediate Terminal X
                                      │
                           CableSegment S2
                                      │
                                  Terminal C
```

该变化意味着：

- 原 S0 连接被移除、替换或标记为历史事实，具体策略由后续 Persistence 设计冻结；
- S1、S2 是新的连接段事实；
- X 是新的拓扑 Terminal，不是新增 Device；
- A 与 C 的连通性来自 S1 + S2 的显式路径；
- B 是否仍连通取决于是否存在独立连接，不由旧 S0 名称推断。

### 7.3 Command Boundary

未来必须通过原子 Command 实现，例如：

- `SplitCableCommand`：把一个既有段拆为两个段并创建/登记 X；
- `ConnectCableCommand`：把 CableSegment 连接到明确的目标 Terminal；
- 必要时配套 Undo/Redo。

Command 必须保持：

- 原有设备 Stable ID 不变；
- 未涉及的 Terminal、Device、Connection 不变；
- Undo 恢复拆分前的完整拓扑；
- Redo 重现同一组 Segment/Terminal 身份，而不是重新随机生成；
- Graph 在 Command 成功后从新的 `DrawingDocument` 快照重建。

本阶段不实现这些 Command，也不决定历史 CableSegment 的归档格式。

## 8. OverheadLine Scenario

架空线路属于 Connection 语义，不是 Device：

```text
Pole A anchor Terminal
          │
   OverheadLine Connection
          │
Pole B anchor Terminal
```

杆塔负责提供可连接的 Terminal。`OverheadLine` 可以保存线路型号、长度、支撑杆塔序列及延续信息，但这些字段不替代 Connection 的两个 endpoint。

规则：

- 两个 endpoint 必须是允许 `OverheadLine` 的 external Terminal；
- 支撑杆塔列表不自动创建额外 Terminal 或 ElectricalNode；
- 线路几何和支撑顺序不改变电气连接端点；
- 一条架空线路不因经过多个杆塔而自动形成多段拓扑，除非未来显式拆分为多个 Connection/Segment。

## 9. Switch State Scenarios

### 9.1 All Closed

在完整线路中，柱上 SwitchDevice 为 Closed：

```text
A ─ Cable ─ CableTermination ─ OverheadLine ─ [Closed Switch] ─ OverheadLine ─ B
```

Graph 查询预期：

- 开关两端存在 `ClosedSwitch` 边；
- 若其他连接完整，A 与 B 的 Terminal 存在结构性路径；
- 查询结果只表示路径存在，不表示 A/B 已带电。

### 9.2 Pole Switch Open

柱上开关变为 Open：

```text
A ─ Cable ─ CableTermination ─ OverheadLine ─ [Open Switch] X ─ OverheadLine ─ B
```

Graph 查询预期：

- Open Switch 不生成动态边；
- A 侧与 B 侧不能通过该开关跨越；
- 两侧各自仍可与本侧其他固定 Connection/Node 连通；
- 不修改 Cable、OverheadLine 或 Terminal 数据。

状态变化必须经过现有 Switch State Operation。Graph 只在新状态快照上重新构建。

## 10. Graph Validation Scenarios

### Scenario 1: Two RingCabinets Connected by Cable

```text
RingCabinet A external Terminal
          │
       Cable
          │
RingCabinet B external Terminal
```

验证数据：

- 两个 RingCabinet 均由既有 Template/Domain Aggregate 创建；
- 两个外部 Terminal 具有 Cable-compatible policy；
- 一个 Cable/Connection 连接两个 Terminal；
- Graph 生成一条 Connection Edge；
- Query 可以判断两端连通。

不把两个 Cabinet 的内部 SwitchAssembly 复制到 Graph Builder 中。

### Scenario 2: Complete 10kV Line

```text
RingCabinet A
    │ Cable
    ▼
Pole P1 / CableTerminal Attachment
    │ fixed internal Node
    │ OverheadLine
    ▼
Pole P1 / SwitchDevice Attachment
    │ ClosedSwitch
    │ OverheadLine
    ▼
Equipment Pole P2 or RingCabinet B
```

验证：

- P1 同时拥有 CableTermination 和 SwitchDevice 两种 Attachment；
- Attachment 关系本身不产生边；
- CableTermination 两侧 Terminal 通过固定 Node 边连通；
- OverheadLine 只通过明确端点生成 Connection Edge；
- Closed Switch 生成 ClosedSwitch Edge；
- 全路径可由 Terminal 查询得到；
- 将 Switch 改为 Open 后跨开关路径消失。

### Scenario 3: Cable Split and Reconnect

```text
Before: A ─ S0 ─ B
After:  A ─ S1 ─ X ─ S2 ─ C
```

验证未来 Command 实现：

- X 是 Intermediate Terminal，不是 Device；
- A→C 连通；
- A→B 是否连通与 S0 是否保留/移除一致；
- Undo 恢复 A→B 原路径；
- Redo 恢复 A→X→C 路径；
- 既有设备和未参与改接的 Stable ID 不变；
- Graph 每次根据新快照重新构建。

## 11. Persistence and Identity Boundary

本场景设计不立即修改 V5，但未来实现必须保存能够重建场景的 Domain 事实：

- Device Stable IDs；
- Terminal Stable IDs；
- ElectricalNode Stable IDs；
- Connection/CableSegment Stable IDs；
- PoleAttachment 关系；
- SwitchState；
- OverheadLine 与 Connection 的一对一明细关系。

Connectivity Graph 是派生结果，不保存 Graph 顶点或边。Save/Reload 后使用相同 Stable IDs 重建等价 Graph。

## 12. Work Ticket Information Scope

工作票绘图需要的最小拓扑信息包括：

- 操作对象的稳定身份和设备类型；
- 开关设备的 `SwitchKind` 与 Open/Closed 状态；
- 端子之间的明确连接关系；
- 电缆终端和杆塔附属关系；
- 当前操作前后的连通性变化；
- Cable Split/Reconnect 的施工前后拓扑差异；
- 可用于解释路径的 Connection、SwitchDevice 和 Terminal 身份。

当前不要求完整的电气参数、潮流方向、负荷模型或实时运行数据。

## 13. Non-Goals

本阶段明确排除：

- T 型电缆分支；
- 电缆分支箱；
- 三相潮流和相量计算；
- 短路计算；
- GIS 空间分析；
- SCADA 或实时遥测；
- 继电保护与自动跳闸；
- 自动推断 Incoming、Outgoing、Tie、SourceSide 或 LoadSide；
- 自由 CAD 拓扑；
- PT/DTU 的具体结构实现。

## 14. Follow-up Implementation Slices

后续建议按以下顺序推进：

### P0-7-C-4-D-1 Cable Segment Model Design

冻结 CableSegment 的身份、字段、与现有 Connection 的关系、Intermediate Terminal 约束以及历史/当前段的语义。

### P0-7-C-4-D-2 Cable Runtime / Split / Reconnect

实现 CableSegment 创建、拆分、改接及原子 Command，覆盖 Undo/Redo、Stable ID 和 Graph 快照变化。

### P0-7-C-4-D-3 OverheadLine Integration

将杆塔 Anchor Terminal、CableTermination 和 OverheadLine Connection 组合成可创建、可保存、可查询的真实线路链路。

### P0-7-C-4-D-4 10kV Scenario Validation

使用 Scenario 1–3 做端到端验证，覆盖 RingCabinet、Cable、PoleAttachment、SwitchState、Connectivity Query 和 Persistence round-trip。

## 15. Decision Summary

1. 10kV 场景以 Terminal 和显式 Connection 为拓扑事实源。
2. RingCabinet 是固定内部拓扑聚合，Graph 不复制 SwitchAssembly。
3. Pole 是主体，SwitchDevice/CableTermination 是可组合 Attachment 能力。
4. CableSegment 属于连接层，不是 Device；不创建 CableJoint。
5. Intermediate Terminal 是非 Device 的拓扑端点，可用于电缆拆分和改接。
6. OverheadLine 属于 Connection 语义，杆塔只提供 Terminal。
7. Split/Reconnect 是未来 Command 驱动的拓扑变更，本阶段只冻结场景。
8. Graph 查询只反映当前结构和 SwitchState，不推断潮流或带电状态。
9. Scenario 1–3 将作为后续 Cable、OverheadLine 和工作票验证的共同基准。
