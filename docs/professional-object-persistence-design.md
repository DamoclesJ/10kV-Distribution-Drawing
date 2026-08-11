# M5-B 配电专业对象持久化边界设计

> 文档状态：设计稿，仅定义 DTO、引用恢复和版本边界，不实现代码<br>
> 编制日期：2026-08-11<br>
> 依据：`docs/distribution-professional-object-model-design.md` 与当前 M4 持久化架构

## 1. 目标与范围

本设计定义 `WorkScope`、`BoundaryPoint` 和 `GroundingPoint` 在 `.kvdrawing` 工程文件中的保存、恢复、校验及迁移边界。

目标是：

- 设备与电气拓扑继续由现有 Domain DTO 表达；
- 工作范围与工作地线作为工程专业事实独立保存；
- 未来 WorkTicketData 只引用专业对象，不复制专业对象；
- 所有跨对象关系通过稳定 ID 恢复；
- 加载过程中先恢复完整 Topology，再绑定 Professional；
- 不把坐标、Rendering 或运行计算结果写入专业 DTO。

本阶段不修改当前工程文件格式，不实现 DTO、Mapper、Rehydrator 或 Migration 代码。

## 2. 三个保存区域

### 2.1 逻辑结构

专业对象加入后的 `document.json` 建议采用：

```text
document.json
├── documentId
├── metadata
├── domain
│   ├── devices
│   ├── ringCabinets
│   ├── electricalNodes
│   ├── terminals
│   ├── connections
│   ├── overheadLines
│   └── poleAttachments
├── professional
│   ├── documentId
│   ├── groundingPoints
│   └── workScopes
├── layout
└── workTicket
```

三个业务保存区域的职责如下：

| 区域 | 保存内容 | 不保存内容 |
| --- | --- | --- |
| Domain | Device、RingCabinet 聚合、ElectricalNode、Terminal、Connection、OverheadLine、PoleAttachment | WorkScope、GroundingPoint、票面流程、Layout |
| Professional | GroundingPoint、WorkScope 及其内部 BoundaryPoint | 设备副本、Topology 副本、坐标、计算结果 |
| WorkTicketData | 未来 SafetyMeasure、OperationStep、票号及对专业对象的引用 | WorkScope/GroundingPoint 副本、Device/Terminal 副本 |

### 2.2 Professional 区不是新的运行时架构

`professional` 是文件合同中的独立分区，用于控制 DTO 职责和恢复顺序。它不改变 M5-A 已确定的运行时关系：

- WorkScope 和 GroundingPoint 仍是 DrawingDocument 持有的工程专业事实；
- DrawingDocument 仍是 Device、Topology 和 Professional 的一致性边界；
- 不新增与 DrawingDocument 并列、可以独立保存的“ProfessionalDocument”；
- Professional DTO 不直接成为编辑器或 Rendering 的事实源。

相较 M5-A 中 `domain.workScopes[]` 和 `domain.groundingPoints[]` 的逻辑示意，本设计进一步把它们收敛到独立 `professional` 分区。该调整只影响未来文件合同的物理组织，不改变对象语义和所有权。

### 2.3 WorkTicketData 隔离

`workTicket` 继续作为可选、独立版本化的未来业务区：

```text
workTicket
├── schemaVersion
├── workTicketId
├── workScopeIds
├── safetyMeasures
└── operationSteps
```

当前未形成工作票完整字段基线，因此当前版本不写入占位 SafetyMeasure 或 OperationStep。工程图可以没有 WorkTicketData，但仍可拥有 WorkScope 和 GroundingPoint。

## 3. Professional DTO 总体边界

### 3.1 根 DTO

建议逻辑 DTO：

```text
ProjectProfessionalDto
├── DocumentId
├── GroundingPoints[] : ProjectGroundingPointDto
└── WorkScopes[]      : ProjectWorkScopeDto
```

