# M5-A 配电专业对象模型设计

> 文档状态：设计稿，仅定义专业对象及分层边界，不实现代码<br>
> 编制日期：2026-08-11<br>
> 依据：当前 Domain、Topology、Persistence 架构，以及 `docs/requirements.md`、`docs/equipment-model.md`、`docs/drawing-rule.md`

## 1. 目标与范围

本设计在现有 `DrawingDocument → Device / Terminal / ElectricalNode / Connection` 模型上，定义配电工作票附图需要的专业对象：

- `BoundaryPoint`：工作范围的单个电气边界；
- `WorkScope`：由两个明确电气边界描述的人工工作范围；
- `GroundingPoint`：用户人工添加并关联端子的工作地线；
- `SafetyMeasure`：未来工作票中对已确认安全措施的结构化记录；
- `OperationStep`：未来工作票中按顺序记录的人工操作步骤。

本设计只定义对象职责、稳定引用、保存边界和 Rendering 映射。它不实现自动停电分析、潮流计算、电气仿真、安全措施自动生成或操作步骤自动执行。

当前需求已经确认 WorkScope 和 GroundingPoint 属于 MVP 工程语义。SafetyMeasure 和 OperationStep 只建立最小兼容边界；其正式类型、必填字段和业务流程必须在工作票需求确认后再冻结。

## 2. 总体模型关系

```text
DrawingDocument
├── Devices
├── Terminals
├── ElectricalNodes
├── Connections
├── WorkScopes
│   └── WorkScope
│       ├── StartBoundary ──→ DeviceId + TerminalId
│       ├── EndBoundary   ──→ DeviceId + TerminalId
│       └── GroundingPointIds ──→ GroundingPoint
└── GroundingPoints
    └── GroundingPoint ──→ TerminalId

WorkTicketData（未来独立业务区）
├── WorkScopeIds ──→ DrawingDocument.WorkScopes
├── SafetyMeasures
│   └── SafetyMeasure ──→ WorkScope / GroundingPoint / Device / Terminal
└── OperationSteps
    └── OperationStep ──→ Device / Terminal / SafetyMeasure
```

核心原则：

- Device、Terminal、ElectricalNode 和 Connection 继续表达设备与电气拓扑；
- WorkScope 和 GroundingPoint 是用户确认的专业事实，不是拓扑计算结果；
- SafetyMeasure 和 OperationStep 不复制设备、端子或工作范围，只通过稳定 ID 引用；
- Layout 保存图面位置，Rendering 只生成可视结果；
- 任何图元、坐标、杆号或显示文字都不能代替 TerminalId 和 DeviceId 引用。

## 3. BoundaryPoint

### 3.1 职责

`BoundaryPoint` 是 WorkScope 内部值对象，表示工作范围的一端。它不是独立 Device，不单独存在，也不表示画布上的任意折点。

建议字段：

| 字段 | 必填 | 保存 | 说明 |
| --- | --- | --- | --- |
| DeviceId | 是 | 是 | 边界所属设备或聚合设备的稳定 ID |
| TerminalId | 是 | 是 | 明确的电气边界端子 |
| Side | 是 | 是 | 经确认的业务侧别，如线路侧或电源侧 |

本阶段不为 BoundaryPoint 增加独立 ID。它由所属 WorkScope 和起始/终止角色唯一定位，避免把内部值对象错误地提升为可独立编辑实体。

### 3.2 DeviceId 与 TerminalId 一致性

TerminalId 是边界电气位置的事实源，DeviceId 用于表达用户看到的设备语义并执行一致性校验：

- TerminalId 必须存在；
- DeviceId 必须存在；
- Terminal 的所有者必须是该 Device，或是该 Device 聚合内可公开作为边界的对象；
- Side 必须与 Terminal.Role 及设备结构一致；
- 仅作为物理支撑且没有 Terminal 的 Pole 不能成为 BoundaryPoint；
- 如果边界位于连续线路的中间杆位，必须先建立明确 Terminal 并拆分外部 Connection，不能引用线路 Layout 折点。

DeviceId 不是 TerminalId 的替代项。加载时禁止在 TerminalId 缺失后根据 DeviceId、名称或坐标猜测端子。

## 4. WorkScope

### 4.1 职责

`WorkScope` 表示用户人工确认的工作范围，由两个电气边界和文字说明构成。它不是矩形图元，也不是通过拓扑自动计算得到的设备集合。

建议字段：

