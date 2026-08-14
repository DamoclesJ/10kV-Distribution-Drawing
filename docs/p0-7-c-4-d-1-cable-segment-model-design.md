# P0-7-C-4-D-1 Cable Segment Model Design

> 状态：业务模型设计；本阶段只冻结 CableSegment 边界，不修改生产代码、测试、Persistence 或 UI。
> 目标：为 10kV 工作票中的普通电缆、拆分和改接建立可审查的连接模型。

## 1. Design Goal and Scope

Cable 模型必须支持以下工作票事实：

- 普通两端电缆；
- 电缆拆分后形成多个电缆段；
- 电缆重新连接到新的 Terminal；
- 施工前后拓扑差异；
- Graph 基于当前连接事实判断 Terminal 是否连通；
- Command、Undo/Redo 和 Persistence 保持 Stable ID 语义。

本设计不模拟电缆制造、敷设过程或三维路径。CableSegment 是当前 10kV 拓扑和工作票施工变更所需的最小业务对象，不是 GIS 电缆通道模型。

明确不做：

- CableJoint 设备模型；
- T 型电缆分支；
- 电缆分支箱；
- GIS 空间路径；
- 三维电缆几何；
- 自动配网分析。

## 2. CableSegment and Connection Boundary

### 2.1 Two Different Responsibilities

`Connection` 与 `CableSegment` 必须保持职责分离：

| 对象 | 表达 | 不表达 |
| --- | --- | --- |
| `Connection` | 两个 Terminal 之间的电气拓扑连接事实 | 电缆名称、型号、施工生命周期等业务信息 |
| `CableSegment` | 一段电缆的业务身份和工程属性 | 另一套独立的电气连通算法 |

推荐关系：

```text
CableSegment S1
        │ describes
        ▼
Connection C1
        │ connects
        ▼
Terminal A ───────────────── Terminal B
```

CableSegment 不替代 Connection，原因是：

- 现有 Connectivity Graph 已以 Connection 生成 Graph Edge；
- Connection 统一处理 Cable、OverheadLine 等外部连接端点；
- Terminal policy、连接容量和 endpoint 校验继续由 Domain Connection 规则负责；
- CableSegment 的工程字段变化不应复制或分叉 Graph 逻辑；
- 未来拆分和改接只需变更显式 Connection/Segment 关系，而不需改写 Graph 基础。

### 2.2 Recommended First Shape

第一阶段推荐将 CableSegment 设计为连接层的业务明细，由一个且仅一个 Connection 引用：

```text
CableSegment
   └── ConnectionId ──> Connection
                         ├── StartTerminalId
                         └── EndTerminalId
```

如果后续确认 Segment 的字段需要独立保存，可采用 `Connection + CableSegmentDetail` 的组合；不应建立 CableSegment 自己的 endpoint 图或隐式连接。

## 3. CableSegment Lifecycle

### 3.1 Ordinary Segment

普通电缆段的拓扑为：

```text
Terminal A
    │
CableSegment S1
    │
Connection C1
    │
Terminal B
```

创建时必须先解析或创建合法的两个 endpoint Terminal，再创建 Connection，最后登记 CableSegment 业务明细。CableSegment 不负责创建设备 Terminal，也不负责推断端点。

删除时必须以原子方式处理 Segment 与其 Connection 的关系：

- 若 Connection 仍被其他业务事实引用，删除应失败；
- 若允许删除，Segment 明细和对应 Connection 同时移除；
- 不删除 endpoint Device、Pole、RingCabinet 或共享 Terminal；
- Undo 应恢复同一 Segment、Connection 和 endpoint 引用；
- Redo 不重新生成稳定身份。

修改时区分两类变化：

- 仅业务属性变化，例如名称、型号、长度：不改变 Graph 拓扑；
- endpoint 变化：属于 Reconnect 拓扑命令，不能作为普通属性直接修改。

## 4. CableSegment Data Model

