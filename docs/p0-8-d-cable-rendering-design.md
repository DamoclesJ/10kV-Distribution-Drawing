# P0-8-D Cable Rendering Design

## 1. Context and Goals

本阶段为 10kV 工作票绘图定义电缆及其拓扑端点的 Rendering 模型。

当前系统已经具备：

- `CableSegment` Domain 模型；
- `Connection` 和 Terminal-centric Connectivity Graph；
- Cable Split；
- Cable Reconnect；
- RingCabinet Rendering；
- Pole 和 PoleAttachment Rendering。

目标是把当前电缆业务对象和拓扑端点投影为可读的图形场景，并能够反映 Split/Reconnect 后的当前拓扑。

本阶段只做设计，不实现符号、布局、选择或拓扑编辑 UI。

## 2. Rendering Boundary

Domain 保存电气事实、业务对象和稳定身份：

- `CableSegment`：电缆业务信息及其当前端点引用；
- `CableTermination`：杆塔上的电缆终端能力和 Terminal Owner；
- `IntermediateTerminal`：电缆拆分产生的轻量拓扑端点；
- `Connection`：当前有效的 Terminal-to-Terminal 电气连接。

Rendering 负责：

- CableLine Symbol；
- CableLabel；
- Joint Symbol；
- CableLayout；
- TerminalAnchorLayout；
- 未来的命中区域和 Stable ID 映射。

Rendering 只能读取 Domain，不得修改端点、Connection、CableSegment 或拓扑关系，不得创建 Stable ID，也不得通过坐标推断新的电气连接。

## 3. CableSegment Rendering Model

第一版显示链路为：

```text
CableSegment
    ↓
CableSymbol
    ↓
SceneElement
```

`CableSegment` 的 `StartTerminalId` 和 `EndTerminalId` 用于查找两个 Terminal 的 Rendering Anchor。`ConnectionId` 用于确认当前电气连接事实和保持图形对象身份关联，但 CableSymbol 不创建或替换 Connection。

第一版 CableSymbol 可以显示：

- 电缆线段；
- `CableType` 或型号；
- `Length` 或长度文本；
- 未来的显示状态和施工标记。

电缆线的几何路径属于 Rendering Layout，不回写 CableSegment，也不把路径点保存为 Domain 拓扑节点。

## 4. CableTermination Rendering

在 Cable Rendering 中，`CableTermination` 表示 Pole 上的电缆终端 Attachment 能力和 Terminal Anchor。

显示层应将它作为电缆端点附近的终端符号处理，而不是把它绘制成另一根杆或一段 CableSegment。

设计语义上，CableTermination：

- 是 PoleAttachment 对应的电缆终端能力；
- 提供与电缆连接的 Terminal Anchor；
- 不替代 Pole；
- 不代表 CableSegment；
- 不自动创建 Connection。

当前 Domain 为保持既有工程和 V6 Persistence 兼容，可能仍以 `CableTermination : Device` 保存该对象。Rendering 不重新定义这一 Domain API，只保持其显示职责为 Attachment/Terminal Anchor。

## 5. IntermediateTerminal Rendering

`IntermediateTerminal` 是 Cable Split/Reconnect 使用的轻量拓扑端点，不是 Device、Attachment 或 Connection。

第一版使用 Joint Symbol 表示它：

```text
CableSegment ───── ○ ───── CableSegment
                   X
```

Joint Symbol 只表达一个中间接头或拓扑端点：

- 读取 `IntermediateTerminal.Id` 和其 `TerminalId`；
- 显示为接头圆点或等价轻量符号；
- 提供未来选择所需的 Stable ID；
- 不显示设备图标；
- 不创建 T 型分支；
- 不创建新的 Terminal 或 Connection。

中间接头默认只连接两段电缆。多于两个方向的连接属于未来分支模型，不由本阶段推断或生成。

## 6. Split Rendering

Split 前：

```text
Terminal A ───────── CableSegment-001 ───────── Terminal B
```

Split 后：

```text
Terminal A ── CableSegment-002 ── ○ X ── CableSegment-003 ── Terminal B
```

Rendering 不记录 Split 操作历史，而是读取当前 Domain 状态：

1. 旧 CableSegment 不再作为当前对象出现；
2. 两个新 CableSegment 分别生成两条 CableSymbol；
3. IntermediateTerminal 生成一个 Joint Symbol；
4. 两段 Cable 的端点通过 TerminalAnchorLayout 对齐到 Joint；
5. Graph 和 Connection 仍由 Domain/Application 提供，Rendering 不复制其算法。

## 7. Reconnect Rendering

Reconnect 不创建新的 CableSegment，而是保留 CableSegment、替换当前 Connection 和端点引用。

改接前：

```text
Terminal A ───────── CableSegment-001 ───────── Terminal B
```