| 字段 | 必填 | 保存 | 说明 |
| --- | --- | --- | --- |
| WorkScopeId | 是 | 是 | 工程内稳定唯一 ID |
| StartBoundary | 是 | 是 | 起始 BoundaryPoint |
| EndBoundary | 是 | 是 | 终止 BoundaryPoint |
| Description | 是 | 是 | 人工填写的工作范围说明 |
| GroundingPointIds | 否 | 是 | 与该范围关联的工作地线稳定 ID 集合 |

GroundingPointIds 只表示业务关联，不表示某个 GroundingPoint 一定处于两个边界之间，也不授权软件计算接地保护范围。

### 4.2 聚合与不变量

WorkScope 由 DrawingDocument 持有，并在文档一致性边界内校验：

- WorkScopeId 非空且工程内唯一；
- StartBoundary 和 EndBoundary 均有效；
- 两个 BoundaryPoint 不能引用同一个 TerminalId；
- Description 非空；
- GroundingPointIds 不重复且全部存在；
- 修改 WorkScope 不得自动修改 SwitchState、ElectricalState 或任何 Connection；
- 创建或修改 WorkScope 不得自动创建、删除或移动 GroundingPoint；
- 不保存“范围内设备列表”“已停电设备列表”等自动推导结果。

### 4.3 与 Topology 的关系

WorkScope 只引用 Topology，不拥有或修改 Topology：

```text
WorkScope
  ├── StartBoundary.TerminalId ──→ Terminal
  └── EndBoundary.TerminalId   ──→ Terminal

Terminal ──→ ElectricalNode / Connection
```

Topology 可以用于检查引用是否存在，也可以为将来的人工辅助浏览提供路径信息，但当前软件不得据此自动判定工作范围、带电范围或明显断开点。

## 5. GroundingPoint

### 5.1 职责

`GroundingPoint` 表示用户人工添加的工作地线。它与以下对象严格区分：

- `GroundSwitch`：设备内部接地刀闸，是 SwitchDevice；
- `ElectricalNodeType.Ground`：固定内部拓扑中的大地节点；
- `IsEffectivelyGrounded`：根据开关组合与拓扑计算的派生结果；
- 接地图元：GroundingPoint 的 Rendering 表达。

建议字段：

| 字段 | 必填 | 保存 | 说明 |
| --- | --- | --- | --- |
| GroundingPointId | 是 | 是 | 工程内稳定唯一 ID |
| TerminalId | 是 | 是 | 工作地线实际关联的明确端子 |
| Location | 是 | 是 | 面向用户的位置说明 |
| Number | 条件必填 | 是 | 工作票附图必填且同一文档内唯一 |
| Note | 否 | 是 | 现场条件或补充说明 |

GroundingPoint 不重复保存 DeviceId。设备归属通过 Terminal.OwnerId 解析；Property Inspector 可以显示派生的设备名称和端子侧别，但这些显示值不是第二份事实。

### 5.2 创建与校验

- GroundingPoint 只能由用户人工创建、改绑端子或删除；
- TerminalId 必须存在且适合挂接工作地线；具体允许的 Terminal.Role 列表须由专业规则确认后冻结；
- GroundingPointId 非空且工程内唯一；
- 工作票附图中 Number 必填且唯一；
- 现场勘察附图可不显示具体编号，但已填写编号仍属于工程事实；
- Location 非空，不用于替代 TerminalId；
- WorkScope、SwitchState、OperationalState、有效接地结果或拓扑变化均不得自动生成 GroundingPoint；
- GroundingPoint 端子失效时必须报告引用错误，不能静默移动到附近端子。

## 6. SafetyMeasure

### 6.1 定位

`SafetyMeasure` 是未来工作票业务区中的结构化安全措施记录，用于关联已由用户确认的措施及其目标对象。它不替代 GroundingPoint，也不表示软件自动计算出的安全结论。

当前只冻结最小结构：

| 字段 | 必填 | 保存 | 说明 |
| --- | --- | --- | --- |
| SafetyMeasureId | 是 | 是 | 工作票业务区内稳定唯一 ID |
| Description | 是 | 是 | 人工确认的措施内容 |
| WorkScopeId | 否 | 是 | 关联的工作范围 |
| GroundingPointIds | 否 | 是 | 关联的工作地线 |
| TargetReferences | 否 | 是 | 对 DeviceId 或 TerminalId 的类型化引用 |

`SafetyMeasureType`、执行状态、负责人、确认时间和审批流程尚无需求基线，本阶段不定义枚举或状态机。后续确认后通过工作票子合同版本增加，不能先以自由字符串隐藏实现。

