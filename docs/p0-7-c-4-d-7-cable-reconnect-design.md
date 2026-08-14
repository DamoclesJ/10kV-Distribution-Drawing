# P0-7-C-4-D-7 Cable Reconnect Design

## 1. 设计目标

Cable Reconnect 表达工作票中的明确电缆端点改接：

```text
Before: Terminal A ── CableSegment S0 / Connection C0 ── Terminal B
After:  Terminal A ── CableSegment S0 / Connection C1 ── Terminal C
```

它描述工程当前有效拓扑的变化，不记录施工过程，不模拟 GIS 路径，也不修改电缆制造属性。新端点 C 必须由调用方明确提供并且已经存在于 `DrawingDocument`；系统不得根据距离、名称、方向或最近设备自动选择端点。

当前代码边界已经明确：`CableSegment` 和 `Connection` 的关键属性均为不可变属性，现有 Document API 通过 `RemoveCableSegment` / `AddCableSegment` 维护二者的一致性。因此 Reconnect 不是对任意对象属性的直接赋值，而是一次受控的拓扑替换。

## 2. CableSegment 生命周期

### 2.1 方案 A：保留 CableSegment

Reconnect 后保留原 `CableSegment.Id`，但用一个新的 after Segment 快照引用新的 `ConnectionId` 和端点：

| 状态 | SegmentId | ConnectionId | StartTerminalId | EndTerminalId |
| --- | --- | --- | --- | --- |
| Before | S0 | C0 | A | B |
| After | S0 | C1 | A | C |

该方案的优点是同一物理电缆段的业务身份连续，符合“同一根电缆改接到另一个端点”的工作票语义，也避免无意义地改变 CableSegment Stable ID。

### 2.2 方案 B：删除旧 Segment 并创建新 Segment

该方案实现简单，但会改变电缆业务对象身份，使改接和“新建一根电缆”难以区分，并削弱 Undo/Redo 与后续历史审查能力。

### 2.3 最终选择

选择方案 A。保留 `CableSegment.Id`，保留原有业务字段 `Name`、`CableType`、`Length`、`VoltageLevel`，只在 after 快照中替换 `ConnectionId`、`StartTerminalId` 和 `EndTerminalId`。

这不表示允许两个同 ID Segment 同时存在。执行替换时，before Segment 必须先从 Document 中移除，after Segment 才能以相同 SegmentId 登记；Undo/Redo 也必须遵守这个互斥生命周期。

如果现场语义是电缆被切割、物理段长度发生变化或产生多个独立电缆段，则应使用已经冻结的 Split 语义，而不能用 Reconnect 伪装。

## 3. Connection 生命周期

`Connection` 表示两个 Terminal 之间当前有效的电气拓扑事实。Reconnect 不原地修改旧 Connection 的端点，而是：

```text
Remove Connection C0
Create Connection C1(A, C)
Register after CableSegment S0(C1)
```

原因是 Connection 为不可变事实；保留旧 C0 的对象快照能够让 Undo 恢复完整 Before 状态，也避免外部引用在原地变异后无法审查。

因此：

- C0 在改接完成后不再属于当前有效 Document；
- C1 是新的 Connection Stable ID；
- CableSegmentId 保持不变；
- Graph 下次从 Document 重建时自然移除旧 Edge 并加入新 Edge；
- Reconnect 不增加专用 Graph Edge 类型。

## 4. ReconnectCableCommand 设计

未来新增 `ReconnectCableCommand`，最小输入为：

- `CableSegmentId`；
- `NewStartTerminalId`；
- `NewEndTerminalId`。

Command 创建或首次 Execute 前应解析当前 Segment 与 Connection，并保存完整 Before/After 状态：

```text
Before:
  SegmentId = S0
  ConnectionId = C0
  StartTerminalId = A
  EndTerminalId = B
  Name / CableType / Length / VoltageLevel = original values

After:
  SegmentId = S0
  ConnectionId = C1
  StartTerminalId = A'
  EndTerminalId = C'
  Name / CableType / Length / VoltageLevel = original values
```

