# M4-B-2-A Domain DTO 序列化架构设计

> 文档状态：设计稿，仅定义 Domain 持久化合同与恢复流程，不实现代码<br>
> 编制日期：2026-08-11<br>
> 依据：`docs/project-file-design.md` 与当前 `DistributionDrawing.Domain` 模型

## 1. 目标与范围

本设计定义 `.kvdrawing` 中 Domain 区的版本化 DTO、稳定 ID、跨对象引用和聚合恢复方案。目标是在不直接序列化运行时领域对象、不绕过 Domain 不变量的前提下，完整保存并恢复当前领域事实。

本阶段重点覆盖：

- `DrawingDocument`；
- 顶层 `Device` 及当前已实现的具体设备；
- `Terminal`；
- `ElectricalNode`；
- `Connection`；
- `RingCabinet` 与有序 `RingCabinetInterval` 聚合；
- 为保持当前 Domain 完整性所必需的 `SwitchAssembly`、`PoleAttachment` 和 `OverheadLine` 关系数据。

本设计不涉及 Layout、Rendering、Editor 状态和实际序列化代码，也不启用尚未实现的 `PTInterval`、`DTUCabinet`、`WorkScope` 或 `GroundingPoint`。

## 2. Domain 与 DTO 的分离边界

### 2.1 分层职责

```text
Domain
  维护业务行为、聚合边界和不变量
        ↑                    ↓
Domain Rehydrator       Domain Snapshot Mapper
  完整恢复并校验              提取持久化事实
        ↑                    ↓
Current Domain DTO（纯数据合同）
        ↑
Version Migration（只转换 DTO）
        ↑
document.json / domain
```

- Domain 对象是运行时业务模型，不承担 JSON 字段兼容职责。
- DTO 是工程文件合同，只包含基本类型、稳定字符串编码、ID、列表和嵌套 DTO。
- Mapper 显式映射每一个保存字段，不通过反射序列化 Domain 私有字段或 CLR 运行时类型。
- Rehydrator 只能调用 Domain 明确提供的创建或恢复入口，不能反射设置只读属性。
- Migration 只处理 DTO，不在 Domain 构造器中加入文件版本判断。
- Rendering、Layout 和 Editor 不引用 Domain DTO。

### 2.2 禁止直接序列化的内容

不得直接序列化：

- `Device`、`RingCabinet` 等领域对象实例；
- CLR 类型名、程序集名或 `$type` 多态元数据；
- Domain 私有集合和缓存；
- `ElectricalNode.TerminalIds` 这类可由正向引用重建的反向索引；
- `CompositionKind`、`OperationalState`、`IsEffectivelyGrounded`、`ViolatedRuleCodes` 等派生值；
- `InterlockRule` 的运行时对象和规则执行委托。

文件中的设备种类、间隔种类、开关种类和状态均使用版本合同定义的稳定字符串编码，不使用 C# 枚举整数值。

## 3. Domain DTO 根结构

`document.json` 的 `domain` 区建议使用以下逻辑结构：

```text
DomainDto
├─ documentId
├─ title
├─ devices[]                 # 非环网柜顶层设备
├─ ringCabinets[]            # 完整环网柜聚合，只保存一次
├─ electricalNodes[]         # 非环网柜内部节点
├─ terminals[]               # 非环网柜内部端子
├─ connections[]             # 外部电气连接
├─ overheadLines[]           # Connection 的架空线路明细
└─ poleAttachments[]         # 杆塔与附属设备关系
```

`documentId` 必须与 Manifest 的工程 ID 一致。`title` 对应 `DrawingDocument.Title`；Metadata 可重复展示标题，但只能由加载协调器校验一致，不能静默选择其中一个覆盖另一个。

环网柜本体不重复出现在 `devices[]`。其内部开关、节点、端子和组合只保存在所属 `RingCabinetDto` 内，不能同时出现在顶层集合。

## 4. Device DTO

