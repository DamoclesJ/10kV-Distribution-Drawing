# P0-7-C-4-D-3 Cable Split / Reconnect Design

> 状态：领域模型与 Command 设计；本阶段只冻结拆分、接续和改接边界，不修改生产代码、测试、Persistence 或 UI。
> 目标：在保持 Terminal-centric Graph 不变的前提下，支持工作票中的电缆拓扑施工变更。

## 1. Context and Design Goal

当前电缆运行时已经能够表达：

```text
Terminal A ─ CableSegment S0 / Connection C0 ─ Terminal B
```

本阶段设计两个后续能力：

- Cable Split：在既有电缆中间形成新的拓扑端点，将一段拆为两段；
- Cable Reconnect：将既有电缆从一个明确 Terminal 改接到另一个明确 Terminal。

这些能力服务于 10kV 工作票绘图中的施工前后拓扑表达、操作范围和连通性验证。它们不是电缆制造模拟、GIS 施工管理或真实施工过程记录。

设计必须继续保持：

- `CableSegment` 表示电缆业务信息；
- `Connection` 表示两个 Terminal 之间的电气拓扑事实；
- `ElectricalConnectivityGraph` 只从 Connection 生成 Connection Edge；
- Device、Terminal、Connection 和 Segment 的生命周期由文档/Command 协调；
- Undo/Redo 使用固定 before/after 对象和 Stable ID，不重新猜测或随机重建。

## 2. Frozen Terminology

### 2.1 CableSegment

CableSegment 是连接层业务对象，不是 Device，不拥有 Terminal，也不直接参与 Graph。它引用一个有效的 Cable `Connection`，并保存电缆名称、类型、长度、电压等级等业务属性。

### 2.2 Connection

Connection 是 Domain 的电气连接事实，具有自己的 Stable ID 和两个 endpoint Terminal。Graph 只读取 Connection；CableSegment 不得成为第二套拓扑边。

### 2.3 Intermediate Terminal

Intermediate Terminal 是电缆拆分或接续过程中的拓扑端点：

- 不是 Device；
- 不是 PoleAttachment；
- 不表示电缆分支箱或 CableJoint 设备；
- 有自己的 Stable TerminalId；
- 通过 Connection 参与 Graph；
- 不因为存在就自动连通两侧。

## 3. Cable Split Scenario

### 3.1 Before

```text
Terminal A
    │
CableSegment S0
    │
Connection C0
    │
Terminal B
```

### 3.2 After

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

拆分后有效 Graph 只有 C1、C2 两条 Connection Edge，经 X 形成 A→B 路径。C0 不得继续作为当前有效边，否则查询会同时得到旧路径和新路径。

### 3.3 Split Facts

Split 至少改变以下当前工程事实：

- 原 S0/C0 的有效生命周期结束；
- 新建 X；
- 新建 S1/C1 和 S2/C2；
- A/B 既有 Terminal 和所属 Device 不变；
- 新 Segment 的业务属性按明确规则继承或由调用方提供；
- Graph 从一条边变为两条边。

拆分不是新增设备，不创建 Pole、CableTermination 或 SwitchDevice。

## 4. CableSegment Stable ID Strategy

### 4.1 Option A: Retire Old Segment and Create Two New Segments

```text
S0/C0 退场
S1/C1 新建
S2/C2 新建
```

优点：

- 语义最直接：一段物理业务事实被替换为两段新事实；
- 不需要判断哪一段“继承”旧段；
- Undo/Redo 的 before/after 集合清晰；
- 不会把旧段名称、长度、历史属性错误地绑定到其中一段；
- 当前不可变 CableSegment API 可以自然支持。

缺点：

- 原 SegmentId 不会延续到新段；
- 若需要施工历史，必须由后续历史模型保存 S0 与 S1/S2 的来源关系。

### 4.2 Option B: Preserve Original Segment for One Side

例如保留 S0 作为 A→X，另建 S2 作为 X→B。

优点：

- 一部分业务记录可以保持 SegmentId；
- 可能更接近“原电缆被截断后继续使用”的现场叙述。

缺点：

- 需要定义哪一侧继承；
- 长度、型号、名称和资产属性可能被错误继承；
- 不同切割位置会产生不同身份结果；
- History、Persistence 和 UI 容易把“继承”误认为“同一完整电缆”；
- Undo/Redo 需要处理部分对象重用和部分对象新建。

### 4.3 Frozen Decision

