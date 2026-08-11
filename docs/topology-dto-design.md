# M4-B-6-B Topology 持久化设计

> 文档状态：设计稿，仅定义拓扑 DTO 与恢复方案，不实现代码<br>
> 编制日期：2026-08-11<br>
> 依据：`docs/domain-dto-design.md`、当前 Domain DTO 实现与当前 Domain 拓扑模型

## 1. 目标与范围

本设计补充 M4-B-6-A 尚未覆盖的外部电气拓扑持久化，定义以下对象的 DTO、稳定引用、恢复顺序和完整性校验：

- `Connection`；
- `OverheadLine`；
- `CableTermination`；
- 顶层 `Terminal` 与 `ElectricalNode` 的关联。

环网柜内部 `Terminal`、`ElectricalNode` 已由 `RingCabinetDto` 聚合保存，本设计不改变其恢复方式。`PoleAttachment` 不是本次新增的核心 DTO，但架空线路端点的物理位置校验依赖它，因此恢复架空线路前必须已经恢复相关附属关系。

本阶段只形成设计，不修改 Domain、Infrastructure、Rendering、Layout 或工程文件版本。

## 2. 当前实现边界

M4-B-6-A 当前已经支持：

- `DrawingDocument`、`Pole`、基础 `Device` 和 `RingCabinet` 的 DTO 映射；
- 环网柜内部间隔、开关、节点和端子的稳定 ID 恢复；
- `ProjectService` 将 `domain` 区写入和读出 `document.json`；
- 工程 ID、标题和环网柜固定拓扑的一致性检查。

当前 `ProjectDomainDto` 尚未保存顶层节点、顶层端子、外部连接、架空线路明细和电缆终端。因此含有这些对象的工程不能在当前实现中完成无损往返。本设计作为后续实现合同，不把这些数据临时塞入 Rendering 或 Layout。

## 3. 持久化边界与单一事实源

### 3.1 Domain 根区扩展

`ProjectDomainDto` 的逻辑结构扩展为：

```text
ProjectDomainDto
├─ documentId
├─ title
├─ devices[]                 # Pole、基础 Device、CableTermination 等顶层设备
├─ ringCabinets[]            # 环网柜完整聚合
├─ electricalNodes[]         # 仅非环网柜内部节点
├─ terminals[]               # 仅非环网柜内部端子
├─ poleAttachments[]         # 架空线路物理端点校验的前置关系
├─ connections[]             # 全部外部电气连接
└─ overheadLines[]           # OverheadLine Connection 的一对一明细
```

对象只能保存一次：

- 环网柜内部节点和端子只保存在所属 `RingCabinetDto`，不得复制到根集合；
- 电缆终端只在 `devices[]` 保存设备事实，其节点和端子完整内容分别在根 `electricalNodes[]`、`terminals[]` 保存；
- `ElectricalNode.TerminalIds` 是可重建的反向索引，不进入 DTO；
- `OverheadLine` 与 `Connection` 共用 `connectionId`，不产生第二个业务对象 ID；
- `SupportPoleIds` 只保存物理经过顺序，不创建 Terminal、ElectricalNode 或额外 Connection。

### 3.2 稳定编码

文件继续使用稳定字符串，不保存 C# 枚举整数：

| Domain 值 | DTO 编码 |
| --- | --- |
| `ConnectionType.Cable` | `cable` |
| `ConnectionType.OverheadLine` | `overhead-line` |
| `ElectricalNodeType.Intermediate` | `intermediate` |
| `TopologyOwnerType.Device` | `device` |
| `TopologyOwnerType.InternalAggregate` | `internal-aggregate` |
| `ElectricalState.Energized` | `energized` |
| `ElectricalState.Deenergized` | `deenergized` |
| `ContinuationState.Energized` | `energized` |
| `ContinuationState.Unknown` | `unknown` |

未知编码必须经版本迁移处理或拒绝加载，不能按枚举默认值恢复。