### 6.2 TargetReference

TargetReference 必须是有类型的稳定引用，最小允许：

```text
DeviceReference   { DeviceId }
TerminalReference { TerminalId }
```

不得使用单个 Guid 字段同时表示多种对象，也不得保存运行时对象引用。加载时必须按引用类型和 ID 同时校验。

SafetyMeasure 引用 GroundingPoint 时，只表示“该工作地线属于或支持该措施”；GroundingPoint 的电气位置仍由自己的 TerminalId 决定。

### 6.3 规则边界

- SafetyMeasure 只能记录用户确认的内容；
- 不根据 WorkScope、拓扑、开关状态或运行状态自动创建措施；
- 不因 SafetyMeasure 创建而修改设备状态；
- 不把规则检查结果直接保存为 SafetyMeasure；
- 删除被引用对象前必须显式处理 SafetyMeasure 引用，不静默丢弃。

## 7. OperationStep

### 7.1 定位

`OperationStep` 是未来工作票业务区中的有序人工步骤。它描述计划或票面上的操作内容，不是可执行命令，也不是 CommandStack 的编辑命令。

当前只冻结最小结构：

| 字段 | 必填 | 保存 | 说明 |
| --- | --- | --- | --- |
| OperationStepId | 是 | 是 | 工作票业务区内稳定唯一 ID |
| Sequence | 是 | 是 | 工作票内明确顺序 |
| Description | 是 | 是 | 人工确认的操作内容 |
| TargetReferences | 否 | 是 | 关联 DeviceId 或 TerminalId |
| SafetyMeasureIds | 否 | 是 | 与该步骤相关的安全措施引用 |

本阶段不定义“待执行、已执行、跳过”等状态，不保存自动执行结果，也不根据 Description 解析或修改 SwitchState。

### 7.2 与编辑命令的区别

```text
OperationStep
  = 工作票业务数据
  = 保存到 workTicket 区

ICommand / CommandStack
  = 编辑器撤销重做机制
  = 仅存在于运行时
  = 当前不保存
```

两者不得共享 ID、历史记录或执行接口。用户编辑 OperationStep 时，可以由 `ChangePropertyCommand` 等编辑器命令实现撤销，但保存的是最终 OperationStep 数据，不保存该编辑命令。

### 7.3 校验边界

- OperationStepId 非空且唯一；
- Sequence 为正数且在同一工作票内唯一；
- 保存时按 Sequence 排序，不依赖 JSON 数组位置恢复身份；
- Description 非空；
- 所有 TargetReference 和 SafetyMeasureId 必须存在且类型正确；
- 步骤顺序不能自动推导设备状态、工作范围或接地结果；
- 是否要求 Sequence 连续、是否允许并行步骤、是否需要操作前后状态，等待工作票需求确认。

## 8. 专业对象的职责边界

| 对象 | 属于哪一层 | 负责 | 不负责 |
| --- | --- | --- | --- |
| BoundaryPoint | Domain 值对象 | 表达 WorkScope 单端的 Device/Terminal/Side | 坐标、路径计算 |
| WorkScope | Domain 工程对象 | 保存人工边界、说明及地线关联 | 自动计算范围、停电传播 |
| GroundingPoint | Domain 工程对象 | 保存人工工作地线及 TerminalId | 代替接地刀闸、自动判定有效接地 |
| SafetyMeasure | 未来 WorkTicket 业务对象 | 保存人工确认的措施和结构化引用 | 自动生成措施、修改设备状态 |
| OperationStep | 未来 WorkTicket 业务对象 | 保存人工步骤及顺序 | 自动执行、Undo 历史 |
| Layout | Layout 数据 | 保存专业对象的图面偏移与路径 | 保存电气语义 |
| Rendering | 临时表现 | 根据 Domain + Layout 绘制 | 保存业务状态、修改 Domain |

## 9. 持久化设计

### 9.1 工程数据

当前工程文件后续应增加：

```text
document.json
├── domain
│   ├── workScopes[]
│   └── groundingPoints[]
├── layout
│   ├── workScopes[]
│   └── groundingPoints[]
└── workTicket
    ├── schemaVersion
    ├── workScopeIds[]
    ├── safetyMeasures[]
    └── operationSteps[]
```

其中：

