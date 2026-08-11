# M3-B-2-A 属性查看器架构设计

> 文档状态：设计稿，仅定义 Property Inspector 架构，不实现代码或 UI<br>
> 编制日期：2026-08-11<br>
> 依据：`docs/editor-architecture.md`、当前 `SelectionReference`、`SelectionManager` 以及 Domain / Layout / Rendering 分层

## 1. 目标与边界

Property Inspector 用于查看当前选中对象的业务属性、布局属性和必要的渲染诊断信息，并为后续受控属性编辑提供统一入口。

基本链路为：

```text
SelectionManager.Selected
        ↓
SelectionReference
        ↓
SelectionObjectResolver
        ↓
ResolvedSelection（本次查询临时对象）
        ↓
PropertyProjector
        ↓
PropertyInspectorViewModel（值快照）
        ↓
Property Panel
```

本阶段只设计，不修改 Domain、Layout、Rendering、Interaction 或 Desktop 代码，不新增属性面板控件，也不实现属性编辑命令。

必须保持：

- Property Panel 不直接修改 Domain 或 Layout。
- UI 不保存 Domain、Layout、SceneElement、DrawingVisual 等对象引用。
- Rendering 不保存属性面板状态或用户输入。
- Property Inspector 不是工程文件事实源，也不参与电气状态推导。

## 2. 职责划分

### 2.1 Property Inspector 负责

- 监听当前单选 `SelectionReference` 的变化。
- 通过稳定 ID 解析当前修订中的 Domain、Layout 和渲染描述。
- 将不同来源投影为有分组、有类型、有只读/可编辑标记的属性行。
- 显示无法解析、属性无效或对象已删除等状态。
- 后续把已确认的新值转换为 Editor Command 请求。
- 在命令成功、撤销、重做或场景刷新后重新读取属性快照。

### 2.2 Property Inspector 不负责

- 直接设置 Domain 属性、调用私有 setter 或修改 Layout 集合。
- 通过反射暴露对象的全部公共属性。
- 判断电气连接是否正确、推导运行方式或有效接地。
- 修改 SymbolDefinition、SymbolRenderContext、SceneElement 或 WPF Visual。
- 保存 Selection、属性 ViewModel、编辑缓存或 Rendering 信息到工程文件。
- 自行维护第二份业务对象状态。

## 3. 属性来源

### 3.1 Domain 属性

Domain 属性是业务事实或稳定身份，始终从当前 EditorSession 中的领域对象重新读取。

典型内容包括：

| 对象 | Domain 属性示例 |
| --- | --- |
| RingCabinet | Id、DisplayName、CompositionKind、MainBusNodeId、间隔数量 |
| RingCabinetInterval | IntervalId、Sequence、DisplayName、IntervalKind、GroundingStructureKind、ExternalTerminalId |
| SwitchDevice | Id、DisplayName、DispatchNumber、SwitchKind、SwitchState、TerminalIds、ParentId |
| Pole | Id、PoleNumber、DisplayName、PoleType |
| PoleAttachment | AttachmentId、PoleId、AttachedDeviceId |
| Connection | Id、ConnectionType、两端 TerminalId、DisplayName、电压等级及人工状态 |
| OverheadLine | ConnectionId、LineModel、LengthMeters、SupportPoleIds、ContinuationState 和说明 |
| CableTermination | Id、DisplayName、电缆侧/架空侧 TerminalId |

Domain 属性不得从图形文字或 Symbol 类型反向解析。对象 ID、类型、所有权、TerminalId、ElectricalNodeId、派生的 CompositionKind 等结构信息通常只读。

### 3.2 Layout 属性

Layout 属性描述图面实例的位置和尺寸，只从与 SelectionReference 稳定 ID 对应的 Layout 记录读取。

典型内容包括：