第一版 Split 选择方案 A：

> 原 Segment 退场；新建两个 Segment；新 Segment 各自拥有新的 Stable ID 和 ConnectionId。

原因是当前目标首先保证拓扑和命令语义可审查，SegmentId 是对象身份而不是自动生成的历史谱系。若未来需要“由 S0 拆出 S1/S2”的历史关系，应增加显式施工历史/来源元数据，不改变当前有效对象身份策略。

## 5. Intermediate Terminal Boundary

### 5.1 Ownership

Intermediate Terminal 应由 `DrawingDocument` 管理，原因是它是工程拓扑事实，不是设备附属物：

- 不属于任意 Pole 或 RingCabinet；
- 不应伪造一个 Device 作为 Terminal owner；
- 不应作为 Attachment 挂到任意设备上；
- 需要参与工程级 ID 唯一性、Connection 生命周期和 Save/Reload。

当前 Domain 的 `Terminal` 需要 `TopologyOwnerType` 和 `OwnerId`。现有 owner 类型主要面向 Device/InternalAggregate，因此 D-4 实现前必须增加一个明确的文档/拓扑 owner 合同，例如 `Document` owner 类型，或等价的专用拓扑 owner 机制。不能用随机 DeviceId 或虚构 CableJoint 绕过该缺口。

### 5.2 Stable ID

X 必须拥有独立且稳定的 TerminalId：

- Execute 创建一次；
- Undo 移除该 Terminal；
- Redo 恢复第一次创建的同一 TerminalId；
- Persistence 保存并恢复该 TerminalId；
- Graph 直接把 X 加入 Vertex 集合。

X 不生成 DeviceId、AttachmentId 或 ElectricalNodeId。

### 5.3 Connectivity Semantics

X 参与 Graph 的方式与其他 Terminal 相同：

- C1 形成 A↔X Connection Edge；
- C2 形成 X↔B Connection Edge；
- 没有 C1/C2 时 X 是孤立 Vertex；
- 删除一侧 Segment 后，不自动把另一侧接到任何新端点；
- 如果命令策略规定无连接的 X 不再是有效当前事实，命令应在同一原子操作中删除 X。

第一版推荐：Intermediate Terminal 只由拥有它的 Split/接续 Command 管理；当其关联的有效 CableSegment 数量降为零时一并移除，避免产生无业务意义的孤立拓扑点。

## 6. SplitCableCommand Design

### 6.1 Command Inputs

SplitCableCommand 至少需要：

- 目标原 SegmentId；
- 拆分位置或新段业务属性输入；
- 新 Intermediate Terminal 的稳定身份策略；
- 新 Segment 的 CableType、Length、VoltageLevel 等明确业务参数；
- 必要时新段名称。

命令不接受坐标作为唯一拓扑依据，不从画布交点自动寻找 A/B，也不创建任何 Device。

### 6.2 Execute Contract

Execute 的逻辑边界：

1. 保存原 S0、C0、A、B 和原业务属性；
2. 校验目标 Segment/Connection 的一致性；
3. 校验 A、B 仍然存在且连接规则有效；
4. 构造 X、S1/C1、S2/C2 的候选对象；
5. 通过 Document 受控 API 移除 S0/C0；
6. 注册 X 和新 Connection/Segment；
7. 验证新拓扑关系；
8. 任一步失败则恢复完整 before 状态。

实际实现可以使用事务式 Domain 方法或 Command 级回滚，但不能让外部观察到只删除旧段、尚未创建新段的半完成状态。

### 6.3 Undo and Redo

Undo：

- 删除 S1/C1、S2/C2；
- 删除 X；
- 恢复原 S0/C0 及其 A/B endpoint；
- 恢复原业务属性。

Redo：

- 再次移除 S0/C0；
- 恢复第一次 Execute 创建的 X、S1/C1、S2/C2 对象和 Stable IDs；
- 不调用随机 ID 生成器重新创建等价对象；
- 不重新构建 Template；
- 不修改未参与操作的 Device、Terminal 或 Connection。

## 7. Cable Reconnect Scenario

### 7.1 Before and After

原连接：

```text
Terminal A ─ CableSegment S0 / Connection C0 ─ Terminal B
```

改接后：

```text
Terminal A ─ CableSegment S0 / Connection C1 ─ Terminal C
```

其中 C 必须是调用方明确选择且已存在的合法 Terminal。不得根据距离、名称、方向或最近设备自动选择 C。