字段建议：

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| DocumentId | 是 | 必须与 Manifest.ProjectId、Domain.DocumentId 和 Layout.DocumentId 一致 |
| GroundingPoints | 是 | 无对象时保存空集合，不使用 null 表示未知 |
| WorkScopes | 是 | 无对象时保存空集合，不使用 null 表示未知 |

Professional 根 DTO 不重复保存 Title、FormatVersion 或 Metadata。FormatVersion 以 Manifest 为唯一依据。

### 3.2 DTO 与运行时对象分离

- DTO 只使用 JSON 可稳定表达的标量、枚举编码、稳定 ID 和集合；
- DTO 不引用 Domain 实例、Terminal 实例或 WPF 类型；
- DTO Mapper 负责快照转换，Rehydrator 负责按顺序恢复；
- 运行时类名重构不应改变 DTO 判别值；
- 未知必填枚举和不支持的 Professional 子类型必须拒绝，不能反射构造任意类型。

## 4. BoundaryPoint DTO

### 4.1 内联值对象

BoundaryPoint 不拥有独立稳定 ID，作为 WorkScope DTO 的内联值对象保存：

```text
ProjectBoundaryPointDto
├── DeviceId
├── TerminalId
└── Side
```

建议字段：

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| DeviceId | Guid | 是 | 边界所属设备或聚合设备 ID |
| TerminalId | Guid | 是 | 明确电气端子 ID，边界位置的事实源 |
| Side | 稳定字符串枚举 | 是 | 经确认的业务侧别 |

StartBoundary 和 EndBoundary 的字段结构相同，其角色由所在属性确定，不保存额外 `BoundaryKind`。

### 4.2 不保存内容

BoundaryPoint DTO 不保存：

- 屏幕坐标、毫米坐标或图元锚点；
- 杆号、设备名称或端子显示文字；
- Connection 路径折点；
- 自动计算的范围方向；
- Terminal 或 Device 的嵌套副本。

### 4.3 引用校验

恢复时必须校验：

- DeviceId、TerminalId 均非空；
- Device 和 Terminal 已存在；
- Terminal 所有者与 Device 或其允许公开的聚合对象一致；
- Side 编码受当前规则支持，并与 Terminal.Role 相容；
- 不能因 DeviceId 有效而接受缺失 TerminalId；
- 不能根据设备名称、杆号、坐标或集合顺序修复引用。

## 5. GroundingPoint DTO

### 5.1 DTO 结构

```text
ProjectGroundingPointDto
├── GroundingPointId
├── TerminalId
├── Location
├── Number?
└── Note?
```

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| GroundingPointId | Guid | 是 | 工程内稳定唯一 ID |
| TerminalId | Guid | 是 | 工作地线关联的电气端子 |
| Location | string | 是 | 人工填写的位置说明 |
| Number | string? | 条件必填 | 工作票附图必填；勘察附图可不显示具体编号 |
| Note | string? | 否 | 补充说明 |

GroundingPoint DTO 不保存 DeviceId。设备归属在 Terminal 恢复后通过 OwnerId 解析，避免 TerminalId 与 DeviceId 形成两个可能冲突的事实源。

### 5.2 GroundingPoint 恢复方式

GroundingPoint 必须在完整 Topology 之后恢复：

```text
ProjectGroundingPointDto
  ↓ 字段预校验
解析 TerminalId
  ↓
校验 Terminal 存在及允许关联
  ↓
通过 Domain 专业对象创建/恢复入口构造 GroundingPoint
  ↓
注册 GroundingPointId
```

恢复规则：

- GroundingPointId 非空且未被其他工程对象占用；
- TerminalId 必须解析到当前 DrawingDocument 内的 Terminal；
- Location 去除首尾空白后必须非空；
- Number 和 Note 保留用户事实，不从图元文字反向生成；
- 编号必填和唯一性的校验需要明确的文档用途上下文；
- 当前文档用途合同未启用前，不应通过猜测文件名称决定工作票或勘察图；
- Terminal 缺失、类型不支持或引用失效时拒绝候选工程；
- 不自动改绑到同设备其他端子，也不根据坐标寻找最近端子。

