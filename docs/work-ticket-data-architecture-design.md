# M6-A WorkTicketData 独立业务区架构设计

> 文档状态：设计稿，仅定义业务边界、稳定引用、持久化与编辑器接入原则，不实现代码<br>
> 编制日期：2026-08-12<br>
> 基线：`phase-5-professional-core`、当前 FormatVersion 2、`docs/distribution-professional-object-model-design.md`、`docs/professional-object-persistence-design.md`

## 1. 目标与范围

本设计为未来工作票数据建立独立的 `WorkTicketData` 业务区，使工作票、`SafetyMeasure` 和 `OperationStep` 可以引用现有工程事实，同时不污染设备、电气拓扑、专业对象、布局或渲染模型。

本阶段只冻结以下最小原则：

- WorkTicketData 是工程中的独立业务区，不是 Domain、Professional 或 Rendering 的扩展字段集合；
- WorkTicketData 通过稳定 ID 单向引用现有对象，不复制对象事实；
- 工作票、SafetyMeasure 和 OperationStep 均由用户显式创建和编辑；
- 所有跨区引用在工程级一致性校验中处理；
- 未经专业确认的票种、分类、执行状态、审批流程和步骤顺序不进入模型。

本设计不定义完整工作票模板，不实现代码，也不改变当前 FormatVersion 2。

## 2. 分层职责边界

| 区域 | 事实来源 | 与 WorkTicketData 的关系 | WorkTicketData 不得承担 |
| --- | --- | --- | --- |
| Domain | Device、Terminal、ElectricalNode 等设备事实 | 可通过类型化稳定 ID 被引用 | 设备副本、电气状态副本 |
| Topology | Connection 及外部电气连接 | 可用于引用校验，不用于自动生成票面内容 | 自动停电分析、自动操作路径 |
| Professional | WorkScope、BoundaryPoint、GroundingPoint | WorkTicketData 通过 WorkScopeId、GroundingPointId 引用 | BoundaryPoint 或 GroundingPoint 副本 |
| WorkTicketData | 工作票业务数据、人工措施、人工步骤 | 保存票面业务事实和跨区引用 | 修改 Domain、Topology 或 Professional |
| Layout | 毫米工程坐标与图面布局 | 可为未来票面标记提供独立布局 | 工作票业务事实和审批状态 |
| Rendering | Scene、Symbol、Overlay 和 WPF 显示 | 读取业务快照并表达 | 反推或保存 WorkTicketData |
| Editor | Command、Selection、Undo/Redo、Dirty | 负责受控修改和刷新 | 绕过业务入口直接写对象 |

引用方向固定为单向：

```text
WorkTicketData
  ├──→ WorkScopeId
  ├──→ GroundingPointId
  ├──→ DeviceId
  └──→ TerminalId

Domain / Topology / Professional
  └── 不反向保存 WorkTicketId
```

删除 WorkTicketData 不得删除任何 Domain、Topology 或 Professional 对象。工作票内容也不得驱动 SwitchState、Connection 或 GroundingPoint 自动变化。

## 3. WorkTicketData 根结构

### 3.1 建议运行时结构

```text
ProjectSession
├── DrawingDocument
│   ├── Domain / Topology
│   └── Professional
├── WorkTicketDataRoot
│   └── WorkTickets[]
│       └── WorkTicket
│           ├── WorkScopeIds[]
│           ├── GroundingPointIds[]
│           ├── SafetyMeasures[]
│           └── OperationSteps[]
└── RuntimeLayoutDocument
```

`WorkTicketDataRoot` 是工作票业务区的运行时根，不是第二份 `DrawingDocument`，也不拥有设备、拓扑或 Professional 对象。

职责划分：

