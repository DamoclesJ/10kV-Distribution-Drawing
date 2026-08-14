# P0-7-C-4-D-9 Cable Persistence V6 Design

## 1. V6 目标

当前 Project Format Version 5 已能够保存现有 Device、RingCabinet、Pole、PoleAttachment、SwitchDevice、Terminal、ElectricalNode 和 Connection。D-6/D-8 新增的 CableSegment、IntermediateTerminal 及其拓扑引用尚未进入 V5 DTO 与 Migration。

因此需要 FormatVersion 6，目标是保存“当前有效的电缆业务对象和拓扑事实”，而不是保存施工历史或 Command 历史。

V6 新增持久化边界：

- `CableSegment` 集合；
- `IntermediateTerminal` 集合；
- CableSegment 与 Connection、Terminal 的稳定引用；
- IntermediateTerminal Owner 与其 Terminal 的稳定关系。

Graph 仍然是运行时查询模型，不进入工程文件。

## 2. ProjectDomainDto 设计

V6 在现有 `ProjectDomainDto` 的顶层增加两个集合：

```text
ProjectDomainDto
├── Devices
├── SwitchDevices
├── RingCabinets
├── ElectricalNodes
├── Terminals
├── IntermediateTerminals       // V6
├── Connections
├── CableSegments               // V6
├── PoleAttachments
└── OverheadLines
```

### 2.1 CableSegment DTO

建议新增 `ProjectCableSegmentDto`：

| 字段 | 来源/含义 |
| --- | --- |
| `Id` | CableSegment Stable ID |
| `DisplayName` | 对应当前 Domain `CableSegment.Name` |
| `CableType` | 电缆业务类型 |
| `Length` | 已保存的业务长度，不由加载过程计算 |
| `VoltageLevel` | 电压等级 |
| `ConnectionId` | 当前有效 Connection Stable ID |
| `StartTerminalId` | 当前有效起始 Terminal |
| `EndTerminalId` | 当前有效终止 Terminal |

DTO 字段可以叫 `DisplayName` 以保持工程文件的显示语义，但 Mapper 必须明确映射到当前 Domain 的 `Name`，不能因此新增第二个 Domain 名称事实。

### 2.2 IntermediateTerminal DTO

建议新增 `ProjectIntermediateTerminalDto`：

| 字段 | 含义 |
| --- | --- |
| `Id` | IntermediateTerminal Owner Stable ID |
| `DisplayName` | 拓扑接续点显示名称 |
| `TerminalId` | 其拥有的真实 Terminal Stable ID |

Terminal 本身仍通过顶层 `ProjectTerminalDto` 保存完整 Terminal 属性，包括 `OwnerType = intermediate-terminal`、`OwnerId = IntermediateTerminal.Id`、Role、VoltageLevel、连接策略和 `ElectricalNodeId`。IntermediateTerminal DTO 不复制这些 Terminal 字段。

### 2.3 不新增 Graph DTO

不保存 Vertex、Edge、连通查询结果或缓存索引。Graph 始终从恢复后的 Terminal、Connection 和 SwitchState 临时构建，避免把派生查询结果变成第二套持久化事实。

## 3. Terminal 持久化关系

IntermediateTerminal 的恢复关系必须是：

```text
ProjectIntermediateTerminalDto
  Id = X
  TerminalId = TX

ProjectTerminalDto
  TerminalId = TX
  OwnerType = intermediate-terminal
  OwnerId = X
```

加载时必须验证：

- `IntermediateTerminal.Id` 非空且全局唯一；
- `TerminalId` 非空且在 Terminal 集合中恰好存在；
- Terminal 的 OwnerType 为 IntermediateTerminal；
- Terminal.OwnerId 等于 IntermediateTerminal.Id；
- Terminal.ElectricalNodeId 为空；
- 一个 IntermediateTerminal 只拥有一个 Terminal；
- 一个 Terminal 不被多个 IntermediateTerminal 引用。

Persistence 必须直接使用 DTO 中的 TerminalId。禁止重新生成 Terminal ID，禁止根据 CableSegment 端点推断或补造 Terminal，也禁止调用 `IntermediateTerminalCreationFactory`。

## 4. Connection 与 CableSegment 恢复顺序

V6 Restore 应冻结为以下顺序：

```text
Project Metadata
      ↓
Devices
      ↓
Top-level SwitchDevice
      ↓
RingCabinet
      ↓
ElectricalNode
      ↓
Terminal
      ↓
IntermediateTerminal
      ↓
Connection
      ↓
CableSegment
      ↓
PoleAttachment / OverheadLine details
      ↓
Validation
```