GroundingPoint 恢复不调用状态评估，不要求 GroundSwitch 合入，也不根据 `IsEffectivelyGrounded` 决定是否创建对象。

### 5.3 与接地拓扑的隔离

以下数据不得写入 GroundingPoint DTO：

- GroundSwitch.SwitchState；
- ElectricalNodeType.Ground 的 NodeId；
- OperationalState；
- IsEffectivelyGrounded；
- 联锁违规代码；
- 自动推导的接地路径。

这些对象分别属于设备事实、固定拓扑或运行计算结果，与人工工作地线不是同一概念。

## 6. WorkScope DTO

### 6.1 DTO 结构

```text
ProjectWorkScopeDto
├── WorkScopeId
├── StartBoundary : ProjectBoundaryPointDto
├── EndBoundary   : ProjectBoundaryPointDto
├── Description
└── GroundingPointIds[]
```

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| WorkScopeId | Guid | 是 | 工程内稳定唯一 ID |
| StartBoundary | DTO | 是 | 起始电气边界 |
| EndBoundary | DTO | 是 | 终止电气边界 |
| Description | string | 是 | 人工确认的工作范围说明 |
| GroundingPointIds | Guid[] | 是 | 可为空，不使用 null |

### 6.2 保存边界

WorkScope DTO 只保存人工确认事实，不保存：

- 自动计算的两个边界之间路径；
- “范围内 DeviceId 集合”；
- 停电、带电或可能带电结果；
- 明显断开点检查结果；
- WorkScope 图形路径、矩形或颜色；
- GroundingPoint 的嵌套 DTO 副本。

### 6.3 恢复校验

- WorkScopeId 非空且唯一；
- StartBoundary 和 EndBoundary 均通过引用校验；
- 两个 BoundaryPoint 的 TerminalId 不同；
- Description 非空；
- GroundingPointIds 内部不重复；
- 所有 GroundingPointId 已在 Professional 恢复上下文中注册；
- GroundingPoint 的关联只表达业务关系，不自动验证其处于某条计算路径内；
- DTO 数组顺序不是 WorkScope 身份来源。

## 7. 稳定 ID 规则

### 7.1 工程级唯一性

以下 ID 必须在 DrawingDocument 的工程级 ID 注册表中稳定且不冲突：

- WorkScopeId；
- GroundingPointId；
- 现有 DeviceId、IntervalId、TerminalId、ElectricalNodeId、ConnectionId、PoleAttachmentId 等。

BoundaryPoint 不拥有 ID。它由 `WorkScopeId + StartBoundary/EndBoundary` 的结构位置确定。

### 7.2 引用规则

| 来源 | 目标 | 保存字段 | 是否允许回退解析 |
| --- | --- | --- | --- |
| BoundaryPoint | Device | DeviceId | 否 |
| BoundaryPoint | Terminal | TerminalId | 否 |
| GroundingPoint | Terminal | TerminalId | 否 |
| WorkScope | GroundingPoint | GroundingPointIds | 否 |
| WorkTicketData | WorkScope | WorkScopeId | 否 |
| WorkTicketData | GroundingPoint | GroundingPointId | 否 |
| WorkTicketData | Device/Terminal | 类型化 TargetReference | 否 |

不得通过名称、杆号、地线编号、数组序号、Layout 坐标或 JSON 对象引用恢复关系。

### 7.3 ID 生命周期

- 新建对象时生成 ID，首次保存后保持不变；
- 修改边界、端子关联、说明或编号不改变对象 ID；
- 删除后重新创建是新对象，应使用新 ID；
- DTO 往返、另存为和格式迁移不得重新生成已有 ID；
- Migration 只有在旧格式确实没有该类 ID 时才可生成，并必须在同一迁移事务中更新全部引用。