## 4. Connection DTO

### 4.1 字段定义

`ProjectConnectionDto` 保存：

| 字段 | 类型 | 规则 |
| --- | --- | --- |
| `connectionId` | Guid | 非空，工程级全局唯一 |
| `connectionType` | string | 当前仅允许 `cable`、`overhead-line` |
| `startTerminalId` | Guid | 非空，引用已注册 Terminal |
| `endTerminalId` | Guid | 非空，引用已注册 Terminal，且不得等于起点 |
| `displayName` | string | 非空白 |
| `voltageLevel` | string | 非空白，当前拓扑应为 `10kV` |

`startTerminalId` 和 `endTerminalId` 只是连接方向的稳定记录，不用于潮流方向或送电方向推导。保存后再次打开必须保持原顺序，不按 ID、坐标或设备类型重新排序。

### 4.2 连接恢复规则

创建 `Connection` 前必须解析两个端子，并验证：

- 两个端子存在且 ID 不同；
- 两个端子均为 `isExternal = true`；
- 两端 `allowedConnectionTypes` 均包含该连接类型；
- 两端电压等级与 Connection 电压等级兼容；
- 对 `allowsMultipleConnections = false` 的端子，不能已有其他 Connection；
- Connection 只能表达设备之间的外部连接，不能替代 Terminal 到 ElectricalNode 的固定内部连接。

恢复器通过 `DrawingDocument.AddConnection` 注册连接，让现有 Domain 规则再次校验。DTO 预校验用于提供清晰错误，不替代 Domain 校验。

### 4.3 重复 Connection 的定义

以下情况直接拒绝：

- `connectionId` 与任意已注册对象 ID 重复；
- 同一 `connectionId` 在 `connections[]` 出现多次；
- 非多连接端子被两条或更多 Connection 引用；
- 同一个 OverheadLine Connection 出现多份 `OverheadLineDto`。

当前 Domain 未规定“相同类型且端点对相同”在两个端子都允许多连接时必然非法，因此本阶段不新增该业务规则。此类数据仍需通过现有端子容量和 Domain 规则判断；后续如专业规范确认禁止，再增加明确校验和版本迁移。

## 5. Terminal 与 ElectricalNode DTO

### 5.1 顶层 Terminal DTO

沿用 `ProjectTerminalDto` 字段：

- `terminalId`；
- `ownerType`；
- `ownerId`；
- `role`；
- `voltageLevel`；
- `isExternal`；
- `allowsMultipleConnections`；
- `electricalNodeId`；
- `allowedConnectionTypes[]`。

根 `terminals[]` 只保存非环网柜内部端子，包括 Pole 的架空锚点和 CableTermination 的两侧端子。环网柜外部端子仍从对应聚合恢复，但完成后必须进入工程级 Terminal ID 注册表，供 Connection 引用。

### 5.2 顶层 ElectricalNode DTO

沿用 `ProjectElectricalNodeDto` 字段：

- `nodeId`；
- `nodeType`；
- `ownerType`；
- `ownerId`；
- `electricalState`。

根 `electricalNodes[]` 只保存非环网柜节点。节点 DTO 不保存 `terminalIds[]`，恢复时按 `TerminalDto.electricalNodeId` 调用 `DrawingDocument.AddTerminal`，由 Domain 建立 `ElectricalNode.TerminalIds` 反向索引。

### 5.3 所有者和节点引用规则

Terminal 恢复前必须验证：

- `ownerType = device` 时，`ownerId` 指向已恢复 Device；
- `ownerType = internal-aggregate` 时，`ownerId` 指向已恢复的 Interval 等内部聚合对象；
- `electricalNodeId` 存在时，目标节点已经创建；
- Terminal 所有者与目标节点所有者符合对应设备合同；
- 内部 Terminal 不得声明 `allowedConnectionTypes`；
- 外部 Terminal 必须至少允许一种 ConnectionType。