### 4.1 通用字段

所有具体设备 DTO 共享以下稳定事实：

| 字段 | 说明 |
| --- | --- |
| `deviceId` | `Device.Id`，非空且工程内全局唯一 |
| `deviceKind` | 稳定类型判别器，如 `pole`、`switch`、`cable-termination` |
| `displayName` | 可选显示名称 |
| `voltageLevel` | 可选电压等级；具体类型可规定必填或固定值 |
| `parentId` | 仅具体设备语义允许时出现，不作为通用自由引用 |

不设计可装入任意字段的通用 `properties` 字典。每一种受支持设备必须有明确 DTO 合同和 Mapper。

### 4.2 当前具体设备

#### PoleDeviceDto

保存：

- `deviceId`；
- `displayName`；
- `poleNumber`；
- `poleType`；
- `overheadAnchorTerminalIds[]`。

锚点端子 ID 集合是 Pole 对端子所有权的声明，但端子完整内容只保存在顶层 `terminals[]`，不能嵌入第二份副本。

#### SwitchDeviceDto

保存：

- `deviceId`；
- `displayName`；
- `voltageLevel`；
- `switchKind`；
- `installationType`；
- `switchState`；
- `dispatchNumber`；
- 有序的 `terminalIds[]`；
- `parentIntervalId`，仅柜内开关使用。

顶层 `devices[]` 当前只允许保存 `installationType = pole` 的柱上开关，并要求 `parentIntervalId` 为空。柜内开关必须嵌套在对应 Interval DTO 中，要求 `installationType = cabinet-interval` 且 `parentIntervalId` 等于所属 Interval ID。

#### CableTerminationDeviceDto

保存：

- `deviceId`；
- `displayName`；
- `voltageLevel`；
- `cableSideTerminalId`；
- `overheadSideTerminalId`；
- `internalNodeId`。

两个端子和内部节点完整内容仍位于顶层 `terminals[]` 与 `electricalNodes[]`，本 DTO 只声明角色引用。

### 4.3 当前不支持的设备类型

`DeviceType.PT` 虽已存在于枚举，但当前没有完整 PT 领域类型和恢复规则，因此当前 DTO 合同不得写出或接受 `pt`。未知 `deviceKind`、把专用设备保存为通用 `device`、或者类型与字段组合不匹配时必须拒绝加载。

## 5. Terminal DTO

`TerminalDto` 保存：

| 字段 | 说明 |
| --- | --- |
| `terminalId` | 稳定端子 ID |
| `ownerType` | `device` 或 `internal-aggregate` |
| `ownerId` | 设备 ID 或 Interval ID |
| `role` | 端子业务角色稳定编码 |
| `voltageLevel` | 可选电压等级 |
| `isExternal` | 是否允许外部 Connection 使用 |
| `allowsMultipleConnections` | 是否允许多条外部连接 |
| `electricalNodeId` | 可选的内部固定拓扑节点 ID |
| `allowedConnectionTypes[]` | 外部端子允许的连接类型集合 |

规则：

- `isExternal = false` 时，`allowedConnectionTypes` 必须为空。
- `isExternal = true` 时，允许类型集合不得为空。
- `electricalNodeId` 只表达固定内部拓扑，不能引用 Connection。
- 端子完整对象只能保存一次；设备中的端子 ID 仅作为角色和所有权声明。
- `role` 在当前版本使用 Domain 已确认的稳定角色值，显示文字不从该字段生成。

## 6. ElectricalNode DTO

`ElectricalNodeDto` 保存：

- `nodeId`；
- `nodeType`：`main-bus`、`circuit`、`intermediate` 或 `earth`；
- `ownerType`；
- `ownerId`；
- `electricalState`：人工维护的 `energized`、`deenergized` 或 null。