## 8. Domain、Topology、Professional 恢复顺序

### 8.1 固定 Pipeline

```text
.kvdrawing
  ↓
Manifest / FormatVersion
  ↓
DTO Migration
  ↓
Domain 身份与聚合恢复
  ├── DrawingDocument
  ├── Device / Pole / CableTermination
  └── RingCabinet 聚合
  ↓
Topology 恢复
  ├── ElectricalNode
  ├── Terminal
  ├── PoleAttachment
  ├── Connection
  └── OverheadLine
  ↓
Topology 完整性校验
  ↓
Professional 恢复
  ├── GroundingPoint
  └── WorkScope + BoundaryPoint
  ↓
Professional 跨区校验
  ↓
WorkTicketData 恢复（未来，可选）
  ↓
Layout 恢复
  ↓
Scene / EditorSession 重建
```

### 8.2 顺序理由

- BoundaryPoint 和 GroundingPoint 都依赖 Terminal，因此不能在 Topology 前恢复；
- BoundaryPoint 同时校验 DeviceId，因此 Device 聚合必须已经完整；
- WorkScope 引用 GroundingPoint，因此 GroundingPoint 先恢复；
- WorkTicketData 依赖 WorkScope、GroundingPoint、Device 和 Terminal，因此最后恢复业务引用；
- Layout 只有在所有语义对象通过校验后恢复，避免为无效 Professional 对象创建图面实例。

### 8.3 候选状态与原子性

恢复期间使用未发布的候选上下文：

```text
CandidateProject
├── CandidateDomain
├── DomainIdCatalog
├── CandidateProfessional
├── CandidateWorkTicketData?
└── CandidateLayout
```

任一 Professional 引用失败时：

- 不发布部分 GroundingPoint 或 WorkScope；
- 不建立 Scene 或 EditorSession；
- 不替换当前已打开工程；
- 不修改源 `.kvdrawing` 文件；
- 返回包含对象 ID 和字段路径的诊断。

## 9. Professional 与 Layout 的边界

Professional DTO 与 Layout DTO 分开保存：

```text
professional.groundingPoints[]
  └── GroundingPointId + TerminalId + 业务属性

layout.groundingPoints[]
  └── GroundingPointId + 图元偏移 + 标签偏移

professional.workScopes[]
  └── WorkScopeId + BoundaryPoint + 说明 + 地线引用

layout.workScopes[]
  └── WorkScopeId + 显示路径 + 标签偏移
```

跨区规则：

- 每个 Professional Layout 必须引用现存的同类型专业对象；
- 不允许重复 Layout；
- 是否要求每个专业对象都有 Layout，由对应 Rendering 是否可生成默认布局决定；
- 缺失 Layout 不得导致 TerminalId、BoundaryPoint 或 GroundingPoint 业务关联改变；
- Layout 不保存 Device、Terminal 或 Professional 对象副本；
- WorkScope 显示路径和 GroundingPoint 图元位置都使用毫米工程坐标，不保存 DIP 或 WPF 状态。

本阶段只设计 Professional 持久化，不冻结 WorkScopeLayout 和 GroundingPointLayout 的最终 DTO 字段。

## 10. 与未来 WorkTicketData 的隔离

### 10.1 单向引用

引用方向固定为：

```text
WorkTicketData
  ├──→ WorkScopeId
  ├──→ GroundingPointId
  ├──→ DeviceId
  └──→ TerminalId
```

Professional 不反向保存 WorkTicketId。这样同一图纸可脱离工作票继续编辑，也避免删除工作票时删除工程专业事实。

### 10.2 不复制事实

WorkTicketData 不重复保存：

- BoundaryPoint 的 DeviceId、TerminalId 和 Side；
- GroundingPoint 的 TerminalId、Location、Number 和 Note；
- Device 名称或 Terminal 角色；
- WorkScope Description。