### 4.1 First-Phase Properties

第一阶段建议冻结以下字段：

| 字段 | 是否第一阶段 | 规则 |
| --- | --- | --- |
| `SegmentId` | 是 | CableSegment 的稳定业务身份，非空且工程内唯一 |
| `ConnectionId` | 是 | 指向唯一 Connection，非空且不可指向其他 Segment |
| `StartTerminalId` / `EndTerminalId` | 派生/校验 | 以 Connection endpoint 为唯一事实源，不重复可编辑保存 |
| `Name` | 是 | 可选显示名，不能作为身份或端点解析依据 |
| `CableType` | 是 | 最小电缆类别/型号标识，保持字符串或受控值对象语义 |
| `Length` | 是 | 可选正长度；不得用于 Graph 路径推断 |
| `VoltageLevel` | 是 | 当前场景要求 `10kV` 兼容 |

推荐不要在 CableSegment 中同时保存可独立修改的 StartTerminalId、EndTerminalId 和 Connection endpoint。若 DTO 为了可读性重复写出端点，加载时必须验证二者一致，并以 Connection/Domain contract 为最终事实源。

### 4.2 Future Properties

以下字段暂不进入第一阶段核心模型：

- 敷设路径和 GIS 坐标；
- 相别、芯数、屏蔽层和制造工艺明细；
- 埋深、管沟、井位、通道和空间障碍；
- 运行电流、负荷率、故障率；
- 生产批次和现场资产管理数据；
- T 型分支或电缆分支箱关系。

这些字段未来若有真实消费者，应作为独立设计切片加入，不得通过 CableSegment 任意扩张。

### 4.3 Identity Rules

SegmentId 与 ConnectionId 是不同层次的稳定身份：

- SegmentId 标识“这一段电缆业务事实”；
- ConnectionId 标识“这一组两个 Terminal 的连接事实”；
- 普通属性编辑不得重新生成二者；
- Split/Reconnect 是否复用旧 SegmentId，必须由命令语义明确，不能由 UI 临时决定。

## 5. Intermediate Terminal

### 5.1 Model Position

电缆拆分需要一个中间端点：

```text
Terminal A
    │
CableSegment S1 / Connection C1
    │
Intermediate Terminal X
    │
CableSegment S2 / Connection C2
    │
Terminal B
```

`Intermediate Terminal X` 的合同为：

- 拥有独立 Stable TerminalId；
- 不是 `Device`；
- 不是 `PoleAttachment`；
- 不拥有 SwitchState；
- 不自动创建 ElectricalNode；
- 可以作为多个电缆段的明确 endpoint，但是否允许多个连接需由 Terminal policy 决定；
- 不因显示名称、空间位置或“接头”文字自动形成连接。

### 5.2 Graph Participation

Intermediate Terminal 与普通 Terminal 完全一样参与 Terminal-centric Graph：

- 每个 Intermediate Terminal 都是 Graph Vertex；
- 每个 CableSegment 对应的 Connection 生成 Connection Edge；
- A→X→B 的路径由两条显式 Connection Edge 组成；
- 没有 Connection 的 X 不会自动连通 A 或 B；
- X 不会因为属于某个 Segment 而成为 Device 顶点。

如果未来现场规则要求 X 两侧固定内部导通，应显式建模 ElectricalNode；不能为方便查询而隐式补边。

## 6. Cable Split Model

### 6.1 Before and After

拆分前：

```text
Terminal A ───── CableSegment S0 / Connection C0 ───── Terminal B
```

拆分后：

```text
Terminal A ─ CableSegment S1 / Connection C1 ─ Intermediate Terminal X

Intermediate Terminal X ─ CableSegment S2 / Connection C2 ─ Terminal B
```

拆分是拓扑结构变化，不是新增 Device。它至少改变：

- CableSegment 集合；
- Connection 集合；
- Intermediate Terminal 集合；
- Graph 中的一条边替换为两条边。