### 7.2 Reconnect Is a Topology Mutation

Reconnect 不是普通 Name、Length 或 CableType 属性修改，因为它改变：

- Connection endpoint；
- Graph Connection Edge；
- Terminal 的连接占用；
- 工作票施工前后路径。

因此必须使用独立的 ReconnectCableCommand，并通过 Domain 的受控文档 API 完成原子替换。

### 7.3 Identity Decision

Reconnect 与 Split 的身份规则不同：

- 如果现场语义是同一物理电缆段仍然存在，只是 endpoint 改接，则保留 CableSegmentId；
- 由于当前 Connection 是不可变连接事实，创建新的 ConnectionId；
- 用同一 SegmentId 的 after CableSegment 对象引用新的 ConnectionId；
- 原 Segment/Connection 对象作为 before 快照保存在 Command 中；
- 如果现场语义是电缆被切割、长度/物理段发生变化，则不使用 Reconnect 伪装，应改用 Split/新 Segment 语义。

该策略保留业务段身份，同时让 Connection 身份准确反映 endpoint 事实。实现前需要允许 Document 原子地移除旧 Segment/C0 并注册相同 SegmentId 的 after Segment/C1；不能把两个同 ID Segment 同时放入文档。

## 8. ReconnectCableCommand Design

### 8.1 Saved Before/After State

Command 必须保存完整 before/after：

| 状态 | SegmentId | ConnectionId | StartTerminalId | EndTerminalId |
| --- | --- | --- | --- | --- |
| Before | S0 | C0 | A | B |
| After | S0 | C1 | A | C |

同时保存 Segment 业务属性和必要 Terminal 连接占用信息。命令不只保存一个“新终点”，否则 Undo 无法可靠恢复原连接。

### 8.2 Execute/Undo/Redo

Execute：

1. 校验 Before Segment/C0 当前仍存在；
2. 校验 A、C 的 Connection policy；
3. 移除旧 Segment/C0；
4. 注册 after Segment/C1；
5. 验证当前文档。

Undo：

- 移除 after Segment/C1；
- 恢复 before Segment/C0。

Redo：

- 再次移除 before Segment/C0；
- 恢复同一个 after Segment/C1 对象和 Stable IDs。

任何失败都不得留下旧、新两组 Segment 同时有效的状态。

## 9. Connection Lifecycle

### 9.1 Split

Split 采用：

```text
Remove S0/C0
Create S1/C1
Create S2/C2
```

不原地修改 C0 的 endpoints。原因是当前 Connection 是稳定的不可变拓扑事实，原地修改会削弱 Undo/Redo、引用和历史审查能力。

### 9.2 Reconnect

Reconnect 采用：

```text
Remove before Segment/C0
Create after Segment/C1
```

SegmentId 是否相同由“是否仍为同一物理电缆段”决定；ConnectionId 新建以反映新的 endpoint。Graph 不需要特殊 CableSegment 逻辑：

- 旧 C0 消失，旧 Edge 消失；
- 新 C1 出现，新 Edge 出现；
- Query 从新的 Document snapshot 得到新连通结果。

### 9.3 Direct Connection Removal

只要 Segment 仍引用 Connection，普通 `RemoveConnection` 不得绕过 Segment 删除它。Split/Reconnect 应使用专用受控 Command/Document API，保证业务对象和拓扑事实同步。

## 10. Persistence Impact

Split/Reconnect 完成后，当前有效工程需要保存：

- CableSegment 的有效身份和业务属性；
- 当前 ConnectionId 与 endpoint TerminalIds；
- Intermediate TerminalId 和 owner 合同；
- 未退场的 Segment/Connection 集合；
- 其他 Device、Terminal、ElectricalNode 和 Attachment 的 Stable IDs。

Graph 顶点、边和查询结果不持久化。加载后由有效 Domain 事实重建 Graph。

完整支持 Intermediate Terminal、CableSegment 集合和拆分/改接后的有效拓扑，建议进入 FormatVersion 6：

- 保持 V1→V5 migration chain 不变；
- 增加 V5→V6 迁移；
- 旧 V5 文件没有 CableSegment 事实时，不伪造 Segment 或 Intermediate Terminal；
- 旧 Connection 继续按原 Connection 语义读取；
- V6 保存不写回已退场的旧 Segment/Connection；
- Migration 不生成随机 ID、不推断新端点、不创建 CableJoint。

