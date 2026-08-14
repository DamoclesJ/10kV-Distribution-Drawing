# P0-7-C-4-D-4 Intermediate Terminal Domain Contract Design

## 1. 设计目标

本设计冻结 Cable Split / Reconnect 场景中 Intermediate Terminal 的 Domain 身份。典型拆分结果为：

```text
Terminal A ── CableSegment ── Intermediate Terminal X ── CableSegment ── Terminal B
```

Intermediate Terminal 用于表达电缆拓扑中的中间端点。它必须能够参与现有 Terminal-centric Graph，但不能把拓扑点误建模成设备，也不能引入第二套连接关系。

## 2. Intermediate Terminal 定位

Intermediate Terminal 是一个独立的、轻量的 Topology Element / Terminal Owner。

它不是：

- Device；
- PoleAttachment；
- Connection；
- CableSegment；
- 自动生成的 ElectricalNode。

它只表达“这里存在一个可被两段电缆引用的拓扑端点”。电气连通关系仍由 Connection 表达，电缆业务事实仍由 CableSegment 表达。

这个分类也避免了两种错误：把中间点伪装成设备，或把两段电缆之间的连通性隐含在 Owner 关系中。

## 3. 与 Terminal 的关系

候选方案如下。

### 方案 A：Intermediate Terminal 自身就是 Terminal

优点是对象数量少、Graph 映射直接。缺点是当前 Terminal 已有 `OwnerId` / `OwnerType` 合同；若它自身既是 Terminal 又是 Owner，就需要自引用或为该对象增加特殊例外，Document 生命周期和持久化边界也不清晰。

### 方案 B：Intermediate Terminal 拥有一个 Terminal

该方案保留一个独立的拓扑对象和一个真实 Terminal。Graph 只消费子 Terminal，Owner 负责生命周期。身份、恢复顺序和 Document 注册都比较清楚。

### 方案 C：Intermediate Terminal 是显式的 Terminal Owner

方案 C 采用方案 B 的对象关系，并进一步冻结其语义：Intermediate Terminal 是轻量 Topology Element，拥有且只拥有一个真实 Terminal；该 Terminal 的 `OwnerId` 指向 IntermediateTerminal 的 Stable ID。

### 最终选择：方案 C

方案 C 与当前 Owner 合同最一致，同时保留 Terminal-centric Graph：

| 关注点 | 设计决定 |
| --- | --- |
| Graph | 只把子 Terminal 作为顶点，不增加 Owner 顶点或特殊边 |
| Stable ID | IntermediateTerminalId 与 TerminalId 独立且稳定 |
| Document | Document 管理 IntermediateTerminal 生命周期，并登记其 Terminal |
| Persistence | 可以显式保存 Owner、Terminal 及其引用关系 |
| Domain 语义 | 拓扑点不是 Device，也不承担设备行为 |

第一版不引入通用 `TopologyElement` 继承体系或接口。只有在出现第二种真实的同类 Owner 后，才评估抽象公共接口的必要性。

## 4. Owner Contract

当前 Terminal 已有：

- `OwnerId`；
- `OwnerType`。

后续实现应增加一个明确的 `TopologyOwnerType.IntermediateTerminal` 值，或等价的、向后兼容的 Owner 标识。Intermediate Terminal 的子 Terminal 应满足：

```text
Terminal.OwnerType = IntermediateTerminal
Terminal.OwnerId   = IntermediateTerminal.Id
```

不采用 `OwnerId == Terminal.Id` 的自拥有方式，也不把 Intermediate Terminal 标记为 `Device` 或 `InternalAggregate`。这能让 Resolver、Persistence 和 Document 查询明确区分设备 Owner 与拓扑端点 Owner。

该 Owner 类型只表达拥有关系，不表达电气连通性。连通性仍必须通过 Connection 建立。

## 5. Stable ID 生命周期

Intermediate Terminal 至少有两个稳定身份：

- `IntermediateTerminalId`：拓扑 Owner 的身份；
- `TerminalId`：其真实 Terminal 的身份。