### 6.2 Identity Strategy

推荐第一阶段采用“原段退场，新段显式身份”的策略：

- 保留 S0/C0 作为施工前事实，或在当前工程状态中原子移除；
- 为 S1/C1 和 S2/C2 分配稳定 ID；
- 创建 X 的稳定 TerminalId；
- 不改变 A、B 及其所属 Device 的 Stable ID；
- 施工历史若需要保留 S0，应由后续 Work Ticket/History 设计保存，不把历史对象混入当前有效 Graph。

当前状态模型应只暴露有效拓扑。Graph 不应同时把已退场的 S0 与 S1/S2 作为有效连接加入查询。

### 6.3 Atomicity

Split 必须作为一个原子 Command：

1. 校验 S0、A、B 和目标拆分位置；
2. 创建 X、S1/C1、S2/C2 的候选对象；
3. 验证新端点和连接 policy；
4. 一次性替换旧 Segment/Connection；
5. 校验新的 Domain/Graph 拓扑；
6. 任一步失败时恢复原拓扑。

Undo 恢复 S0/C0 及原 endpoint 关系；Redo 恢复同一组 X、S1/C1、S2/C2 身份，不重新随机生成。

## 7. Cable Reconnect Model

### 7.1 Scenario

原连接：

```text
Terminal A ───── CableSegment S0 / Connection C0 ───── Terminal B
```

施工后改接到 C：

```text
Terminal A ───── CableSegment S0 / Connection C0' ───── Terminal C
```

### 7.2 Recommended Operation

Reconnect 应按“保留 Segment 业务身份，替换 Connection endpoint”还是“旧段退场、新段创建”两种语义选择其一，不能由实现细节隐式决定。

第一版推荐：

- 如果现场语义仍是同一段电缆，仅连接终点变化：保留 SegmentId，原子替换或更新其 Connection endpoint；
- ConnectionId 是否保留，需由 Connection 是否允许 endpoint 修改的 Domain 合同冻结；若 Connection immutable，则删除 C0 并创建 C0'，但由 Reconnect Command 保持可追踪关系；
- 如果现场语义是切割、接续或长度变化导致物理段发生变化：使用 Split/Remove + Create 新 Segment，不强行复用 SegmentId；
- 不修改 Terminal C 的所属 Device 或创建假设备。

在当前 Connection 类型为不可变值事实的前提下，实现阶段更安全的最小方案是：原子移除旧 Connection，创建新 Connection，再更新受控 CableSegment 关联；SegmentId 是否复用由命令参数/规则明确记录。

### 7.3 Undo/Redo and Stable IDs

Reconnect Command 必须保存完整的 before/after 引用集合：

- before ConnectionId、StartTerminalId、EndTerminalId；
- after ConnectionId、StartTerminalId、EndTerminalId；
- SegmentId 和业务属性；
- 是否存在新 Intermediate Terminal。

Undo/Redo 不得通过重新搜索“当前同名 Terminal”恢复，也不得调用 Factory 重新生成对象。未参与改接的 Device、Terminal、Node、Connection 和 Segment 的 Stable ID 必须保持。

## 8. Command Boundary

后续命令建议：

### CreateCableSegmentCommand

创建一个 CableSegment 与其唯一 Connection，引用既有两个合法 Terminal。

### RemoveCableSegmentCommand

原子移除 Segment/Connection，拒绝删除仍被工作票或其他拓扑事实引用的对象。

### SplitCableCommand

将一个既有 Segment 原子替换为两个 Segment，并创建 Intermediate Terminal。

### ReconnectCableCommand

将 Segment 的有效连接端点从一个明确 Terminal 改接到另一个明确 Terminal。

命令共同要求：

- Execute/Undo/Redo；
- 不直接修改 Graph；
- 不直接设置 Dirty；
- 不重新创建未参与操作的对象；
- 成功后由 Graph Builder 从新的 DrawingDocument 快照重建；
- 失败时不留下部分 Segment、Connection 或 Terminal。