输入允许只改变一端，也允许两端都由调用方明确指定；不应默认为“保留哪一端”或根据方向推断。实现时应禁止新旧端点相同、两端相同以及无法通过 Document Terminal/Connection policy 校验的组合。

## 5. Execute / Undo / Redo

### 5.1 Execute

Execute 应作为一次原子操作：

1. 校验当前 Document 中存在指定 Segment 和其 C0；
2. 校验 Segment 与 C0 的 ConnectionId、端点、类型和电压等级一致；
3. 校验新端点存在且满足 Cable Connection policy；
4. 预先构造 C1 和 after Segment，生成各自 Stable ID 一次；
5. 从 Document 移除 before Segment/C0；
6. 注册 after Segment/C1，保留 SegmentId；
7. 若任一步失败，恢复完整 Before 状态，Document 不得留下半个改接结果。

当前已有 `RemoveCableSegment` / `AddCableSegment`，D-7 实现应在 Document 增加一个受控的原子替换辅助方法，或提供等价的事务边界。调用方不应让 `CableSegment` 自己修改 Connection。

### 5.2 Undo

Undo 应：

1. 移除 after Segment/C1；
2. 以原 SegmentId 重新登记 Before Segment/C0；
3. 恢复 A-B 的当前 Graph 结果。

Undo 不生成任何新 ID，也不重新查找或推断端点。

### 5.3 Redo

Redo 应复用首次 Execute 已构造的 after Segment 和 C1，重复相同的 Document 替换。禁止重新调用 Builder、重新生成 SegmentId 或 ConnectionId。

## 6. 与 Cable Split 兼容

Split 后可以出现：

```text
A ── CableSegment S1 ── X ── CableSegment S2 ── B
```

如果将 S2 改接到 C，结果为：

```text
A ── CableSegment S1 ── X    CableSegment S2 ── C
```

S1、S2 的 SegmentId 均保持；只有被改接的 S2 替换其 Connection。X 仍由 S1 引用，因此 Reconnect 不得自动删除 X，也不得自动把 X 与 C 建立隐式连接。若某个 Intermediate Terminal 已无任何 Segment 引用，是否清理应由独立生命周期策略决定，不属于 Reconnect 的自动拓扑推断。

Reconnect 不能把两个 Segment 合并，也不能把 X 变成 T 型分支。需要合并时，应定义独立的业务 Command。

## 7. Domain 边界

Document 应负责：

- 检查 Segment 与 Connection 的一致性；
- 检查新 Terminal 存在及其 Connection policy；
- 维护 Segment、Connection 和 Terminal 的引用关系；
- 保证 before/after 替换的原子性；
- 阻止 Segment 仍引用时直接删除 Connection。

建议 D-7 增加语义明确的 `ReplaceCableSegmentConnection` 或等价内部原子 API。该 API 可以接收 before/after Segment 与 Connection，但不应暴露任意拓扑篡改入口。

`CableSegment` 仍是不可变业务对象，不管理 Document，不直接修改 Connection。`Connection` 仍是不可变拓扑对象，不知道 CableSegment 的历史。

## 8. Graph 影响

Graph 无需修改，仍保持：

```text
Terminal → Connection → Graph Edge
```

Reconnect 前构建 Graph 得到 A-B Connection Edge；Reconnect 后重新构建 Graph：

- C0 Edge 消失；
- C1 Edge 出现；
- A-C 的查询可用；
- B 是否与 A 断开取决于是否存在其他路径，测试应使用没有替代路径的最小场景。

Graph 不读取 CableSegment 的 Name、Length 或 CableType，也不缓存旧查询结果。每次 Document 拓扑变化后由调用方重新构建查询 Graph。

## 9. Persistence 影响

Reconnect 完成后，当前有效工程必须能够保存：