- PoleLayout 的 Position、Width、Height 和 LabelOffset。
- AttachmentLayout 的相对 Offset、Width、Height 和 LabelOffset。
- RingCabinetLayout 的 Position、Width、Height、MainBusY 和 LabelOffset。
- RingCabinetIntervalLayout 的 RelativePosition、Width、Height 及标签偏移。
- RingCabinetSwitchLayout 的 RelativePosition、Width、Height 和 LabelOffset。
- OverheadLineLayout 的显示 Start、End、ContinuationOffset；这些只表示当前 MVP 的绘制布局，不替代 Connection 端点。

位置、偏移和标签位置未来可以通过 Layout Command 编辑。固定模板尺寸是否开放编辑应按具体图元逐项确认，不能因其存在于 Layout 就默认全部可编辑。

### 3.3 Rendering 信息

Rendering 信息只用于解释当前显示结果或辅助诊断，由当前场景、命中索引和 Symbol 映射即时提供。例如：

- 当前 SelectionTargetKind。
- 当前 SymbolKind 或复合 Symbol 类型。
- 当前命中边界和命中优先级。
- 当前显示使用的 `SymbolVisualState`。
- 当前图元在场景中的显示层级或可见性。
- 场景生成错误或缺失 Layout 的只读提示。

Rendering 信息必须全部只读。颜色、线宽、命中范围和 SymbolKind 不是普通业务属性；用户不能通过属性面板自由覆盖专业图元规则。

Property Inspector 不持有 DrawingScene 或 DrawingVisual。需要渲染信息时，由只读 `RenderingDescriptorProvider` 根据当前场景修订返回一个短生命周期的值对象；新场景生成后旧描述立即失效。

### 3.4 派生与校验信息

Domain 评估或规则校验结果可以作为单独的“状态/校验”只读分组显示，例如：

- `OperationalState`。
- `IsEffectivelyGrounded`。
- `ViolatedRuleCodes`。
- 聚合结构或引用校验结果。

这些信息必须实时重新计算或读取本次评估快照，不进入可编辑属性，不写回 Domain、Layout、Rendering 或工程文件。

## 4. Selection 到属性模型的解析

### 4.1 当前 SelectionReference

当前实现支持以下选择类型：

| SelectionTargetKind | ObjectId 含义 | ParentId 用途 |
| --- | --- | --- |
| Device | DeviceId 或内部 SwitchDeviceId | 可指向 IntervalId |
| RingCabinet | RingCabinet.Id | 无 |
| RingCabinetInterval | IntervalId | CabinetId |
| PoleAttachment | AttachmentId | PoleId |
| Connection | ConnectionId | 无 |

Property Inspector 只保存 `SelectionReference` 值，不保存解析得到的对象引用。`ParentId` 用于缩小内部聚合对象的查找范围和校验所有权，不能替代 ObjectId。

### 4.2 SelectionObjectResolver

建议由 Application/Editor 层提供 `SelectionObjectResolver`。它读取当前 EditorSession，而不是读取 WPF 视觉树。

解析顺序：

1. 读取 SelectionReference 和当前 DocumentRevision。
2. 按 SelectionTargetKind 选择显式 Resolver。
3. 按 ObjectId 查找 Domain 对象或关系。
4. 使用 ParentId 校验所属柜体、间隔或杆塔关系。
5. 按相同稳定 ID 查找对应 Layout。
6. 可选读取与当前 SceneRevision 匹配的 RenderingDescriptor。
7. 返回本次查询使用的 `ResolvedSelection`。

`ResolvedSelection` 是短生命周期解析结果，可以在投影期间临时引用 Domain/Layout，但不得传给 View 长期保存。它至少包含：

- 原始 SelectionReference。
- DocumentRevision 和可选 SceneRevision。
- 已解析对象的类型标识。
- Domain、Layout 和 Rendering 三类临时解析结果。
- 缺失对象、所有权不匹配或版本过期等解析问题。

对象已删除、SelectionReference 无效或 ParentId 不匹配时，Resolver 返回明确的 Unresolved 结果。SelectionManager 随后应清除失效选择，属性面板显示“未选择对象”或短暂错误，不得保留旧对象属性。