- `DrawingDocument` 继续作为工程电气及 Professional 事实的唯一聚合根；
- `WorkTicketDataRoot` 管理工作票业务对象及区内不变量；
- `ProjectSession` 持有两者并协调跨区引用校验、保存、加载、CommandStack 和原子替换；
- 跨区规则不应塞入 Rendering，也不应让 WorkTicketData 直接修改 DrawingDocument 内部集合。

### 3.2 工作票数量

文件和运行时结构采用 `WorkTickets[]` 集合，以避免在业务确认前把合同锁死为“一工程一票”。这只代表技术结构可容纳多个工作票，不代表已经确认允许多个工作票，也不设置最大数量。

待专业确认：

- 一个工程允许零张、一张还是多张工作票；
- 多张票之间是否允许共享 WorkScope 或 GroundingPoint；
- 是否存在主票、子票、关联票或历史版本关系。

上述规则确认前，不实现“一工程只能一张票”或“必须至少一张票”的校验。

### 3.3 WorkTicket 最小身份

`WorkTicket` 建议定义为实体，并拥有稳定 `WorkTicketId`。理由是它需要独立编辑、保存、引用、Undo/Redo 和跨次加载保持身份。

当前只冻结以下最小字段边界：

| 字段 | 状态 | 说明 |
| --- | --- | --- |
| WorkTicketId | 已确定 | 非空、工程内稳定唯一 |
| WorkScopeIds | 已确定 | 对现有 WorkScope 的稳定 ID 引用集合，可为空 |
| GroundingPointIds | 已确定 | 对现有 GroundingPoint 的稳定 ID 引用集合，可为空 |
| SafetyMeasures | 已确定为子集合 | 不复制 Professional 数据 |
| OperationSteps | 已确定为子集合 | 只保存人工输入步骤 |
| 票号、标题、票种等票面字段 | 待确认 | 不在本阶段虚构名称、必填性或唯一性 |
| 状态、签发、许可、终结等流程字段 | 待确认 | 不在本阶段定义枚举或状态机 |

## 4. SafetyMeasure 边界

### 4.1 对象身份

`SafetyMeasure` 建议作为 WorkTicket 内部实体，而不是值对象，并拥有稳定 `SafetyMeasureId`。原因是未来需要对单条措施进行编辑、删除、排序或引用；稳定 ID 可避免依赖集合序号恢复身份。

这项决定只冻结身份与归属，不冻结措施分类或执行流程。

### 4.2 最小结构建议

```text
SafetyMeasure
├── SafetyMeasureId
├── Content                 人工输入内容
├── WorkScopeIds[]          可选稳定引用
├── GroundingPointIds[]     可选稳定引用
└── TargetReferences[]      可选类型化目标引用
```

类型化目标引用最小形态：

```text
DeviceTargetReference   { DeviceId }
TerminalTargetReference { TerminalId }
```

不得用一个无类型 Guid 同时表示 Device、Terminal、WorkScope 或 GroundingPoint。不得嵌套保存目标对象的名称、杆号、端子角色或坐标。

### 4.3 事实与执行状态边界

当前可确认的业务事实只有：

- 稳定 SafetyMeasureId；
- 人工输入的措施内容；
- 用户显式建立的稳定 ID 引用。

以下内容必须在专业规则确认后才能加入：

- SafetyMeasure 分类；
- 必填目标类型；
- 措施顺序及唯一性规则；
- 待执行、已执行、确认、解除等执行状态；
- 执行人、监护人、时间、签名和审批字段；
- 措施与 GroundingPoint、WorkScope 的强制数量关系。

若未来需要执行记录，应与措施定义事实明确区分；不得用一个含义模糊的布尔值同时表达计划、执行和确认。

## 5. OperationStep 边界

### 5.1 归属与身份

`OperationStep` 属于单个 WorkTicket 的内部实体，建议拥有稳定 `OperationStepId`。它是人工录入的票面业务数据，不是编辑器的 `ICommand`，也不是可直接执行的设备操作。