本阶段只冻结命令边界，不实现命令或 UI。

## 9. Persistence Strategy

### 9.1 Persistence Requirement

CableSegment 不是纯 UI 临时信息。只要工程需要保存当前电缆身份、型号、长度和拆分/改接后的有效拓扑，就必须持久化：

- SegmentId；
- ConnectionId；
- CableSegment 业务属性；
- 当前有效 Connection endpoint；
- Intermediate Terminal 的 Stable ID 和必要 Terminal policy。

Graph 顶点/边不保存。加载后由 Domain 事实重新构建 Graph。

### 9.2 V5 Extension or V6

两种方案：

| 方案 | 优点 | 风险 |
| --- | --- | --- |
| V5 兼容扩展 | 版本成本低，旧 V1→V5 链保持；适合字段为可选且旧读取器可忽略的场景 | 需要明确 V5 DTO 向后兼容和旧文件保存语义 |
| V6 | 结构边界清晰，可明确 Segment、Intermediate Terminal、拆分历史合同 | 需要 V5→V6 migration，增加发布和测试成本 |

推荐在真正实现 CableSegment Persistence 前先评估 DTO 结构：

- 如果只是为现有 V5 的 `connections[]` 增加可选 CableSegment 明细，且 V5 reader 能忽略未知字段，可采用受控 V5 扩展；
- 如果新增顶层 Segment 集合、Intermediate Terminal 集合、历史/有效状态或重连关系，建议使用 V6，避免让 V5 合同隐式变成另一种文件格式。

在没有实际 DTO 方案和兼容测试前，本阶段不提前升级版本，也不承诺 V5 扩展必然安全。默认建议：完整 CableSegment 持久化优先走 V6，并保持 V1→V5 的历史读取链不变。

### 9.3 Migration Rules

未来 V5→V6 migration 不得：

- 生成随机 Stable ID 代替缺失的历史事实；
- 推断一个旧 Connection 是 CableSegment 还是其他连接；
- 自动创建 Intermediate Terminal；
- 改变已有 Connection endpoint；
- 将 OverheadLine 误转成 CableSegment；
- 为旧文件补造不可证明的电缆属性。

如果旧文件没有 CableSegment 业务事实，迁移应保持旧 Connection 可用，并由后续显式用户操作创建 Segment，而不是伪造来源。

## 10. Graph Impact

Graph 不需要新增第二套 CableSegment Edge。关系保持：

```text
CableSegment
        │ describes
        ▼
Connection
        │ creates
        ▼
ElectricalConnectivityGraph Connection Edge
        │ connects
        ▼
Terminal vertices
```

因此：

- 普通 CableSegment 通过其 Connection 进入 Graph；
- Split 后一条边变为两条 Connection Edges，中间经过 X；
- Reconnect 后 Graph 使用新的 endpoint；
- CableSegment 名称、型号、长度变化不影响 Graph 连通性；
- Graph 不读取 CableSegment 来推断第二条边；
- Graph Builder 不持有 CableSegment 或其他 Domain 对象引用。

任何“电缆断开”语义必须通过移除/失效对应 Connection 明确表达，不能仅把 CableSegment 状态改成字符串后期待 Graph 自动理解。

## 11. Rendering Boundary

未来 Rendering 可以将 CableSegment 的 Connection 以电缆线样式显示：

```text
Terminal A
    ══════════════════
Terminal B
```

拆分后：

```text
Terminal A
    ══════════ X ══════════
Terminal B
```

渲染职责只包括：

- 使用 Connection endpoint 定位两端；
- 使用 CableSegment 属性选择线型、标签或长度显示；
- 对 X 显示拓扑端点而不是虚构设备符号；
- 在施工前后状态下投影当前有效对象。

Rendering 不负责：