### 4.3 显式 PropertyProjector

不同对象类型使用显式 Projector，例如：

- RingCabinetPropertyProjector。
- RingCabinetIntervalPropertyProjector。
- SwitchDevicePropertyProjector。
- PolePropertyProjector。
- PoleAttachmentPropertyProjector。
- ConnectionPropertyProjector。
- OverheadLinePropertyProjector。

Projector 将解析结果复制为属性值快照，不把 Domain/Layout 对象直接暴露给 UI。禁止使用通用反射自动生成全部属性，因为这会意外开放内部 ID、固定拓扑或不允许编辑的字段。

## 5. Property View Model

### 5.1 顶层模型

建议 PropertyInspectorViewModel 只保存：

- 当前 SelectionReference。
- 生成快照时的 DocumentRevision 和 SceneRevision。
- 对象类型标题和显示名称快照。
- 有序 PropertySection 集合。
- IsResolved、HasErrors 等显示状态。

它不保存 Device、RingCabinetInterval、Layout、SceneElement、SymbolDefinition 或 DrawingVisual 引用。

Selection 变化、DocumentChanged、Undo、Redo 或 SceneChanged 后，旧 PropertyInspectorViewModel 应整体替换或重新投影，不能依赖双向绑定让旧对象自行变化。

### 5.2 属性分组

建议统一分组：

1. 基本信息：对象类型、名称、编号。
2. 专业属性：开关类型、状态、线路型号等 Domain 信息。
3. 拓扑与归属：父对象、端子、节点、Connection 端点，只读。
4. 布局：位置、偏移、尺寸和标签位置。
5. 显示信息：Symbol、命中边界和渲染状态，只读。
6. 状态与校验：派生评估和违规提示，只读。

无适用属性的分组不显示，不用空字符串占位。

### 5.3 PropertyRow

每个属性行建议包含：

| 字段 | 作用 |
| --- | --- |
| PropertyKey | 稳定属性键，用于选择 Command 工厂，不使用显示名称作为键 |
| DisplayName | 中文显示名称 |
| Category | Domain、Layout、Rendering 或 Derived |
| ValueType | Text、Number、Enum、Boolean、Id、IdList 等 |
| DisplayValue | 已格式化的只读显示值 |
| EditValue | 未来编辑缓冲值，仅存在于 UI 会话 |
| IsReadOnly | 是否允许发起编辑 |
| Unit | mm、m、kV 等显示单位 |
| ValidationMessage | 输入或命令校验提示 |
| EditorKind | TextBox、EnumSelector 等编辑器提示，不保存控件引用 |

PropertyKey 必须与对象类型共同确定命令语义，例如 `SwitchDevice.SwitchState` 和 `RingCabinetLayout.PositionX`。UI 不能把任意 PropertyKey 和任意对象组合提交。

## 6. 显示与编辑边界

### 6.1 始终只读

以下属性在 MVP 中始终只读：

- 所有稳定 ID、ParentId、TerminalId、ElectricalNodeId 和 MainBusNodeId。
- DeviceType、SwitchKind、IntervalKind、PoleType、ConnectionType。
- CabinetCompositionKind 等派生分类。
- SwitchAssembly 成员关系和 RuleSetRef。
- 固定端子数量、端子角色和柜内 ElectricalNode 拓扑。
- IntegratedFeederInterval 已创建后的 GroundingStructureKind；如未来允许变更，应使用结构替换命令，而非普通字段编辑。
- OperationalState、IsEffectivelyGrounded、ViolatedRuleCodes。
- SymbolKind、SymbolVisualState、颜色、线宽、命中边界和层级。
- 根据 Domain + Layout 计算得到的实际端子屏幕坐标。

### 6.2 未来可编辑的 Domain 属性

只有当前模型明确支持且已有业务语义的字段可以开放：