- WorkScope 与 GroundingPoint 属于图纸工程 Domain，保存完整结构化事实；
- SafetyMeasure 与 OperationStep 属于未来 `workTicket` 独立业务区；
- WorkTicket 通过稳定 ID 引用 Domain 对象，不复制 Device、Terminal、WorkScope 或 GroundingPoint；
- `workTicket.schemaVersion` 与工程 FormatVersion 协同，但允许子合同独立演进；
- 当前工程格式尚未实现上述字段，正式加入时必须升级 DTO 合同并提供迁移测试。

### 9.2 保存的 Layout 数据

建议预留：

| Layout | 关联键 | 保存内容 |
| --- | --- | --- |
| WorkScopeLayout | WorkScopeId | 边界标签偏移、说明文字偏移、人工调整的显示路径 |
| GroundingPointLayout | GroundingPointId | 图元相对端子偏移、编号标签偏移 |
| SafetyMeasureAnnotationLayout | SafetyMeasureId | 仅在确认需要图面标注时保存注记位置 |
| OperationStepAnnotationLayout | OperationStepId | 仅在确认需要图面序号时保存注记位置 |

WorkScopeLayout 的显示路径不是电气范围事实；删除 Layout 后仍应能够从 BoundaryPoint 的 TerminalId 生成默认显示。GroundingPointLayout 也不能改变工作地线所关联的 TerminalId。

### 9.3 不保存的运行结果

以下内容均为运行时派生或临时状态，不进入工程数据：

- WorkScope 两端之间的自动拓扑路径；
- 自动推导的“范围内设备集合”；
- 自动推导的停电、带电或可能带电结论；
- GroundingPoint 的屏幕坐标和 WPF Geometry；
- 从 Terminal Layout 计算出的图元锚点；
- SwitchAssembly 的 OperationalState、IsEffectivelyGrounded 和违规结果；
- SafetyMeasure 的规则检查结果或自动完成状态；
- OperationStep 的运行执行结果；
- Selection、HitTestIndex、DrawingScene、DrawingVisual 和 CommandStack 历史。

如果未来业务明确要求保存审批、执行或确认记录，应作为工作票业务事实单独建模，不能复用上述运行计算结果字段。

## 10. 加载与引用恢复顺序

专业对象加入持久化后，建议恢复顺序为：

```text
Device / RingCabinet 聚合
  ↓
ElectricalNode
  ↓
Terminal
  ↓
Connection / OverheadLine / PoleAttachment
  ↓
GroundingPoint
  ↓
WorkScope + BoundaryPoint 引用绑定
  ↓
WorkTicketData
  ├── SafetyMeasure
  └── OperationStep
  ↓
专业对象完整性校验
  ↓
Layout 恢复
  ↓
Scene 重建
```

GroundingPoint 先于 WorkScope 恢复，使 WorkScope.GroundingPointIds 可以一次性校验。SafetyMeasure 依赖 WorkScope、GroundingPoint、Device 和 Terminal；OperationStep 最后绑定 SafetyMeasure 引用。

任一必填引用缺失时应拒绝候选工程，不根据显示名称、编号、杆号、Sequence 或坐标修复引用。

## 11. Rendering 映射

### 11.1 WorkScope

```text
WorkScope
  ↓ BoundaryPoint.TerminalId
Terminal Layout Anchor
  ↓ WorkScopeLayout
WorkScope Scene Elements
```

Rendering 应显示两个边界标记、工作范围说明和必要的范围引导线。范围图形不是固定矩形；不得从范围图形反向修改 BoundaryPoint。规范中的“明显断开点外少部分”没有固定距离，Rendering 不自行生成数值规则。

### 11.2 GroundingPoint

```text
GroundingPoint.TerminalId
  ↓
Terminal Layout Anchor
  ↓ GroundingPointLayout.Offset
Grounding Symbol + Number/Location Label
```

- 工作票附图显示工作地线位置和编号；
- 现场勘察附图显示位置，具体编号按规范隐藏；
- GroundingPoint 图元与 GroundSwitch 图元使用不同语义和选择目标；
- Rendering 不计算有效接地，也不根据开关状态改变 GroundingPoint 是否存在。

### 11.3 SafetyMeasure 与 OperationStep

SafetyMeasure 和 OperationStep 默认只进入属性面板或工作票视图，不自动出现在附图上。只有后续规范明确要求图面标注时，才通过独立 AnnotationLayout 生成 SceneElement。

SymbolRenderContext 可以接收当前文档类型和只读显示策略，但不得保存安全措施状态或反写工作票数据。

## 12. 编辑与命令边界

未来编辑流程统一为：