Terminal 不允许通过数组顺序、角色名称或坐标寻找节点。所有关系均由稳定 ID 绑定。

### 5.4 孤立 Node 检查

加载完成后统计每个 `ElectricalNode` 被 Terminal 引用的数量：

- 零个 Terminal 引用的节点视为孤立节点并拒绝加载；
- CableTermination 的内部节点必须恰好被其电缆侧和架空侧两个 Terminal 引用；
- RingCabinet 节点由聚合恢复入口执行更严格的固定拓扑校验；
- 不根据 Connection 给节点补 Terminal，也不删除孤立节点来“修复”文件。

若未来出现业务上允许无 Terminal 的节点类型，必须先在 Domain 和 DTO 合同中明确，不能在当前实现中预留宽松例外。

## 6. CableTermination 持久化

### 6.1 设备 DTO

在现有 `devices[]` 类型判别体系中增加 `deviceKind = cable-termination`。建议为 `ProjectDeviceDto` 增加明确的 `cableTermination` 明细对象，避免继续增加互不相关的扁平可空字段：

```text
ProjectDeviceDto
├─ deviceId
├─ deviceKind = cable-termination
├─ deviceType = cable-termination
├─ displayName
├─ voltageLevel
└─ cableTermination
   ├─ cableSideTerminalId
   ├─ overheadSideTerminalId
   └─ internalNodeId
```

当 `deviceKind = cable-termination` 时，明细必填；其他设备不得带有该明细。三个 ID 均非空且互不混用：两侧 Terminal ID 必须不同，内部 Node ID 不得等于任何设备或 Terminal ID。

### 6.2 固定内部拓扑

电缆终端固定结构为：

```text
电缆 Connection
      ↓
CableSide Terminal
      ↓
Intermediate ElectricalNode
      ↑
OverheadSide Terminal
      ↑
架空线路 Connection
```

保存和恢复规则：

- `CableTermination` 是 `Device`；
- 内部节点类型必须为 `intermediate`，所有者为该 CableTermination；
- 电缆侧 Terminal 角色为 `CableSide`，允许类型只能是 `cable`；
- 架空侧 Terminal 角色为 `OverheadSide`，允许类型只能是 `overhead-line`；
- 两个 Terminal 都是外部端子，默认不允许多连接；
- 两个 Terminal 的 `electricalNodeId` 必须同时指向 `internalNodeId`；
- 两侧固定导通由 ElectricalNode 表达，禁止额外创建一条 Connection 表示内部接线；
- 设备、节点和端子的电压等级必须与当前 10kV 合同一致。

### 6.3 恢复次序

CableTermination 的恢复不能作为一次无校验反序列化完成，顺序固定为：

1. 根据设备 DTO 创建 CableTermination，保留设备、两侧端子和内部节点 ID；
2. 将 Device 加入候选 DrawingDocument；
3. 创建并加入其 Intermediate ElectricalNode；
4. 创建电缆侧 Terminal 并加入文档；
5. 创建架空侧 Terminal 并加入文档；
6. 核对节点反向索引恰好包含两个声明的 Terminal ID；
7. 后续再恢复引用这两个 Terminal 的 Connection。

任何一步失败都放弃整个候选 DrawingDocument，不保留部分电缆终端。

## 7. OverheadLine DTO 与一对一关系

### 7.1 字段定义

`ProjectOverheadLineDto` 保存：

| 字段 | 类型 | 规则 |
| --- | --- | --- |
| `connectionId` | Guid | 与对应 Connection 共用 ID，不是新对象 ID |
| `lineModel` | string | 非空白 |
| `lengthMeters` | double? | 可空；存在时必须为有限正数 |
| `supportPoleIds` | Guid[] | 至少一个，非空、无重复，保持物理顺序 |
| `isContinued` | bool | 是否延续到当前绘制范围之外 |
| `continuationTerminalId` | Guid? | 延续时必填，且必须是 Connection 端点之一 |
| `continuationState` | string? | 延续时为 `energized` 或 `unknown` |
| `continuationDescription` | string? | 可选说明，空白归一为 null |