当前 V5 Restore 已有 Devices、Top-level SwitchDevice、RingCabinet、ElectricalNode、Terminal、PoleAttachment、Connection 的历史顺序。D-10 实现 V6 时应把 IntermediateTerminal 恢复插入 Terminal 之后，并把 CableSegment 恢复放到 Connection 之后；不是修改历史 V1-V5 的事实合同。

CableSegment 必须在 Connection 之后恢复，因为 `DrawingDocument.AddCableSegment` 要验证：

- Connection 类型为 Cable；
- ConnectionId 一致；
- StartTerminalId / EndTerminalId 一致；
- VoltageLevel 一致；
- Connection 已经引用两个已注册 Terminal。

恢复 Connection 时只建立当前有效拓扑事实；恢复 CableSegment 时只建立其业务对象与 Connection 引用。最后统一验证所有引用、重复 ID、端点策略和拓扑一致性。

## 5. V5 → V6 Migration

V5 文件没有 `CableSegments` 和 `IntermediateTerminals`。V5→V6 Migration 只增加空集合：

```json
{
  "intermediateTerminals": [],
  "cableSegments": []
}
```

迁移禁止：

- 从普通 Connection 推断 CableSegment；
- 从 PoleAttachment 推断 IntermediateTerminal；
- 创建缺失 CableSegment；
- 创建缺失 Terminal；
- 生成任何 Stable ID；
- 修改已有 Connection、Terminal、ElectricalNode 或 Device。

没有 Cable 业务对象的旧 V5 工程，迁移后仍保持原有 Domain 内容。V5→V6 只是格式结构升级，不是拓扑修复或历史重建。

## 6. Round Trip 设计

### 6.1 普通 Cable

保存：

```text
Terminal A ── CableSegment S0 / Connection C0 ── Terminal B
```

恢复后验证：

- SegmentId = S0；
- ConnectionId = C0；
- StartTerminalId = A；
- EndTerminalId = B；
- CableSegment 业务字段保持；
- Graph 查询结果保持。

### 6.2 Split 后 Cable

保存：

```text
A ── Segment S1 / C1 ── IntermediateTerminal X / TX ── Segment S2 / C2 ── B
```

恢复后验证：

- IntermediateTerminalId = X；
- TerminalId = TX；
- TX 的 OwnerId = X；
- SegmentId S1、S2 保持；
- ConnectionId C1、C2 保持；
- 所有端点引用存在；
- Graph 仍能从 A 连通到 B。

### 6.3 Reconnect 后 Cable

保存：

```text
A ── CableSegment S0 / Connection C1 ── C
```

恢复后验证：

- SegmentId S0 保持；
- 新 ConnectionId C1 存在；
- 旧 C0 不出现在当前有效 Connection 集合；
- Segment 的 ConnectionId、StartTerminalId、EndTerminalId 与 C1 一致；
- Graph 反映 A-C，而不是旧 A-B。

Persistence 保存当前状态，不保存 Reconnect 的 Before 快照。

## 7. Undo/Redo 影响

V6 保存的是 Document 当前有效状态，不保存：

- Before 状态；
- After 状态快照；
- Undo 栈；
- Redo 栈；
- Command 类型；
- Command 执行历史。

应用程序加载 V6 文件后得到的是当前拓扑快照。Undo/Redo 历史属于运行时会话生命周期，不能依赖工程文件恢复。

## 8. Stable ID 策略

保存时直接写入 Domain 对象现有 ID：

- CableSegmentId；
- ConnectionId；
- IntermediateTerminalId；
- Intermediate Terminal 的 TerminalId；
- 其他 Device、Terminal、ElectricalNode 和 Attachment ID。

恢复时直接使用 DTO 中的 ID。Persistence 层禁止：

- `Guid.NewGuid()`；
- 调用 CreationFactory；
- 调用 Template Builder；
- 根据名称或数组顺序生成身份；
- 根据缺失引用推断新对象。

V6 Round Trip 的核心不变量是：保存前后的有效对象身份和端点关系完全一致；Reconnect 后保留 SegmentId 但使用新的 ConnectionId 也必须保持。

## 9. Graph 影响

V6 恢复后不需要修改 `ElectricalConnectivityGraph` 或 Query：

- Terminal 是 Graph 顶点；
- Connection 生成 Connection Edge；
- Closed Switch 生成 ClosedSwitch Edge；
- CableSegment 只是业务对象，不产生额外 Graph Edge；
- IntermediateTerminal 不产生特殊 Edge。