| 对象 | 可编辑属性候选 | 命令边界 |
| --- | --- | --- |
| RingCabinet | DisplayName | RenameDeviceCommand |
| RingCabinetInterval | DisplayName | RenameIntervalCommand |
| SwitchDevice | DisplayName、DispatchNumber、SwitchState | 专用设备属性/状态命令 |
| Pole | PoleNumber、DisplayName | UpdatePolePropertiesCommand |
| Connection | DisplayName、人工 ElectricalState | UpdateConnectionPropertiesCommand |
| OverheadLine | LineModel、LengthMeters、延续状态及说明 | UpdateOverheadLinePropertiesCommand |
| CableTermination | DisplayName | RenameDeviceCommand |

SupportPoleIds、Connection 端点、PoleAttachment 所属关系、环网柜间隔组成等虽然可能在后续改变，但必须使用显式结构命令，不作为普通文本属性直接编辑。

### 6.3 未来可编辑的 Layout 属性

以下字段可通过专用 Layout Command 开放：

- 设备绝对位置。
- 附属设备、间隔和柜内开关的相对位置。
- 标签偏移。
- 允许调整的线路显示路径或端点显示位置。

图元宽高、主母线位置等模板几何第一版默认只读。只有专业图元规范允许且不会破坏组合结构时，才能为具体对象增加显式尺寸命令。

### 6.4 Rendering 属性

Rendering 类别没有可编辑项。若未来需要主题、显示比例或调试开关，应进入全局显示设置或用户偏好，不作为单个业务对象属性，也不进入 Domain。

## 7. 与 Command 和 Undo/Redo 的关系

### 7.1 编辑提交流程

```text
用户在 PropertyRow 输入新值
        ↓
Property Inspector 做格式和必填校验
        ↓
PropertyEditRequest
  - SelectionReference
  - PropertyKey
  - NewValue
  - BaseDocumentRevision
        ↓
PropertyCommandFactory
        ↓
具体 Editor Command
        ↓
CommandDispatcher 执行 Domain 或 Layout 修改
        ↓
DocumentChanged
        ↓
Resolver 重新解析并生成新的属性快照
```

UI 的格式校验只负责数字、枚举和必填等输入问题；专业不变量仍由 Command 和 Domain 校验。UI 校验成功不代表命令一定成功。

### 7.2 Command 工厂边界

PropertyCommandFactory 必须采用显式白名单：

- 根据 SelectionTargetKind、已解析对象类型和 PropertyKey 选择命令。
- 拒绝未知属性、只读属性和类型不匹配的值。
- 命令只携带稳定 ID、原值、新值和必要修订信息。
- 命令不携带 PropertyRow、控件、Domain 对象或 WPF 对象引用。

Domain 属性命令只修改 Domain；布局属性命令只修改对应 Layout。需要同时修改结构和 Layout 的操作不能伪装成普通 PropertyEditRequest，应进入专用原子结构命令。

### 7.3 Undo/Redo

每次成功提交的属性变更形成一条 Editor Command 历史：

- 文本编辑在失焦、回车或明确应用时提交为一条命令，不按每个字符记录。
- 枚举或状态选择确认后形成一条命令。
- 命令保存明确的前值和后值，用于 Undo/Redo。
- 命令失败不进入历史，属性面板继续显示当前事实值和错误提示。
- Undo/Redo 后通过 DocumentChanged 重新投影属性，不由 ViewModel 手工回填旧值。
- Undo 后执行新命令时，RedoStack 按编辑器统一规则清空。

属性面板的展开状态、输入焦点、临时未提交文本和 Selection 变化不进入文档 Undo History。

### 7.4 并发与过期快照

PropertyEditRequest 携带 BaseDocumentRevision。若对象在编辑期间已被其他命令修改、删除或替换，CommandDispatcher 应拒绝过期请求并要求 Inspector 刷新。MVP 不自动合并冲突值。

## 8. 生命周期与刷新