两者在创建时各生成一次。SplitCableCommand 的一次 Execute 应创建并保存这两个 ID，以及新建 CableSegment 和 Connection 的 ID。

Undo 应移除 Intermediate Terminal、其 Terminal、拆分产生的 Segment/Connection，并恢复拆分前的原 Segment/Connection。Redo 必须重新登记第一次 Execute 创建的同一对象或同一组不可变恢复数据，复用全部原始 ID；不得重新调用 `Guid.NewGuid()` 或重新构造一组不同身份的对象。

Reconnect 若复用同一物理 CableSegment，应遵循 D-3 的决定：保留 SegmentId、以新的 ConnectionId 表达新的端点关系，并在 Undo/Redo 中保存和恢复 Before/After 引用。

## 6. DrawingDocument 管理边界

Intermediate Terminal 由 DrawingDocument 直接管理其生命周期，但不进入 Devices 集合：

```text
DrawingDocument
├── Devices
├── IntermediateTerminals
├── Terminals
├── ElectricalNodes
├── CableSegments
└── Connections
```

`IntermediateTerminals` 保存 Owner 集合；其子 Terminal 同时登记到 Document 的全局 Terminals 集合，供 Connection、Graph 和 Resolver 使用。Intermediate Terminal 不自行管理 Connection，也不拥有 CableSegment。

Document 的添加和删除操作应保持关系完整：不能在 Owner 已登记而 Terminal 尚未登记的可观察中间状态下完成一次原子创建；删除 Owner 前应先处理引用它的 Terminal、Segment 和 Connection，或由上层原子 Command 按冻结的生命周期顺序执行。

## 7. Terminal 注册顺序

未来 Runtime 实现应按以下顺序建立对象和引用：

1. 生成 IntermediateTerminalId 与 TerminalId；
2. 构造 Intermediate Terminal Owner；
3. 构造其真实 Terminal，并设置 `OwnerType` / `OwnerId`；
4. 以原子 Document 操作登记 Owner 和 Terminal；
5. 创建引用该 Terminal 的 Connection；
6. 创建或登记新的 CableSegment。

在 Cable Split 中，推荐先构造完整的候选对象组，再由一个原子 Command 一次性提交。这样 Execute 失败时不会留下半注册的 Owner、Terminal 或 Connection。

## 8. ElectricalNode 关系

Intermediate Terminal 默认不创建、共享或拥有 ElectricalNode。

ElectricalNode 表示固定的内部导通关系，而 Intermediate Terminal 是电缆段之间的显式连接端点。典型拆分结果应依赖两条显式 Connection：

```text
A ── Connection 1 ── X ── Connection 2 ── B
```

Graph 因此能够沿两条 Connection 经过 X。删除任一段时，Graph 应反映实际断开状态；不能因为 X 的 Owner 关系自动补出 A 到 B 的隐式连通。

如果未来需要表达柜内固定接点，应由独立的 ElectricalNode 设计明确表示，不能把该语义偷偷加入 Intermediate Terminal。

## 9. Graph 行为

Graph 不需要 Intermediate Terminal 专用顶点或边类型。构建规则保持：

```text
Terminal → Connection → Graph Edge
```

Intermediate Terminal 的子 Terminal 作为普通 Terminal 顶点加入 Graph；两条 CableSegment 对应的 Connection 生成普通 Connection Edge。Graph 不读取 Owner 关系来生成边，也不新增 `IntermediateTerminalEdge`。

因此：

- A、X、B 的 Terminal 存在两条 Connection 时，A 与 B 可连通；
- 删除 A-X 段时，X 不会自动与 A 连通；
- Intermediate Terminal 不会被解释为设备、开关或自动分支点。

## 10. Persistence 边界

完整的 Intermediate Terminal 生命周期和 Cable Split 结果应进入未来 FormatVersion 6。V6 至少需要保存：

- `IntermediateTerminalId`；
- `TerminalId`；
- OwnerType / OwnerId 关系；
- Terminal 的现有结构属性；
- 引用该 Terminal 的 Connection；
- 相关 CableSegment 的端点引用。