```text
OperationStep
├── OperationStepId
├── Content                 人工输入内容
├── TargetReferences[]      可选类型化稳定引用
├── WorkScopeIds[]          可选稳定引用，是否保留待确认
├── GroundingPointIds[]     可选稳定引用，是否保留待确认
└── SafetyMeasureIds[]      可选区内引用，是否允许待确认
```

`Content` 只保存人工输入原文。软件不得解析文本并修改 SwitchState，也不得根据设备状态、Topology 或 Professional 对象自动生成内容。

### 5.2 顺序边界

OperationStep 是否必须严格排序、顺序字段采用连续序号还是可重排键、是否允许并行或分组，当前均未确认。因此本阶段不冻结 `Sequence`、分组或前后依赖字段。

在正式实现前必须先确认：

- 步骤是否天然有序；
- 顺序是否必须连续且唯一；
- 删除或插入步骤是否需要整体重编号；
- 是否区分操作前、工作中和恢复送电步骤；
- 是否存在子步骤、并行步骤或条件步骤。

DTO 数组顺序在当前设计中不能被视为已经确认的业务顺序。

### 5.3 禁止与 Editor Command 混用

```text
OperationStep
  = 可持久化的工作票业务实体

ICommand / CommandStack
  = 运行时编辑历史
  = 当前不持久化
```

编辑 OperationStep 可以通过 Editor Command 进入 Undo/Redo，但保存时只保存最终业务数据，不保存 Execute、Undo、Redo 历史。

## 6. 与 Professional 的引用关系

### 6.1 引用规则

| WorkTicketData 来源 | 目标 | 保存内容 | 禁止复制 |
| --- | --- | --- | --- |
| WorkTicket | WorkScope | WorkScopeId | BoundaryPoint、Description |
| WorkTicket | GroundingPoint | GroundingPointId | TerminalId、Location、Number、Note |
| SafetyMeasure | WorkScope | WorkScopeId | WorkScope 全部字段 |
| SafetyMeasure | GroundingPoint | GroundingPointId | GroundingPoint 全部字段 |
| TargetReference | Device | DeviceId + 明确引用类型 | Device 名称和属性 |
| TargetReference | Terminal | TerminalId + 明确引用类型 | Terminal 角色和所有者副本 |

BoundaryPoint 只能通过 WorkScopeId 间接取得。WorkTicketData 不保存 BoundaryPoint 副本，也不直接以 `WorkScopeId + Start/End` 之外的临时坐标引用边界。

### 6.2 解析与显示

工作票展示时，从当前 ProjectSession 中按稳定 ID 解析对象并生成只读投影：

```text
WorkTicketData 引用
  ↓
Project-level Object Resolver
  ↓
DrawingDocument 中的当前对象
  ↓
View Model / Rendering snapshot
```

解析失败属于工程一致性错误，不能回退使用历史名称、数组位置、图面坐标或最近对象。

### 6.3 Professional 删除依赖

删除被 WorkTicketData 引用的 WorkScope 或 GroundingPoint 时，默认原则为拒绝删除并报告引用者。不得默认级联删除工作票、SafetyMeasure 或 OperationStep，也不得静默移除引用。

未来编辑器可以提供显式处理流程：

1. 展示全部引用位置；
2. 用户明确选择解除或替换引用；
3. 通过同一个 `CompositeCommand` 完成引用修改和 Professional 删除；
4. 任一步失败则整体回滚。

是否允许已签发工作票解除引用、是否需要作废票据，属于待确认审批规则。

## 7. 生命周期与一致性边界

### 7.1 创建与编辑

```text
用户显式创建 WorkTicket
  ↓
人工选择已有 WorkScope / GroundingPoint / Device / Terminal
  ↓
人工填写 SafetyMeasure / OperationStep 内容
  ↓
CommandFactory
  ↓
CommandStack
  ↓
WorkTicketDataRoot 业务入口
  ↓
跨区引用校验
  ↓
Scene / PropertyInspector 等只读投影刷新
```