改接后：

```text
Terminal A ───────── CableSegment-001 ───────── Terminal C
```

Rendering 每次根据当前 `StartTerminalId`、`EndTerminalId` 重新解析两个 Anchor 并更新 CableLayout。它不保存 Before/After 状态，不显示 Undo/Redo 历史，也不生成额外的 CableSegment。

如果 Reconnect 后仍存在旧 Connection 的历史记录，该历史不属于当前 Scene；当前图形只显示当前有效 Connection。

## 8. Layout Model

第一版定义三类 Rendering Layout：

- `CableLayout`：CableSegment 的起点、终点、路径、宽度和标签位置；
- `TerminalAnchorLayout`：CableTermination、IntermediateTerminal、RingCabinet Terminal 或其他端子的图形锚点；
- `JointLayout`：IntermediateTerminal 的接头位置、尺寸和标签偏移。

布局计算规则：

1. 先根据 Terminal 身份取得端点 Anchor；
2. 再根据两个 Anchor 生成 CableLayout；
3. 对 IntermediateTerminal 使用其 Terminal Anchor 作为 JointLayout 的中心；
4. 对 Split 后的两个 Segment 分别生成独立 CableLayout；
5. 对 Reconnect 后的 Segment 重新使用新的端点 Anchor；
6. 所有坐标和路径只存在于 Rendering，不写入 Domain。

布局不能根据左右位置、线段方向或坐标推断 Incoming、Outgoing、Tie、SourceSide 或 LoadSide。

## 9. Graph Relationship

Graph 继续保持 Terminal-centric：

```text
CableSegment
    ↓ references
Connection
    ↓ connects
Terminal A ───── Terminal B
```

Rendering 不新增 CableSegment Edge，也不创建第二套拓扑模型。CableSegment 只是业务显示对象；Connection 才是当前电气连接事实。

Split 或 Reconnect 后，Application 只需更新 Domain，Rendering 重新读取当前 Connection 和 Terminal，即可生成新的图形。

## 10. Selection Preparation

后续 Selection 需要能够命中并识别：

- CableSegment；
- IntermediateTerminal/Joint；
- CableTermination；
- Cable 端点 Terminal Anchor。

每个可选择图形应携带对象类别和 Stable ID：

- CableSegment 使用 `CableSegment.Id`；
- Joint 使用 `IntermediateTerminal.Id`；
- CableTermination 使用其既有 Device/Attachment 身份；
- 端点使用 Terminal ID。

本阶段不实现鼠标命中、选择状态或编辑命令。

## 11. Persistence Boundary

Rendering 不修改 Persistence，也不保存临时 Layout。

工程保存/恢复由 Infrastructure 负责：

- CableSegment 的业务属性和端点引用；
- IntermediateTerminal 的 Stable ID 和 Terminal 引用；
- 当前有效 Connection；
- 既有 CableTermination 和 PoleAttachment 事实。

加载工程后，Rendering 根据恢复后的 Domain 重新生成 CableLayout、JointLayout 和 TerminalAnchorLayout。Undo/Redo 历史、路径缓存和临时 Scene 不进入工程文件。

## 12. Non-Goals

本阶段明确不实现：

- T 型电缆分支；
- 电缆分支箱；
- 多端点 CableSegment；
- GIS 电缆通道；
- 三维电缆路径；
- 路径优化；
- 自动长度计算；
- 自动拓扑推断；
- 配网仿真、潮流或短路计算；
- SCADA；
- Cable Selection UI；
- Split/Reconnect 编辑操作。

## 13. Follow-up Slices

### P0-8-D-2 Cable Symbol Runtime

实现普通 CableSegment 的 CableSymbol、型号/长度标签和端点 Anchor 连接。

### P0-8-D-3 Cable Joint Rendering

实现 IntermediateTerminal 的 Joint Symbol、JointLayout 和两段电缆的接头显示。

### P0-8-D-4 Cable Topology Visual Update

实现 Split/Reconnect 后 Scene 根据当前 Domain 拓扑重新生成和更新，保持 CableSegment、Joint 和端点 Stable ID 映射。

## 14. Final Design Decision

第一版 Cable Rendering 采用：

```text
CableSegment
    |
    +── CableSymbol
    |
    +── CableLabel
    |
    +── TerminalAnchorLayout
    |
    +── JointSymbol（IntermediateTerminal）
```

其中：

- Connection 仍是电气拓扑事实；
- CableSegment 是电缆业务显示对象；
- CableTermination 是杆塔上的终端 Attachment 能力；
- IntermediateTerminal 是轻量接头拓扑点；
- Split/Reconnect 只通过当前 Domain 状态反映到图形；
- Layout、Symbol 和 Label 只属于 Rendering；
- Rendering 不修改拓扑，也不替代 Graph、Command 或 Persistence。