### 7.2 一对一恢复规则

`Connection` 是外部电气连接事实，`OverheadLine` 是该连接的专业明细。必须满足：

- 每个 `connectionType = overhead-line` 的 Connection 恰好对应一个 OverheadLineDto；
- 每个 OverheadLineDto 必须找到同 ID 的 Connection；
- 对应 Connection 类型必须是 `overhead-line`；
- `connectionType = cable` 的 Connection 不得有 OverheadLineDto；
- `OverheadLineDto` 不独立注册第二个全局 ID，但其 `connectionId` 在明细集合内必须唯一。

加载时先调用 `DrawingDocument.AddConnection`，再构造 OverheadLine 并调用 `DrawingDocument.AddOverheadLine`。这样会复用当前 Domain 对连接类型、支撑杆、延续端子和物理端点位置的校验。

### 7.3 支撑杆和物理端点

- `supportPoleIds` 中每个 ID 必须指向 Pole；
- 数组顺序原样恢复，不按杆号或坐标排序；
- 普通支撑杆不需要 Terminal，也不改变 Connection 两端；
- 数组首尾用于校验 Connection 两端设备的物理杆位；
- 当端点属于 CableTermination 或柱上开关时，对应 PoleAttachment 必须先恢复；
- `isContinued = true` 时，ContinuationTerminal 必须是 Connection 的起点或终点；
- `isContinued = false` 时，ContinuationTerminal、ContinuationState 和说明必须全部为空。

该模型不保存弧垂、折线点、坐标或自动布线路径；这些属于 Layout/Rendering 范围。

## 8. ID 注册与引用恢复

### 8.1 工程级 ID 注册表

反序列化后、创建 Domain 对象前，先扫描 DTO 建立只读 ID 目录：

```text
DomainIdCatalog
├─ DeviceIds
├─ InternalAggregateIds
├─ SwitchAssemblyIds
├─ ElectricalNodeIds
├─ TerminalIds
├─ ConnectionIds
└─ PoleAttachmentIds
```

同一个 ID 不得跨类别复用。OverheadLine 的 `connectionId` 是唯一例外，因为它不是第二个对象 ID，而是对应 Connection 的一对一明细键。

预注册只证明引用目标在 DTO 中声明，不代表对象已经通过 Domain 校验。运行时解析器必须在对应阶段对象创建成功后才返回对象。

### 8.2 引用绑定原则

- 使用 `Dictionary<Guid, T>` 或等价只读 Resolver 按 ID 查找；
- 不把 DTO 数组索引作为引用；
- 不通过名称、杆号、Terminal 角色或坐标回退查找；
- 不创建占位 Domain 对象；
- 不在引用缺失时生成新 ID；
- 不跳过损坏对象后继续打开“部分工程”。

## 9. 完整加载顺序

Topology 恢复嵌入现有 Domain 恢复事务，固定为：

```text
DTO 结构与稳定编码预校验
    ↓
扫描全部 ID，建立 DomainIdCatalog
    ↓
恢复 RingCabinet 聚合并注册其内部对象
    ↓
创建 Pole、CableTermination 等顶层 Device
    ↓
创建非环网柜 ElectricalNode
    ↓
创建非环网柜 Terminal，并建立 Node 反向索引
    ↓
恢复 PoleAttachment
    ↓
创建 Connection
    ↓
按 ConnectionId 创建 OverheadLine 明细
    ↓
执行工程级拓扑完整性检查
    ↓
输出候选 DrawingDocument
```

顺序不能交换：Connection 依赖 Terminal；Terminal 依赖 Device 和 ElectricalNode；OverheadLine 依赖 Connection、Pole 和必要的 PoleAttachment。

加载器始终在新的候选 DrawingDocument 中执行。只有全部步骤成功后，`ProjectService` 才能原子替换当前 Session；失败时保留原工程、Selection、CommandStack 和 Dirty 状态。