禁止在创建工作票时自动创建 WorkScope、GroundingPoint、SafetyMeasure 或 OperationStep。

### 7.2 修改依赖对象

- Device、Terminal、WorkScope 或 GroundingPoint 的 ID 在普通编辑中保持稳定；
- 被引用对象的可编辑字段变化后，工作票显示投影可以刷新，但 WorkTicketData 不复制新值；
- 删除或更换稳定 ID 引用必须通过显式 Command；
- 跨区修改如果需要原子性，应使用 CompositeCommand，而不是分别修改后尝试补偿；
- WorkTicketData 不反写 Professional 关联集合。

### 7.3 保存与加载

创建、编辑和删除 WorkTicketData 后，最终状态随工程保存。加载完成后重新解析稳定引用，不恢复 Selection 或 Undo 历史。

## 8. Persistence 边界

### 8.1 文件区域

建议在 `document.json` 中增加独立可选区域：

```text
document.json
├── documentId
├── metadata
├── domain
├── professional
├── workTicketData
│   ├── schemaVersion
│   └── workTickets[]
└── layout
```

`workTicketData` 不嵌入 `domain` 或 `professional`。其 DTO 与运行时 WorkTicketData 对象分离，只保存标量、稳定 ID、明确引用判别值和集合。

### 8.2 保存内容

WorkTicketData DTO 可以保存：

- WorkTicketId；
- 已确认的工作票人工业务字段；
- WorkScopeId、GroundingPointId；
- SafetyMeasureId、人工内容及类型化稳定引用；
- OperationStepId、人工内容及类型化稳定引用；
- 未来经专业确认的顺序、分类或业务状态字段。

不得保存：

- Device、Terminal、BoundaryPoint、WorkScope 或 GroundingPoint 副本；
- Connection、ElectricalNode 或拓扑计算结果；
- Layout 坐标、DIP、DrawingVisual、Symbol 或 Scene；
- Selection、PropertyInspector 快照、CommandStack 或 Undo 历史；
- 自动停电分析、安全措施建议或操作顺序推导结果。

### 8.3 固定恢复顺序

```text
.kvdrawing
  ↓
Manifest / FormatVersion
  ↓
DTO Migration
  ↓
Domain
  ↓
Topology
  ↓
Professional
  ↓
WorkTicketData
  ↓
Layout
  ↓
Scene
  ↓
EditorSession
```

顺序原因：WorkTicketData 可能同时引用 Device、Terminal、WorkScope 和 GroundingPoint，只有前三个业务区完整恢复并通过校验后才能绑定工作票引用。

加载使用候选状态。任一 WorkTicketData 引用、ID 或合同校验失败时：

- 拒绝加载整个候选工程；
- 不发布部分工作票；
- 不替换当前有效 ProjectSession；
- 不自动删除无效引用或猜测目标；
- 返回包含 WorkTicketId、对象 ID 和字段路径的诊断。

### 8.4 FormatVersion 评估

当前工程格式为 FormatVersion 2，正式加入 WorkTicketData 区会改变 `ProjectFileDocument` 合同、恢复 Pipeline 和跨区校验，因此建议实现时升级到 FormatVersion 3，并提供 `v2 → v3` Migration。

建议迁移规则：

```text
FormatVersion 2
  ↓ AddEmptyWorkTicketDataSectionMigration
FormatVersion 3
  └── workTicketData.workTickets = []
```

迁移只能建立空 WorkTicketData，不能根据 WorkScope、GroundingPoint、Topology、SwitchState、Rendering 或文本标注生成工作票内容。

`workTicketData.schemaVersion` 是否保留为子合同版本，需要在实现前决定。若保留，必须定义它与工程 FormatVersion 的兼容矩阵，避免两个版本源各自决定同一字段语义。本阶段不修改 FormatVersion，也不冻结 schemaVersion 的具体数值。