`terminalIds` 不在节点 DTO 中重复保存。`TerminalDto.electricalNodeId` 是节点—端子固定连接的唯一持久化来源，恢复时据此重建 `ElectricalNode.TerminalIds` 反向索引。这避免两个方向数据不一致。

Earth 节点的 `electricalState` 必须为 null。其他节点只恢复文件中明确保存的人工状态，不根据开关状态或 Connection 自动推导现场带电事实。

## 7. Connection DTO

`ConnectionDto` 保存：

- `connectionId`；
- `connectionType`：`cable` 或 `overhead-line`；
- `startTerminalId`；
- `endTerminalId`；
- `displayName`；
- `voltageLevel`。

Connection 只表达设备之间的外部电气连接，不用于保存柜内固定接线。加载时必须验证：

- 两个端子均存在、不同且为外部端子；
- 两端均允许该 `connectionType`；
- 端子连接数量不违反 `allowsMultipleConnections`；
- 电压等级和当前 Domain 已确认的连接约束一致。

`OverheadLineDto` 是 `ConnectionType = overhead-line` 的一对一明细，以同一个 `connectionId` 为键，不注册第二个独立对象 ID。它保存线路型号、长度、按业务顺序排列的 `supportPoleIds` 以及延续事实。`PoleAttachmentDto` 保存 `attachmentId`、`poleId` 与 `attachedDeviceId`。两者虽不属于本阶段核心六类 DTO，但缺少它们会导致当前 Domain 关系丢失，因此纳入 Domain 根合同。

## 8. RingCabinet 聚合 DTO

### 8.1 聚合结构

```text
RingCabinetDto
├─ cabinetId
├─ deviceKind = ring-cabinet
├─ displayName
├─ voltageLevel
├─ mainBusNodeId
├─ intervals[]               # 保留物理顺序
│  ├─ switches[]
│  └─ switchAssembly
├─ electricalNodes[]         # 柜内全部节点，含主母线节点
└─ terminals[]               # 柜内全部端子
```

`CompositionKind` 不保存，由 Interval 类型组合重新计算。环网柜内部对象不得脱离该 DTO 单独存在。

### 8.2 RingCabinetIntervalDto

每个 Interval 保存：

- `intervalId`；
- `parentCabinetId`；
- `sequence`；
- `displayName`；
- `intervalKind`：当前仅允许 `load-switch-interval` 或 `integrated-feeder-interval`；
- `groundingStructureKind`：仅一二次融合间隔必填；
- `intermediateNodeId`：仅一二次融合间隔必填；
- `circuitNodeId`；
- `earthNodeId`；
- `externalTerminalId`；
- `switches[]`；
- `switchAssembly`。

`intervals[]` 的数组顺序是物理顺序，且必须满足 `sequence = 数组索引 + 1`。加载时不按 Sequence 排序修复错误，也不根据名称猜测顺序。

### 8.3 SwitchAssemblyDto

保存：

- `assemblyId`；
- `parentIntervalId`；
- `assemblyType`；
- `ruleSetRef`；
- `members[]`，每项包含稳定角色与 `switchDeviceId`。

不保存 `InterlockRule` 运行时规则内容。恢复工厂根据受支持的 `assemblyType + groundingStructureKind + ruleSetRef` 选择 Domain 内置规则，并验证成员角色：

- 普通负荷开关间隔必须恰好包含负荷开关和接地刀闸；
- 一二次融合间隔必须恰好包含隔离刀闸、断路器和接地刀闸；
- Assembly 成员 ID 集合必须与 Interval 内开关集合完全一致；
- `parentIntervalId` 必须与所属 Interval 相同；
- 未知或不匹配的 `ruleSetRef` 必须通过迁移处理或拒绝加载，不能静默采用当前规则。

### 8.4 三种接地结构的恢复

一二次融合间隔必须保存单个 Interval 自身的 `groundingStructureKind`：

- `upper-isolation-grounding`；
- `upper-lower-grounding`；
- `lower-lower-grounding`。