Property Inspector 订阅编辑器级事件，而不是 Domain 属性通知：

| 事件 | 行为 |
| --- | --- |
| SelectionChanged | 解析新 SelectionReference 并替换整个属性快照 |
| DocumentChanged | 若影响当前 ObjectId 或 ParentId，重新解析 Domain 和 Layout |
| UndoCompleted / RedoCompleted | 重新解析当前选择；对象不存在则清除选择 |
| SceneChanged | 只刷新 Rendering 分组，不改变 Domain/Layout 属性 |
| DocumentClosed | 清除 Selection 和全部属性快照 |

为避免旧引用，Resolver 每次从当前 EditorSession 索引重新查找对象。Property ViewModel 不订阅具体 Device 或 Layout 实例。

## 9. 典型对象投影

### 9.1 选中环网柜内部开关

```text
SelectionReference
  Kind = Device
  ObjectId = SwitchDeviceId
  ParentId = IntervalId
        ↓
Resolver 在指定 Interval 中验证 SwitchDevice
        ↓
Domain 分组：名称、调度编号、SwitchKind、SwitchState、TerminalIds
Layout 分组：相对位置、尺寸、标签偏移
Rendering 分组：SymbolKind、SymbolVisualState、命中边界
Derived 分组：所属 SwitchAssembly 的只读评估结果
```

SwitchKind、TerminalIds 和评估结果只读；名称、调度编号、SwitchState 未来通过专用 Command 编辑。

### 9.2 选中杆塔附属关系

PoleAttachment 的 Domain 分组显示 AttachmentId、PoleId、AttachedDeviceId；Layout 分组显示相对杆塔偏移。所属关系只读，未来重新挂接必须使用专用结构命令；偏移可使用 MoveAttachmentLayoutCommand 修改。

### 9.3 选中架空线路

ConnectionId 同时解析 Connection、OverheadLine 明细和 OverheadLineLayout：

- Connection 保存两个端点和通用业务属性。
- OverheadLine 保存型号、长度、SupportPoleIds 和延续语义。
- Layout 保存当前显示线段和后续路线数据。
- Rendering 只显示命中边界、线型映射和显示状态。

Property Inspector 必须按来源分组，不能把三者合并成一个可任意写入的对象。

## 10. 多选预留

当前 SelectionManager 只支持单选，M3-B-2-A 不设计多选交互实现。未来扩展时：

- Resolver 分别解析每个 SelectionReference。
- 只显示对象集合共有且类型一致的属性。
- 不同值显示 Mixed，而不是任取第一个值。
- 批量修改生成一条原子批量命令，保存每个对象的前后值。
- 任一对象校验失败时整条批量命令不提交。

单选 PropertyProjector 不应把第一个对象写死为全局状态，为未来集合投影保留组合入口即可。

## 11. 校验与测试建议

后续实现至少覆盖：

- SelectionReference 能解析到正确的 Domain、Layout 和 Rendering 描述。
- ParentId 不匹配、对象已删除或修订过期时不会显示旧属性。
- Property ViewModel 不持有 Domain、Layout、SceneElement 或 DrawingVisual 引用。
- ID、拓扑、派生评估和 Rendering 属性始终只读。
- 可编辑属性只能通过显式 PropertyCommandFactory 生成命令。
- 命令成功、失败、Undo 和 Redo 后属性快照与当前事实一致。
- 修改 Layout 属性不改变 Domain；修改 Domain 属性不写入 Rendering。
- Inspector 状态不进入工程文件、JPG 或打印。

## 12. 本阶段不实现

- Property Inspector、Resolver、Projector、ViewModel 或 UI 控件代码。
- 任何 Domain、Layout、Rendering、Interaction、Application 或 Desktop 修改。
- 属性编辑 Command、Undo/Redo 运行时代码。
- 多选属性编辑。
- 工程保存、JPG、打印、WorkScope、GroundingPoint、PTInterval 或 DTU。