如果未来证明只增加可选 CableSegment 明细且旧读取器能安全忽略，才可重新评估 V5 扩展；当前默认不把拆分/改接隐式塞入 V5。

## 11. Rendering Boundary

未来 Rendering 只显示当前有效拓扑：

原始电缆：

```text
A ═════════════════════ B
```

拆分后：

```text
A ═══════════ X ═══════════ B
```

X 可以显示为断点或接续标记，但不是设备符号。Rendering 不负责：

- 创建 X；
- 根据几何相交拆分 Cable；
- 生成 Segment/Connection ID；
- 修改 endpoint；
- 保存施工历史。

## 12. Test Scenario Design

### Scenario 1: Ordinary Split

```text
A ─ Cable S0/C0 ─ B
          ↓ Split
A ─ S1/C1 ─ X ─ S2/C2 ─ B
```

验证：

- S0/C0 不再是当前有效对象；
- X 是非 Device Terminal；
- A→X→B 通过两条 Connection Edge 连通；
- Undo 恢复 A→B；
- Redo 恢复 A→X→B，并保持 after IDs。

### Scenario 2: One Split Segment Removed

```text
A ─ S1/C1 ─ X
X ─ S2/C2 ─ B
```

删除一段后，另一段不会自动跨越 X 连接到 B/A：

- 删除 S2/C2 后，A 与 B 不应继续通过隐式边连通；
- X 是否保留取决于命令生命周期，第一版推荐无有效 Segment 引用时删除；
- Graph 只反映剩余显式 Connection。

### Scenario 3: Reconnect

```text
Before: A ─ S0/C0 ─ B
After:  A ─ S0/C1 ─ C
```

验证：

- Reconnect 被识别为拓扑命令；
- B 侧旧 Connection Edge 消失；
- C 侧新 Edge 出现；
- SegmentId 按同一物理段策略保持；
- ConnectionId 按新 endpoint 新建；
- Undo/Redo 恢复 before/after。

### Scenario 4: Persistence Round Trip

V6 round-trip 需要验证：

- 有效 SegmentId；
- ConnectionId；
- A/B/C/X TerminalId；
- Segment 业务属性；
- 当前有效 Connection 集合；
- Graph 查询结果；
- Split/Reconnect 后的 before 不会错误写回当前文件；
- Stable ID 不因 Reload 重新生成。

## 13. Non-Goals

本阶段明确排除：

- T 型电缆分支；
- 电缆分支箱；
- 自动寻找最佳接线；
- 自动选取最近 Terminal；
- GIS 路径和三维施工模型；
- 电缆制造与资产管理；
- 三相潮流、短路和保护计算；
- CableJoint 独立设备；
- 用户自定义电缆编辑器。

## 14. Follow-up Implementation Plan

### P0-7-C-4-D-4 Cable Split Runtime

先解决 Intermediate Terminal owner 合同，实现 Split 的 Domain 注册、原子替换、Graph 验证和失败回滚。

### P0-7-C-4-D-5 Cable Reconnect Runtime

实现 before/after Connection 替换、SegmentId 策略、Undo/Redo 和端点占用验证。

### P0-7-C-4-D-6 Cable Persistence

设计并实现 V5→V6，保存有效 Segment、Intermediate Terminal、Connection 和迁移兼容。

### P0-7-C-4-D-7 Cable Rendering Integration

显示当前 CableSegment 和 Intermediate Terminal，验证拆分/改接后的 Scene 更新，不让 Rendering 反向修改拓扑。

## 15. Decision Summary

1. Split 是拓扑替换，不是属性修改。
2. Split 采用旧段退场、新 Segment 新 Stable ID 的方案 A。
3. Reconnect 是拓扑命令；同一物理段可保留 SegmentId，但 ConnectionId 因 endpoint 变化而新建。
4. Intermediate Terminal 是 Document 管理的非 Device、非 Attachment 拓扑端点。
5. 当前 Domain 的 Terminal owner 合同需要在 D-4 实现前增加明确的文档/拓扑 owner 语义。
6. Split/Reconnect 通过移除旧 Connection、创建新 Connection 表达，不原地修改 Connection endpoint。
7. Graph 只读取当前有效 Connection，不保存历史边，也不生成 CableSegment 专用边。
8. 完整 Persistence 默认进入 V6，保持 V1→V5 兼容链。
9. 后续 Command 必须原子、可 Undo/Redo，并保持未参与对象的 Stable ID。