加载时不从端子连接图猜测结构类型。恢复工厂先读取结构类型，再校验 DTO 中各开关端子的 `electricalNodeId` 是否与该结构的已确认拓扑完全一致。结构类型、节点引用和端子引用三者冲突时拒绝加载。

## 9. 稳定 ID 保存与恢复

### 9.1 ID 规则

- 所有 Guid 使用标准小写 `D` 格式。
- 空 Guid 非法。
- 保存、打开、再次保存后，全部已有 ID 必须逐值不变。
- 不使用名称、杆号、Sequence、集合下标、坐标或对象哈希生成恢复 ID。
- Mapper 不生成 ID；只有创建新业务对象的 Domain 用例可以生成新 ID。

需要保持的 ID 包括 Document、Device、Interval、SwitchAssembly、Terminal、ElectricalNode、Connection 和 PoleAttachment ID。柜内 SwitchDevice ID 同样参加工程级唯一性检查。

### 9.2 全局 ID 注册表

在构造任何 Domain 聚合前，加载器对当前版本 DTO 建立只读 ID 注册表：

```text
ObjectIdRegistry
├─ document
├─ devices（含 RingCabinet 与柜内 SwitchDevice）
├─ intervals
├─ switchAssemblies
├─ terminals
├─ electricalNodes
├─ connections
└─ poleAttachments
```

所有具有独立身份的对象 ID 在工程范围内不得重复。`OverheadLineDto.connectionId` 是 Connection 的明细外键，不作为新身份重复注册。发现重复 ID 必须立即失败，禁止字典后写覆盖前写。

## 10. 跨对象引用恢复

引用恢复采用“先登记、后构造、最后提交”的方式，不依赖 JSON 属性或数组的读取顺序。

### 10.1 Terminal 引用

- `ownerId` 必须存在，且类型与 `ownerType` 一致。
- Device 所有端子必须被该具体设备的端子 ID 声明包含。
- InternalAggregate 所有端子当前只能指向所属 RingCabinet 的 Interval。
- `electricalNodeId` 必须指向同一允许聚合边界内的节点。
- 柜内开关端子不能引用其他柜体或其他 Interval 的内部节点，主母线节点是经 RingCabinet 聚合允许的唯一跨 Interval 共享节点。

### 10.2 Connection 引用

- 端点查找在全部设备、环网柜及端子恢复完成后执行。
- 端点只能引用 `isExternal = true` 的 Terminal。
- Connection 不得通过坐标或名称补救缺失端点。
- 起点、终点交换不改变连接身份，但保存时保持原有顺序，避免无意义文件差异。

### 10.3 RingCabinet 内部引用

每个柜体在独立的聚合 ID 作用域内检查：

- `mainBusNodeId` 指向本柜唯一 MainBus 节点；
- Interval 的 `parentCabinetId` 指向当前柜体；
- SwitchDevice 的 `parentIntervalId` 指向当前 Interval；
- Assembly 的父级和成员均属于当前 Interval；
- `circuitNodeId`、`earthNodeId`、可选 `intermediateNodeId` 指向当前 Interval 所有节点；
- `externalTerminalId` 指向当前 Interval 的外部端子；
- 所有开关 `terminalIds` 指向本柜唯一 Terminal 对象；
- Terminal—Node 关系符合 IntervalKind 与 GroundingStructureKind 的固定拓扑。

悬空引用、跨聚合引用、重复引用或角色不匹配都作为文件损坏处理，不自动修复。

## 11. 聚合恢复流程

### 11.1 专用恢复入口

当前 `RingCabinet.Create` 会为主母线、Interval、内部开关、节点、端子和 SwitchAssembly 生成新 ID，只适用于新建设备，不能用于打开工程。

后续实现必须在 Domain 提供专用的完整恢复入口，例如 `RingCabinet.Restore(restoreDefinition)`。名称可在实现阶段确定，但必须满足：