```text
用户选择 Terminal / Device
  ↓
专业对象编辑器校验输入
  ↓
创建 Editor Command
  ↓
修改 WorkScope / GroundingPoint / WorkTicketData
  ↓
更新对应 Layout（如需要）
  ↓
重建 Scene
```

建议命令边界：

- `CreateWorkScopeCommand`：一次提交两个 BoundaryPoint 和 Description；
- `ChangeWorkScopeCommand`：修改边界或说明，不改变设备状态；
- `AddGroundingPointCommand`：人工选择 Terminal、填写位置与编号；
- `MoveGroundingPointLayoutCommand`：只修改图面偏移；
- `ChangeSafetyMeasureCommand`：修改未来工作票业务记录；
- `ReorderOperationStepCommand`：修改步骤顺序。

命令必须支持 Undo/Redo，但 CommandStack 历史仍不持久化。复合操作失败时整体回滚，不能留下只有 Layout 或只有 Domain 的半成品。

## 13. 专业校验建议

### 13.1 阻断错误

- WorkScope 缺少任一 BoundaryPoint；
- BoundaryPoint 引用不存在或不匹配的 Device/Terminal；
- WorkScope 两端引用同一 Terminal；
- GroundingPoint 引用不存在的 Terminal；
- 工作票附图中的 GroundingPoint 缺少编号或编号重复；
- SafetyMeasure 或 OperationStep 存在失效引用；
- OperationStepId 或 Sequence 重复。

### 13.2 警告或待人工确认

- WorkScope 是否已经画到明显断开点及其外侧少部分；
- 工作范围外设备的带电状态是否已人工确认；
- GroundingPoint 的端子角色是否符合具体现场措施；
- SafetyMeasure 是否完整覆盖工作票要求；
- OperationStep 的顺序是否符合现场规程。

上述项目需要专业人员判断。软件可以提示，但不能自动修复、自动生成措施或自动改变设备状态。

## 14. 为未来工作票功能预留

未来 `WorkTicketData` 建议作为独立聚合，至少具备：

- WorkTicketId；
- SchemaVersion；
- 对一个 DrawingDocument 的引用；
- WorkScopeIds；
- SafetyMeasures；
- OperationSteps。

票号、任务、人员、许可、时间、签名、审批状态等字段尚未形成当前需求基线，本设计不提前定义。启用时应：

1. 先形成工作票字段和流程需求；
2. 冻结 WorkTicket 子合同及枚举；
3. 增加 Domain/Application 校验；
4. 增加 DTO 版本与迁移；
5. 增加编辑命令、属性视图和输出规则；
6. 用脱敏真实工作票进行专业验收。

图纸可以独立于工作票存在；删除或解绑 WorkTicketData 不得删除 DrawingDocument 中的设备、Topology、WorkScope 或 GroundingPoint。

## 15. 待确认问题

以下问题在实现前必须取得业务或规范确认：

1. SafetyMeasure 的正式分类、编号、负责人、确认时间和状态集合；
2. OperationStep 是否只表示计划步骤，还是需要保存执行、复诵、监护等记录；
3. OperationStep 是否需要结构化保存目标 SwitchState，及其与现有联锁校验的关系；
4. GroundingPoint 允许关联的 Terminal.Role 白名单；
5. 同一 Terminal 是否允许多组工作地线；
6. GroundingPoint 编号唯一性范围是整张图、同一 WorkScope 还是同一工作票；
7. 一个 GroundingPoint 是否允许关联多个 WorkScope；
8. WorkScope 是否允许一个工作票包含多个独立范围；
9. SafetyMeasure 和 OperationStep 是否必须出现在附图，以及具体图元和标注规则；
10. 工作票与现场勘察记录是否共用同一 WorkTicketData，还是采用不同子合同。

在这些问题确认前，M5 后续实现应优先完成已冻结的 BoundaryPoint、WorkScope 和 GroundingPoint，不自行补充工作票流程状态机。

## 16. 本阶段不实现

- 不修改现有 Domain、Topology 或 Persistence 代码；
- 不实现 BoundaryPoint、WorkScope、GroundingPoint、SafetyMeasure 或 OperationStep 类；
- 不升级工程文件格式或 DTO；
- 不实现专业对象编辑器、Undo/Redo 命令或 Property Inspector；
- 不实现 WorkScope、GroundingPoint 或工作票图元；
- 不实现自动停电分析、自动范围计算、自动安全措施或自动操作；
- 不实现工作票审批、许可、签名、人员或执行流程。