Graph 是临时查询模型，不持久化。恢复时应先恢复 Intermediate Terminal Owner 和其 Terminal，再恢复 Connection 与 CableSegment 引用，最后执行关系校验。恢复必须使用文件中的 ID，不得由 Migration 或 Restore 重新生成 ID，也不得从旧文件推断不存在的中间点。

V1→V5 migration chain 保持不变。V5→V6 只处理确实包含 Intermediate Terminal 的新结构；没有该结构的旧 V5 文件应保持原有对象和拓扑不变，不应被强行补造 Intermediate Terminal。

## 11. Rendering 边界

未来 Rendering 可以把中间拓扑点表示为轻量连接标记，例如：

```text
A ===== ○ ===== B
```

其中 `○` 只是拓扑断点或接续标记，不是设备符号，不提供开关控制，不代表 Pole、CableTermination 或其他设备。Rendering 应继续从 Terminal、Connection 和 CableSegment 投影，不把渲染标记反向写入 Domain。

## 12. 测试设计

未来实现至少覆盖以下场景：

### Case 1：创建 Intermediate Terminal

验证 Owner 与子 Terminal 均有 Stable ID，OwnerId/OwnerType 关系正确，且对象被 Document 正确登记。

### Case 2：Split 后的 Graph

创建 A-X、X-B 两条 Connection，验证 Graph 中 A、X、B 的 Terminal 顶点存在，A 与 B 仍然连通。

### Case 3：Undo Split

验证拆分产生的 Owner、Terminal、Segment 和 Connection 被移除，原 A-B Segment/Connection 恢复，且没有悬空引用。

### Case 4：Redo Split

验证重新执行后复用第一次创建的 IntermediateTerminalId、TerminalId、SegmentId 和 ConnectionId，而不是生成新身份。

### Case 5：Persistence Round Trip

未来 V6 中保存并恢复 Owner、Terminal、端点引用和拓扑关系，验证 Stable ID 与 Graph 结果保持一致。

### Case 6：显式连接边界

删除一条 Segment 或 Connection 后，验证 Graph 不自动绕过 Intermediate Terminal 建立隐式连接；该点不应演变为 T 接或自动分支。

## 13. 非目标

本阶段及后续 Intermediate Terminal 最小实现不包含：

- 电缆分支；
- T 接；
- 电缆分支箱；
- GIS 节点；
- 三维空间节点；
- 自动拓扑优化；
- 潮流、短路或配网仿真；
- 将拓扑点提升为可操作设备。

## 14. 后续实施计划

设计冻结后的后续切片为：

1. **P0-7-C-4-D-5 Intermediate Terminal Runtime**：实现 Owner、子 Terminal、Document 注册和稳定生命周期。
2. **P0-7-C-4-D-6 Cable Split Runtime**：以原子 Command 实现旧段替换、新 Owner/Terminal、两段新 Segment/Connection，以及 Undo/Redo。
3. **P0-7-C-4-D-7 Cable Reconnect Runtime**：实现端点变更的 Before/After 记录与稳定 ID策略。
4. **P0-7-C-4-D-8 Cable Persistence**：设计并实现 V6 保存、恢复和 V5 兼容路径。
5. **P0-7-C-4-D-9 Cable Rendering**：将中间拓扑点投影为非设备型接续标记。

## 15. 决策摘要

Intermediate Terminal 最终定义为独立的轻量 Topology Element / Terminal Owner，不是 Device、Attachment 或 Connection。它拥有一个真实 Terminal，Terminal 通过明确的 OwnerType/OwnerId 指向该 Owner；Graph 只读取该 Terminal 和显式 Connection；默认不创建 ElectricalNode；Document 管理 Owner 和全局 Terminal；完整生命周期进入未来 V6 Persistence。

该方案解决了“没有设备属性的拓扑点在系统中是谁”的问题，同时保持现有 Terminal-centric Graph、CableSegment/Connection 边界和 Command Undo/Redo 模型不变。