### 8.5 保存事务

保存顺序建议为：

```text
Domain snapshot
  ↓
Topology snapshot
  ↓
Professional snapshot
  ↓
WorkTicketData snapshot
  ↓
Layout snapshot
  ↓
跨区完整性校验
  ↓
写入临时 .kvdrawing
  ↓
重新打开并验证
  ↓
原子替换正式文件
```

WorkTicketData 不能单独覆盖正式工程文件。只有整体保存成功后才建立 SavePoint 并清除 Dirty。

## 9. Editor 边界

### 9.1 修改路径

未来所有 WorkTicketData 修改必须遵循：

```text
UI input
  ↓
PropertyEditor / WorkTicket Editor
  ↓
CommandFactory
  ↓
ICommand
  ↓
CommandStack
  ↓
WorkTicketDataRoot 业务入口
  ↓
统一刷新
```

UI 不持有可变业务对象引用，不直接修改 WorkTicketData；Rendering 和 PropertyInspector 只消费值快照。

### 9.2 Command 最小方向

后续可按已确认用例设计：

- AddWorkTicketCommand / RemoveWorkTicketCommand；
- ChangeWorkTicketCommand；
- AddSafetyMeasureCommand / RemoveSafetyMeasureCommand / ChangeSafetyMeasureCommand；
- AddOperationStepCommand / RemoveOperationStepCommand / ChangeOperationStepCommand；
- 需要跨区原子修改时使用 CompositeCommand。

本阶段不冻结具体 Command 类或字段快照，因为工作票业务字段尚未确认。

### 9.3 Undo、Dirty 与刷新

- 成功修改才进入 CommandStack；
- 失败 Command 不污染历史或 Dirty；
- Undo/Redo 恢复相同稳定 ID 和相同引用；
- Undo 回到 SavePoint 时恢复 clean；
- Dirty 属于 EditorSession，不进入 WorkTicketData；
- 修改后依次刷新业务快照、Scene/HitTest（如有票面显示）、Selection 有效性和 PropertyInspector；
- Selection 使用 WorkTicketId、SafetyMeasureId 或 OperationStepId，不持有运行时对象引用。

## 10. 校验职责

| 校验层 | 负责内容 |
| --- | --- |
| WorkTicketDataRoot | 区内 ID 唯一、对象归属、已确认的必填字段和集合规则 |
| DrawingDocument | Device、Terminal、WorkScope、GroundingPoint 的自身规则与所有权 |
| ProjectSession / 跨区校验器 | WorkTicketData 的稳定 ID 引用存在且类型匹配 |
| Persistence DTO | JSON 结构、非空集合、判别值、格式版本 |
| Editor | 输入转换和错误展示，不复制业务规则 |
| Rendering | 无业务校验职责 |

工程级 ID 是否要求 WorkTicketId、SafetyMeasureId、OperationStepId 与所有 Domain/Professional ID 全局不冲突，尚待实现设计确认。无论采用全局目录还是分类型目录，引用解析必须同时包含引用类型，不得仅凭 Guid 猜测对象类别。

## 11. 明确禁止

本阶段及后续实现除非获得明确业务规则，不得：

- 自动生成 WorkTicket；
- 自动生成 SafetyMeasure；
- 自动生成 OperationStep；
- 根据 Topology、SwitchState 或 OperationalState 自动分析停电范围；
- 自动排列操作顺序；
- 自动执行设备操作；
- 自动建立审批、签发、许可或终结流程；
- 根据 Rendering、Symbol、坐标或文字反推业务事实；
- 自行创造票种、措施类型、执行状态或审批状态枚举；
- 在 WorkTicketData 中复制 Professional 或 Domain 对象；
- 删除 WorkTicket 时级联删除 WorkScope 或 GroundingPoint。

## 12. 待专业确认事项

### 12.1 工程与工作票关系