- 一次接收完整聚合快照和全部原始 ID；
- 在 Domain 程序集中创建内部 `SwitchDevice`、`SwitchAssembly` 和 `RingCabinetInterval`；
- 根据 DTO 事实恢复 `SwitchState`，不反写派生状态；
- 重建 Node 的 Terminal 反向索引；
- 执行与新建工厂同等级别的结构和拓扑校验；
- 仅在完整成功后返回 RingCabinet；
- 禁止先调用普通 `Create`，再通过反射或属性替换 ID。

柱上 `SwitchDevice` 当前同样缺少 Infrastructure 可调用的公共恢复构造入口，应由 Domain 提供类型明确的创建/恢复工厂；不能为了序列化把构造器和状态设置器无条件公开。

### 11.2 推荐加载顺序

```text
1. 校验容器与 Manifest
2. 解析对应版本 DTO
3. 逐版本迁移到 Current Domain DTO
4. 执行 DTO 结构、枚举和 ID 注册校验
5. 创建临时 DrawingDocument
6. 完整恢复每个 RingCabinet 聚合
7. 恢复其他顶层 Device
8. 恢复非环网柜 ElectricalNode
9. 恢复非环网柜 Terminal，并建立 Node 反向索引
10. 添加 Connection
11. 添加 OverheadLine 和 PoleAttachment 关系
12. 执行 DrawingDocument 全量完整性校验
13. 重新计算派生状态
14. 成功后一次性替换当前会话文档
```

整个过程使用临时加载上下文。任一步失败均丢弃临时结果，不把部分对象加入当前 EditorSession。

### 11.3 保存快照流程

保存方向按以下顺序提取：

1. 从一致的 `DrawingDocument` 快照开始；
2. 将 RingCabinet 作为完整聚合映射，标记其内部对象 ID；
3. 映射其他顶层设备、节点和端子；
4. 映射 Connection 及关系明细；
5. 检查没有遗漏对象或重复映射；
6. 对无业务顺序集合按稳定 ID 排序，对 Interval、Assembly 角色和 SupportPoleIds 保留业务顺序；
7. 对 Current DTO 执行与加载前相同的结构及引用校验；
8. 交给工程文件容器写入 `document.json`。

## 12. 加载后的完整性校验

### 12.1 DTO 结构校验

- 必填字段存在且字符串不为空；
- Guid 非空、枚举编码受当前合同支持；
- 数值有限且满足已确认范围；
- DTO 类型判别器与字段结构匹配；
- 当前不支持的 PT、PTInterval、DTU 等类型被明确拒绝。

### 12.2 身份与所有权校验

- 工程级 ID 无重复；
- 所有 Parent、Owner、Member 和端点引用存在且类型正确；
- 顶层对象与聚合内部对象没有重复保存；
- 一个附属设备不会同时挂到多个 Pole；
- 一个 Connection 最多有一个对应 OverheadLine 明细。

### 12.3 电气拓扑校验

- Terminal 所属设备确实声明该 Terminal ID；
- Terminal 引用的 ElectricalNode 存在且聚合边界合法；
- Node 的反向 Terminal 集合与所有正向引用完全一致；
- Connection 两端及允许连接策略有效；
- CableTermination 两端子和内部节点形成其固定拓扑；
- Pole 支撑顺序和 PoleAttachment 不生成额外 ElectricalNode 或 Connection。

### 12.4 环网柜聚合校验

- Interval 非空、有序且 Sequence 连续；
- 普通间隔和一二次融合间隔的开关数量、种类、Assembly 成员及节点结构正确；
- MainBus、Circuit、Intermediate、Earth 节点类型和所有者正确；
- 三种 GroundingStructureKind 的端子—节点拓扑完全匹配；
- 已确认的硬联锁组合有效；非法组合拒绝加载，不自动改变任何 SwitchState；
- 合法但未确认运行语义的组合可恢复，派生结果保持 `Unclassified`。