工作票展示时通过稳定 ID 解析当前工程事实。若票面业务需要冻结签发时快照，应另行设计不可变签发快照合同，不能在当前可编辑 WorkTicketData 中悄悄复制字段。

### 10.3 生命周期约束

- 删除 WorkTicketData 不删除 WorkScope 或 GroundingPoint；
- 删除被 WorkTicketData 引用的 Professional 对象前必须显式解除或修改引用；
- Professional 对象修改后是否使已签发工作票失效，属于未来审批流程规则，本阶段不假设；
- SafetyMeasure 和 OperationStep 不进入 Professional DTO。

## 11. 保存事务与完整性校验

### 11.1 保存前快照

保存应从同一个 EditorSession 一致状态生成：

```text
DrawingDocument
  ├── Domain DTO
  └── Professional DTO

Runtime Layout
  └── Layout DTO

WorkTicketData?
  └── WorkTicket DTO
```

生成 DTO 后、写入 ZIP 前执行跨区校验：

- 各区域 DocumentId 一致；
- 全局 ID 唯一；
- Professional 引用的 Device、Terminal 和 GroundingPoint 存在；
- WorkTicketData 引用的 Professional 和 Domain 对象存在；
- Layout 引用的专业对象存在；
- 当前文档用途下 GroundingPoint 编号规则通过。

校验失败时不创建新的正式工程文件。

### 11.2 原子写入

继续沿用 M4 原子保存边界：

1. 生成完整候选 Project DTO；
2. 写入同目录临时文件；
3. 重新读取并校验候选容器；
4. 原子替换目标 `.kvdrawing`；
5. 保存成功后建立新 SavePoint 并清除 Dirty。

Professional 区不能单独覆盖保存；必须与 Domain、Layout 和 WorkTicketData 在同一工程事务中提交。

## 12. 版本与 Migration 预留

### 12.1 首次引入 Professional 区

当前 M4 文件合同没有 `professional` 区。正式加入时必须提升工程 `FormatVersion`，并提供相邻版本 Migration：

```text
M4 CurrentProjectDto
  ↓ AddProfessionalSectionMigration
M5 CurrentProjectDto
  ├── professional.groundingPoints = []
  └── professional.workScopes = []
```

旧文件没有专业对象事实，Migration 只能创建空集合，不能根据图形、文字、SwitchState、ElectricalState 或接地拓扑猜测 WorkScope/GroundingPoint。

### 12.2 版本职责

- Manifest.FormatVersion 继续是整个工程合同的首要迁移依据；
- 当前不为 Professional 单独增加 SchemaVersion，避免形成未经需要的双重版本源；
- WorkTicketData 启用后可以拥有独立 `schemaVersion`，但必须定义与工程 FormatVersion 的兼容矩阵；
- Professional DTO 字段或枚举发生不兼容变化时提升工程 FormatVersion；
- 仅运行时类重命名、内部重构或 Rendering 变化不提升格式版本。

### 12.3 Migration 规则

Migration 可以：

- 为旧版本增加空 Professional 区；
- 将旧版本中已有、语义完全等价且来源明确的字段移动到 Professional DTO；
- 在旧格式缺少 ID 且语义对象明确存在时生成稳定 ID，并同步更新所有引用；
- 把旧枚举编码显式映射到当前编码。

Migration 不可以：

- 根据坐标或文字猜测 TerminalId；
- 根据开关状态生成 GroundingPoint；
- 根据拓扑路径生成 WorkScope；
- 丢弃无法解析的专业对象后继续打开；
- 自动覆盖源工程文件；
- 在 Domain 构造器中散布文件版本判断。

### 12.4 当前版本读取规则

升级后的当前版本应把 `professional` 作为明确合同读取：