1. 一个工程允许零张、一张还是多张工作票；
2. 多张工作票是否可同时有效；
3. 是否存在主票、子票、关联票或票据版本；
4. 工作票删除、作废和历史保留的区别；
5. WorkTicketId 之外是否有票号及其唯一性范围。

### 12.2 WorkScope 与 GroundingPoint

1. 一张票允许引用多少个 WorkScope；
2. WorkScope 是否必须至少一个；
3. 一个 WorkScope 或 GroundingPoint 是否可被多张票引用；
4. WorkTicket 是否需要直接引用 GroundingPoint，还是只能通过 WorkScope 间接引用；
5. Professional 对象修改后，未签发或已签发工作票如何处理；
6. 删除被票据引用的 Professional 对象是否始终拒绝，或允许特定流程作废后解除引用。

### 12.3 SafetyMeasure

1. 正式分类及稳定编码；
2. 是否必须关联 WorkScope、GroundingPoint、Device 或 Terminal；
3. 是否有顺序、分组、数量和唯一性规则；
4. 是否区分计划内容、执行记录和确认记录；
5. 是否需要人员、时间、签名、监护或审批字段；
6. 是否允许自由文本措施与结构化措施并存。

### 12.4 OperationStep

1. 步骤是否必须有序，以及顺序的稳定表达；
2. 序号是否连续、唯一，插入删除时是否重编号；
3. 是否允许子步骤、并行步骤、条件步骤或分组；
4. 是否必须关联 Device、Terminal、SafetyMeasure、WorkScope 或 GroundingPoint；
5. 是否需要操作前后设备状态，但不得据此自动执行；
6. 是否区分计划步骤与实际执行记录；
7. 是否需要操作人、监护人、时间、确认和异常记录。

### 12.5 流程与版本

1. 票种、签发、许可、执行、间断、终结等流程是否在本软件范围内；
2. 已签发数据是否必须形成不可变快照；
3. 当前可编辑工程事实变化后，已签发票面如何保持可追溯性；
4. WorkTicketData 是否需要独立 schemaVersion；
5. WorkTicketData 的打印、JPG 导出和专业模板边界；
6. 工作票业务字段的法规、企业标准和版本来源。

## 13. M6 后续建议拆分

在上述专业问题逐项确认后，建议按以下顺序推进：

1. **M6-B：工作票字段与规则确认**

   由专业人员确认票数量、票面字段、SafetyMeasure 分类、OperationStep 顺序和生命周期；只更新设计，不编码。

2. **M6-C-A：WorkTicketData 实现设计冻结**

   冻结运行时根、实体字段、稳定 ID、跨区校验、Command 边界和最小验收标准。

3. **M6-C-B：WorkTicketData Domain/Application 基础实现**

   只实现已确认的根对象、WorkTicket 和跨区引用，不先做 UI 或自动推导。

4. **M6-D：FormatVersion 3 与 DTO 持久化**

   增加 WorkTicketData 区、`v2 → v3` 空集合迁移、原子保存加载和负向校验。

5. **M6-E：Editor 最小闭环**

   实现显式创建、编辑、删除、引用选择、Undo/Redo、Dirty 和只读属性投影。

6. **M6-F：SafetyMeasure / OperationStep 分阶段实现**

   仅在分类、顺序和业务状态确认后分别实现，避免一次引入未经确认的完整票务流程。

## 14. 本阶段不实现

- 不修改 Domain、Topology、Professional、Layout、Rendering、Editor 或 Persistence 代码；
- 不升级当前 FormatVersion 2；
- 不创建 WorkTicketData、WorkTicket、SafetyMeasure 或 OperationStep 类；
- 不创建 DTO、Migration、Command、UI、Scene Element 或打印模板；
- 不修改 DrawingDocument 或 ProjectSession；
- 不实现任何自动停电分析、自动措施、自动步骤或审批流程；
- 不提交 Git 变更。