- 保持不变的 CableSegmentId；
- 新的 ConnectionId；
- 新的 StartTerminalId / EndTerminalId；
- 原有 CableSegment 业务字段；
- 仍有效的 Terminal、IntermediateTerminal 及其他拓扑对象。

完整支持 CableSegment 集合、Intermediate Terminal 和 Split/Reconnect 后有效拓扑，仍建议进入 FormatVersion 6。V1→V5 migration chain 保持不变；V5→V6 只转换明确存在的结构，不生成新的端点、Segment 或 Connection，也不推断改接历史。

Graph 和 Query 结果不持久化。加载后根据当前有效 Connection 重建 Graph。已退场的 C0 不应在 V6 保存中写回当前有效集合。

## 10. 测试设计

### Case 1：Reconnect 成功

构造 A-S0/C0-B，执行改接到 C，验证旧 C0 消失、C1 存在、当前 SegmentId 保持。

### Case 2：Stable ID

记录 SegmentId、Before ConnectionId 和 After ConnectionId，验证 SegmentId 不变、C0 与 C1 不同，且 C1 在 Redo 后仍是同一个 ID。

### Case 3：Undo

验证 after Segment/C1 移除，A-B 的原 Segment/C0 恢复，Name、CableType、Length、VoltageLevel 不变。

### Case 4：Redo

验证再次得到 A-C，复用第一次 Execute 的 C1，不重新生成 Connection。

### Case 5：Graph Query

在无替代路径的场景中验证：

- 改接前 A-B 为 Connected；
- 改接后 A-C 为 Connected；
- 改接后 A-B 为 Disconnected；
- Undo/Redo 后查询结果分别恢复 Before/After。

### Case 6：非法 Reconnect

使用不存在的 Terminal、不存在的 Segment、相同端点、端点策略不允许的组合，验证命令在修改前失败，Document 的 Segment、Connection、Terminal 数量和 Stable ID 均不变化。

### Case 7：Split 后 Reconnect

先构造 A-S1-X-S2-B，再将 S2 改接到 C，验证 S1、X 和 S2 的 SegmentId 保持，且没有生成额外 Connection 或隐式分支。

## 11. Rendering 边界

未来 Rendering 只投影当前有效端点：

```text
Before: A ═════════ B
After:  A ═════════ C
```

Reconnect 不要求新的设备符号，不改变 CableSegment 的业务显示属性，不把 C0 的历史线条留在 Scene 中。Rendering 应在 Document/Scene rebuild 后消费新的 Connection 端点；本阶段不实现 UI、拖拽或鼠标改接。

## 12. 非目标

本设计不包含：

- Cable Split；
- Cable Merge；
- T 型分支；
- 电缆分支箱；
- GIS 路径调整；
- 自动规划线路；
- 根据电源方向或距离自动选择端点；
- 潮流、短路或配网仿真；
- Rendering/UI 实现。

## 13. 后续实施计划

1. **P0-7-C-4-D-8 Cable Reconnect Runtime**：实现 Before/After 快照、受控 Document 原子替换和 `ReconnectCableCommand`。
2. **P0-7-C-4-D-9 Cable Persistence V6**：保存有效 CableSegment、Connection、Intermediate Terminal 及端点引用。
3. **P0-7-C-4-D-10 Cable Rendering**：根据当前有效 Connection 投影改接后的电缆端点。

## 14. 决策摘要

Reconnect 选择“保留 CableSegment、替换 Connection”的方案：同一物理电缆段保持 SegmentId，旧 Connection 退场并创建新的 ConnectionId，after Segment 以相同 SegmentId 引用新 Connection。Execute、Undo、Redo 使用固定 Before/After 对象，不原地修改 Connection，不重新生成已有身份。

该方案与已完成的 Cable Segment Runtime、Intermediate Terminal Runtime、Cable Split Runtime 兼容，Graph 无需增加特殊逻辑；后续只需补充 Document 的受控原子替换边界和 V6 持久化支持。