### 12.5 文档级最终校验

所有对象加入临时 `DrawingDocument` 后执行一次全量校验，覆盖局部 DTO 校验无法发现的跨对象冲突。随后重新计算组合状态和有效接地结果；这些计算结果只用于确认模型可评估，不写回 DTO。

错误必须包含稳定错误代码、JSON 路径、相关对象 ID 和可读说明。加载器不得通过删除无效对象、生成新 ID、修改状态或忽略未知类型继续打开。

## 13. Version 与 Migration 衔接

### 13.1 版本选择

Manifest 的 `formatVersion` 是唯一工程格式版本来源：

```text
Manifest.formatVersion
    ↓
选择 VersionedDomainDto Reader
    ↓
逐级 Migration
    ↓
CurrentDomainDto
    ↓
统一校验与 Domain Restore
```

不在 `domain` 区重复保存可冲突的格式版本。若未来 Domain 子合同确实需要独立演进，必须先正式定义 `domainSchemaVersion` 与总格式版本的兼容矩阵，当前版本不提前加入该字段。

### 13.2 迁移规则

- 每个迁移器只接受一个明确旧版本并输出下一个版本。
- 迁移保留全部已有稳定 ID，并同步更新所有受影响引用。
- 字段重命名、枚举编码变化、聚合嵌套调整在 DTO 层完成。
- 迁移不得构造 Domain 对象，也不得根据 Layout 坐标推测电气连接。
- 旧版本缺少后来新增对象 ID 时，迁移器可以确定性地生成一次新 ID，但必须在同一步更新全部引用并通过冲突检查。
- 未知 Device、Interval、规则集或更高版本默认拒绝，不降级为基础 Device。

迁移完成后只运行 Current DTO 的 Mapper 和恢复工厂，从而避免 Domain 层散布版本分支。

## 14. 后续实现需要补充的 Domain 能力

进入序列化实现前，需要单独评审并实现以下最小 Domain 支撑，但本阶段不修改代码：

- 保持原始内部 ID 的 RingCabinet 完整恢复定义与工厂；
- 柱上 SwitchDevice 的类型安全恢复入口；
- 能对恢复后的 DrawingDocument 执行全量完整性检查的入口；
- 明确 DTO Mapper 所需只读事实是否都能通过现有公共属性取得；
- 为恢复工厂和正常创建工厂建立等价不变量测试。

这些入口应属于 Domain，不属于 DTO 或 Infrastructure；其目的只是安全恢复既有事实，不应开放外部逐步拼装不完整聚合的能力。

## 15. 验收建议

后续实现至少验证：

- 普通、融合及混合 RingCabinet 保存—打开后全部内部 ID、Interval 顺序和开关状态不变；
- 三种接地结构的节点及端子关系往返一致；
- Pole、柱上 SwitchDevice、CableTermination、Terminal、Node 和 Connection 引用一致；
- Node 的 Terminal 反向索引能由 DTO 正向引用完整重建；
- 重复 ID、悬空端点、跨柜引用、错误 Parent、未知类型和未知规则集均被拒绝；
- 加载非法硬联锁状态失败且不自动修改开关；
- `CompositionKind`、OperationalState 和有效接地结果未出现在 JSON，打开后可重新得到一致结果；
- V1 DTO 经保存、打开、再次保存后得到确定性的 Domain 数据；
- 迁移保持已有 ID，迁移失败不影响当前会话和原工程文件。

## 16. 本阶段不实现

- DTO、Mapper、Rehydrator、Migration 或 JSON Schema 代码；
- Domain 恢复工厂及任何现有领域模型修改；
- Layout DTO 和 Layout 恢复；
- Rendering、Symbol、Selection、Undo/Redo 或其他 Editor 状态持久化；
- PTInterval、DTUCabinet、WorkScope、GroundingPoint 和工作票数据；
- 保存/打开 UI、自动修复、数据库、云同步或多用户能力。