- 根据线段相交创建 Terminal；
- 根据坐标自动拆分 Cable；
- 生成 CableSegment/Connection Stable ID；
- 直接修改 Domain；
- 用图形显示推断连通性。

本阶段不实现 Cable Symbol、编辑手柄或 UI。

## 12. Test Scenario Design

### Scenario 1: Ordinary Cable

```text
Terminal A ─ CableSegment S1 / Connection C1 ─ Terminal B
```

验证：

- S1 与 C1 身份存在且唯一；
- Connection endpoint 为 A/B；
- Graph 有一条 Connection Edge；
- Query 判断 A 与 B 连通；
- 仅修改 Name/Length 不改变 Graph；
- Save/Reload 后 Segment/Connection/Terminal Stable ID 保持。

### Scenario 2: Cable Split

```text
Terminal A ─ S1/C1 ─ Intermediate Terminal X ─ S2/C2 ─ Terminal B
```

验证：

- X 不是 Device；
- X 有独立 TerminalId；
- Graph 由两条 Connection Edge 形成 A→X→B；
- 原 S0/C0 不同时作为有效边存在；
- Split Undo 恢复单段路径；
- Split Redo 恢复双段路径和相同 Stable IDs。

### Scenario 3: Split and Reconnect

```text
Before: A ─ S0/C0 ─ B
After:  A ─ S1/C1 ─ X ─ S2/C2 ─ C
```

验证：

- A→C 连通；
- B 是否可达与旧 C0 是否退场一致；
- 未参与改接的对象身份不变；
- Undo/Redo 不通过重新 Build 或随机 ID 重建结果。

### Scenario 4: Persistence Round Trip

保存并恢复普通段、拆分段和改接后的有效拓扑，验证：

- SegmentId；
- ConnectionId；
- endpoint TerminalId；
- Intermediate TerminalId；
- 电缆业务属性；
- Graph 查询结果；
- Undo/Redo 所需身份。

## 13. Non-Goals

本阶段明确排除：

- T 型电缆分支；
- 电缆分支箱；
- GIS 与电缆通道；
- 电缆路径优化；
- 三维电缆模型；
- 三相潮流、短路和保护计算；
- 自动配网分析；
- 通过电缆方向推断 Incoming/Outgoing；
- CableJoint 独立设备；
- 用户自定义电缆编辑器。

## 14. Follow-up Implementation Plan

### P0-7-C-4-D-2 Cable Segment Runtime

实现 CableSegment 最小运行时模型、Create/Remove 及 Connection 组合，保持 Graph 和 Domain 边界。

### P0-7-C-4-D-3 Cable Split/Reconnect Command

实现 Split、Reconnect 原子 Command，覆盖失败回滚、Undo/Redo 和 Stable ID。

### P0-7-C-4-D-4 Cable Persistence

根据实际 DTO 规模决定 V5 兼容扩展或 V6，完成迁移、保存恢复和 Graph round-trip 验证。

### P0-7-C-4-D-5 Cable Rendering Integration

将当前有效 CableSegment/Connection 投影到 Rendering，验证拆分和改接后的端点、标签及场景刷新。

## 15. Final Decisions

1. CableSegment 是电缆业务信息，Connection 是电气拓扑事实，二者不互相替代。
2. 第一阶段 CableSegment 通过唯一 Connection 进入 Terminal-centric Graph。
3. Intermediate Terminal 是非 Device、非 Attachment 的拓扑端点。
4. Split 将一条有效 Connection/Segment 原子替换为两条，并创建 Intermediate Terminal。
5. Reconnect 必须显式保存 before/after 端点，不允许通过名称、坐标或重新生成对象推断。
6. CableSegment 属性变化与拓扑 endpoint 变化分开处理。
7. Graph 不新增 CableSegment 专用边；它只读取 Connection。
8. 完整 CableSegment Persistence 默认倾向 V6，除非后续证明 V5 可安全扩展。
9. Cable 创建、拆分、改接和渲染均在后续切片实现，本阶段不写代码。