加载完成并通过 Domain validation 后，调用现有 Graph Builder 即可重建连通关系。Split/Reconnect 的持久化结果只要准确恢复 Terminal 和 Connection，Graph 行为自然保持。

## 10. Rendering 影响

V6 Persistence 不保存 Scene、HitTest、Symbol 或坐标。未来 Rendering 可以读取恢复后的 CableSegment 属性，用当前有效 Connection 端点重建电缆图形：

```text
Terminal A ═════════ Terminal B
```

Split 后可以投影为带中间接续标记的两段线，Reconnect 后投影新端点。Rendering 不应把显示对象写回 Persistence 核心，也不应根据渲染结果创建拓扑对象。

## 11. Migration Chain

冻结历史格式迁移链：

```text
V1 → V2 → V3 → V4 → V5 → V6
```

规则：

- V1-V5 历史迁移逻辑保持不变；
- 每个历史阶段使用明确的目标版本常量；
- V5→V6 只添加空的 CableSegments / IntermediateTerminals 集合；
- V6 文件不再继续迁移；
- 不提供 V1/V2/V3/V4/V5 到 V6 的 shortcut；
- Migration 不生成新设备、Terminal、Connection 或 Stable ID。

V5 文件中的现有 Connection 仍按既有格式恢复。只有 V6 payload 明确包含 CableSegment 和 IntermediateTerminal 时，V6 DTO/Restore 才建立这些新对象。

## 12. 测试计划

### 12.1 Migration 测试

验证 V5→V6：

- `CurrentVersion` 目标为 V6；
- 缺失字段被补为空集合；
- 不生成 CableSegment、IntermediateTerminal 或 Stable ID；
- 原 Device、Connection、Terminal 和 SwitchState 不变。

### 12.2 普通 Cable Round Trip

保存并恢复 A-S0/C0-B，验证 Segment、Connection、端点和业务字段全部保持。

### 12.3 Split Round Trip

保存并恢复 A-S1/C1-X-S2/C2-B，逐项验证 X、TX、S1、S2、C1、C2 和 Owner 引用。

### 12.4 Reconnect Round Trip

保存并恢复 A-S0/C1-C，验证 SegmentId 保持、新 Connection 存在、旧 Connection 不写回。

### 12.5 Stable ID 测试

验证所有 Cable、IntermediateTerminal、Terminal、Connection、ElectricalNode 和现有设备 ID 在 Save/Load 后保持。

### 12.6 Graph 测试

从恢复后的真实 Domain Aggregate 重建 Graph，验证普通 Cable、Split 和 Reconnect 的 Connectivity Query 结果与保存前一致。

### 12.7 严格性测试

验证缺失或不一致的 ConnectionId、TerminalId、OwnerId、重复 ID、错误 ConnectionType 和非法端点策略被拒绝；Migration 只对新增空集合保持兼容，不放宽其他结构校验。

## 13. 明确排除

V6 设计不包含：

- Cable 历史版本或施工记录；
- Split/Reconnect Command 历史；
- GIS 路径与空间坐标；
- 电缆线路路径优化；
- 自动长度计算；
- Cable Merge；
- T 型分支与分支箱；
- 潮流、短路或配网仿真；
- Rendering Scene 持久化。

## 14. D-10 实施边界

下一阶段 P0-7-C-4-D-10 才允许修改：

- `ProjectDomainDto`；
- `ProjectFileFormat`；
- `ProjectFormatMigration`；
- Domain Mapper/Restore；
- Infrastructure Round Trip Tests。

D-10 必须保持 V5 文件可读取，并以真实 `DrawingDocument` 验证 V6 Save/Load，而不是只比较 JSON 字符串。D-10 不应顺便实现新的 Cable、Graph、Rendering 或 UI 能力。

## 15. 决策摘要

FormatVersion 6 新增顶层 `CableSegments` 与 `IntermediateTerminals` DTO 集合。IntermediateTerminal 与其真实 Terminal 通过 Stable ID 和 OwnerId/OwnerType 显式关联；Connection 先恢复，CableSegment 后恢复；V5→V6 只增加空集合，不推断任何历史电缆对象。

V6 保存当前有效状态，不保存 Command 历史。所有 ID 直接保存和恢复，Graph 继续由 Terminal、Connection 和 SwitchState 重建。该设计为普通 Cable、Split 后 Cable 和 Reconnect 后 Cable 提供统一的持久化边界，同时保持 V1→V5 历史迁移链不变。