## 10. 拓扑完整性检查

### 10.1 缺失 Terminal

以下任一情况拒绝加载：

- Connection 起点或终点 Terminal 不存在；
- CableTermination 声明的任一侧 Terminal 不存在；
- Pole 声明的架空锚点 Terminal 不存在；
- Terminal 声明的所有者不存在；
- Terminal 引用的 ElectricalNode 不存在；
- 延续线路声明的 ContinuationTerminal 不存在或不是对应 Connection 端点。

### 10.2 重复对象和连接占用

检查：

- 所有独立对象 ID 工程级唯一；
- Connection ID 唯一；
- OverheadLine 明细键唯一；
- 非多连接 Terminal 的 Connection 引用次数不超过一次；
- 一个 AttachedDevice 不能同时挂到多个 Pole；
- SupportPoleIds 内不得重复。

### 10.3 Node 完整性

检查：

- Node 所有者存在且类型匹配；
- 每个 Terminal 的正向 `electricalNodeId` 与 Node 的恢复后反向 TerminalIds 一致；
- 不存在零 Terminal 引用的孤立 Node；
- Earth Node 不保存 ElectricalState；
- CableTermination 内部 Node 为 Intermediate，且仅连接其声明的两个 Terminal；
- RingCabinet 内部 Node 继续由 RingCabinet 聚合校验，不由通用恢复器重建另一份。

### 10.4 Connection 与专业明细一致性

检查：

- Connection 类型与两端允许类型一致；
- Connection 电压与两端 Terminal 电压兼容；
- 每条 OverheadLine Connection 恰好有一个明细；
- Cable Connection 没有 OverheadLine 明细；
- OverheadLine 支撑杆和物理端点关系通过 Domain 校验；
- SupportPoleIds 不生成任何隐含电气连接。

## 11. 错误处理与诊断

拓扑加载错误应至少包含：

- 稳定错误类别，如 `MissingReference`、`DuplicateId`、`InvalidTopology`、`UnsupportedValue`；
- DTO 对象类型和对象 ID；
- 字段路径，如 `domain.connections[2].startTerminalId`；
- 缺失或冲突的目标 ID；
- 不包含本机路径以外的敏感内容。

恢复器不得自动执行以下修复：

- 交换 Connection 起止端；
- 删除重复 Connection；
- 为缺失 Terminal、Node 或 Pole 生成新对象；
- 按坐标猜测连接；
- 为孤立 Node 补虚拟 Terminal；
- 把缺少 OverheadLine 明细的连接降级为普通直线。

这些问题均导致候选工程加载失败，源 `.kvdrawing` 文件保持不变。

## 12. 对后续实现的建议拆分

建议后续编码按以下最小顺序进行：

1. 扩展 `ProjectDomainDto`，增加顶层 Node、Terminal、Connection 和 OverheadLine DTO；
2. 增加 CableTermination 专用 DTO 明细和 Mapper；
3. 实现 DTO 全局 ID 预校验及引用目录；
4. 按 Device → Node → Terminal → Attachment → Connection → OverheadLine 顺序恢复；
5. 增加保存后重新加载的 Domain 往返测试；
6. 覆盖缺失 Terminal、重复 ID、端子容量冲突、孤立 Node 和一对一明细缺失测试。

若该 DTO 结构在正式工程文件发布后发生不兼容变化，必须提升 `FormatVersion` 并提供 DTO Migration；不能在同一格式版本下静默改变字段含义。

## 13. 本阶段不实现

- 任何 C# DTO、Mapper、Rehydrator 或测试代码；
- Layout、线路路径、坐标、WPF Visual 和 Symbol；
- Selection、Undo/Redo、Dirty 状态持久化；
- WorkScope、BoundaryPoint、GroundingPoint；
- 自动布线、潮流计算、电气仿真或现场带电状态推导；
- PTInterval、DTUCabinet；
- 文件修复、部分加载或容错降级。