- 当前格式缺少必填 Professional 区时拒绝加载；
- 只有旧版本 Reader + Migration 可以把缺失区补为空集合；
- 未知必填 Side 枚举或未知 Professional 对象类型时拒绝加载；
- 不能依赖 JSON 未知字段忽略行为实现格式兼容。

## 13. DTO 校验分层

| 阶段 | 校验内容 | 失败结果 |
| --- | --- | --- |
| JSON 结构 | 必填区、字段类型、Guid/字符串格式、集合非 null | 拒绝读取 Current DTO |
| Professional 预校验 | ID 非空、内部不重复、文本基本约束、稳定枚举 | 不创建运行时专业对象 |
| Domain/Topology 绑定 | DeviceId、TerminalId 存在且类型/所有权正确 | 拒绝候选工程 |
| Professional 绑定 | GroundingPointIds 存在、边界不同、编号规则 | 拒绝候选工程 |
| WorkTicket 绑定 | WorkScope/GroundingPoint/TargetReference 有效 | 拒绝候选工程 |
| Layout 跨区 | LayoutKey 存在、无重复或孤立布局 | 不构建 Scene |

规则检查只验证已确认事实，不自动补齐缺失边界、地线编号或安全措施。

## 14. 往返验收建议

最小持久化往返场景应包含：

1. 建立包含环网柜、CableTermination、Pole 和 OverheadLine 的完整 Topology；
2. 在两个不同 Terminal 上创建一个 WorkScope 的 StartBoundary 和 EndBoundary；
3. 在有效 Terminal 上人工创建两个 GroundingPoint；
4. 关联其中至少一个 GroundingPoint 到 WorkScope；
5. 保存工程并完全释放原运行时对象；
6. 重新加载 Domain、Topology、Professional 和 Layout；
7. 验证所有稳定 ID、边界引用、端子关联、说明、编号和备注一致；
8. 验证未恢复自动范围路径、有效接地结果、Selection 或 Undo 历史；
9. 在未来启用 WorkTicketData 后，验证其引用指向恢复后的 Professional 对象而非副本。

负向用例至少覆盖：

- BoundaryPoint 的 DeviceId 或 TerminalId 缺失；
- Device 与 Terminal 所有者不匹配；
- WorkScope 两端为同一 Terminal；
- GroundingPoint 引用不存在的 Terminal；
- GroundingPointId 或 WorkScopeId 重复；
- WorkScope 引用不存在的 GroundingPoint；
- 当前格式缺少 Professional 区；
- 未知 Side 编码；
- WorkTicketData 引用不存在的专业对象。

## 15. 待确认问题

以下问题不影响 DTO 分区方向，但会影响后续字段校验：

1. 用于决定工作票附图/勘察附图的文档用途字段归属 Metadata 还是 WorkTicketData；
2. GroundingPoint.Number 的唯一性范围；
3. 同一 Terminal 是否允许多个 GroundingPoint；
4. 一个 GroundingPoint 是否允许被多个 WorkScope 引用；
5. Side 的正式稳定编码及与 Terminal.Role 的映射表；
6. WorkScopeLayout 和 GroundingPointLayout 是否为每个专业对象必填；
7. 已签发 WorkTicketData 是否需要不可变的专业事实快照。

这些问题确认前，不在 DTO 中加入临时布尔字段、自由枚举或重复引用。

## 16. 本阶段不实现

- 不修改 Domain、Topology、Rendering 或 Infrastructure 代码；
- 不增加 ProjectProfessionalDto、Mapper、Rehydrator 或 Migration 实现；
- 不升级当前工程 FormatVersion；
- 不修改 ProjectFileDocument、ProjectFileContainer 或 ProjectService；
- 不实现 WorkScopeLayout、GroundingPointLayout 或 Scene 图元；
- 不实现 WorkTicketData、SafetyMeasure 或 OperationStep 持久化；
- 不实现自动停电分析、自动工作范围或自动工作地线。
