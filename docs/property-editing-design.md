# M3-B-5-A 属性编辑架构设计

> 文档状态：设计稿，仅定义属性编辑机制，不实现代码或具体 UI<br>
> 编制日期：2026-08-11<br>
> 依据：当前 PropertyInspector、SelectionReference、ICommand、CommandStack，以及 Domain / Layout / Rendering 分层

## 1. 目标与范围

本设计在现有只读 PropertyInspector 基础上增加受控的属性编辑链路，使用户修改 Domain 属性或 Layout 属性时，均通过明确的 Command 执行、校验、记录和刷新。

统一流程如下：

```text
用户输入
    ↓
PropertyEditor（编辑草稿与输入解析）
    ↓
PropertyEditRequest
    ↓
PropertyCommandFactory（白名单映射）
    ↓
ICommand
    ↓
CommandStack.ExecuteCommand
    ↓
Domain 行为或 Layout 替换
    ↓
DocumentChanged
    ↓
DrawingScene + PropertyInspector 刷新
```

本阶段仅修改设计文档，不修改现有模型，不实现属性编辑代码、具体 UI 控件、保存工程或新的设备类型。

## 2. 核心原则

属性编辑必须保持以下边界：

- UI 只负责收集输入、基础格式检查和展示反馈，不直接修改 Domain 或 Layout。
- PropertyInspector 继续保存只读属性快照，不保存业务对象引用。
- PropertyEditor 保存的是短生命周期编辑草稿，不是第二份工程状态。
- SelectionReference 是编辑目标身份，提交时必须重新解析当前对象。
- Domain 属性通过明确的领域行为或聚合替换入口修改。
- Layout 属性通过类型化 Layout Command 和 LayoutStore / DrawingLayout 的 Replace 入口修改。
- Rendering、Symbol 和 SceneElement 没有可编辑属性，不进入属性 Command。
- 所有成功修改必须进入 CommandStack，并支持 Undo 和 Redo。
- 派生状态、固定拓扑和专业图元规则不得伪装成普通属性开放编辑。

## 3. 编辑对象分类

### 3.1 Domain 属性

Domain 属性表示设备语义、电气事实、业务标识或人工录入的专业数据。它们的事实源始终是当前 Domain 对象。

第一阶段需要考虑的属性分类如下：

| 分类 | 对象与属性示例 | 修改方式 | 说明 |
| --- | --- | --- | --- |
| 设备名称 | RingCabinet、SwitchDevice、CableTermination 的 DisplayName | ChangePropertyCommand | 调用设备或聚合公开行为 |
| 杆号与杆塔名称 | Pole.PoleNumber、Pole.DisplayName | ChangePropertyCommand | 杆号规则由 Domain 校验 |
| 线路参数 | OverheadLine.LineModel、LengthMeters、ContinuationState、ContinuationDescription | ChangePropertyCommand | 线路参数与 Connection 端点分离 |
| 连接描述 | Connection.DisplayName、VoltageLevel、人工 ElectricalState | ChangePropertyCommand | 只开放已有明确业务语义的字段 |
| 开关机械状态 | SwitchDevice.SwitchState | 专门配置的 ChangePropertyCommand | 必须经过 Domain 联锁与状态规则边界 |
| 间隔名称 | RingCabinetInterval.DisplayName | ChangePropertyCommand | 通过所属 RingCabinet 聚合定位和修改 |

“电气属性”不等于允许任意编辑电气模型。以下内容不是普通属性编辑目标：

- DeviceType、SwitchKind、IntervalKind、PoleType、ConnectionType。
- DeviceId、ParentId、TerminalId、ElectricalNodeId、MainBusNodeId。
- Connection 的两个端点和 PoleAttachment 所属关系。
- SwitchAssembly 成员、InterlockRule 和柜内固定拓扑。
- RingCabinet 的间隔组成、顺序和接地结构类型。
- OperationalState、IsEffectivelyGrounded、ViolatedRuleCodes 等派生结果。
- SupportPoleIds 等会改变结构关系的集合。

上述结构若未来允许调整，应使用专门的结构 Command，并进行聚合级校验；不能通过通用属性键或文本输入直接修改。

### 3.2 Layout 属性

Layout 属性描述图面实例几何，只影响绘制结果，不改变电气拓扑。

| 分类 | Layout 属性示例 | 修改方式 | 保持不变 |
| --- | --- | --- | --- |
| 绝对坐标 | PoleLayout.Position、RingCabinetLayout.Position | ChangeLayoutCommand | Domain ID、Terminal、Connection |
| 相对坐标 | AttachmentLayout.Offset、IntervalLayout.RelativePosition | ChangeLayoutCommand | 所属关系、间隔类型 |
| 标签位置 | LabelOffset、NameLabelOffset、SequenceLabelOffset | ChangeLayoutCommand | 业务名称和编号 |
| 显示路径 | OverheadLineLayout.Start、End、ContinuationOffset | ChangeLayoutCommand | Connection 两端 TerminalId |
| 尺寸 | 明确允许调整的 Width、Height | ChangeLayoutCommand | Symbol 专业语义和固定拓扑 |
| 排列 | 同一父布局中的人工相对位置 | ChangeLayoutCommand 或 CompositeCommand | Domain 中的电气顺序和所有权 |

坐标统一使用文档毫米，不保存屏幕 DIP。属性输入 X/Y 与鼠标拖动应最终使用相同的 Layout 修改语义，避免出现两套校验和 Undo/Redo 行为。

尺寸和排列必须逐类开放：

- 坐标为有限数值，不能是 NaN 或 Infinity。
- 尺寸必须大于零，并满足具体图元组合的最小约束。
- 环网柜间隔的视觉排列不能改变 Domain Sequence 或 IntervalKind。
- 移动 Pole 不修改 AttachmentLayout.Offset；移动 Attachment 只修改自身相对偏移。
- 修改 OverheadLine 显示端点不改变 Connection 端点和 SupportPoleIds。
- 专业规范固定的 Symbol 几何默认只读，除非后续设计明确允许。

### 3.3 Rendering 与派生属性

以下属性只能查看，不能编辑：

- SymbolKind、SymbolVisualState、颜色、线型和线宽。
- HitTest Bounds、命中优先级和显示层级。
- Selection 高亮、拖动预览和临时 Overlay。
- OperationalState、有效接地和联锁违规结果。
- 根据 Domain + Layout 计算得到的端子显示锚点。

需要修改主题、显示比例或调试信息时，应进入应用显示设置，不生成单个业务对象的属性 Command。

## 4. 属性描述与白名单

### 4.1 PropertyDefinition

建议在 Editor 层为每种可查看属性提供显式 PropertyDefinition。它是属性语义描述，不是 UI 控件定义，至少包含：

- `PropertyKey`：稳定属性键，例如 `Pole.PoleNumber`。
- `TargetKind`：适用的 SelectionTargetKind 和解析对象类型。
- `Source`：Domain、Layout、Rendering 或 Derived。
- `ValueType`：Text、Number、Enum、Boolean、Point、Size 等。
- `IsEditable`：当前阶段是否允许提交编辑。
- `Unit`：mm、m、kV 等输入与显示单位。
- 基础约束：必填、长度、数值范围或枚举集合。
- `CommandKind`：ChangeProperty、ChangeLayout 或专门结构操作。

PropertyDefinition 不保存 Device、Layout、View 或控件引用。具体 UI 可以根据 ValueType 选择控件，但该选择不属于本设计范围。

### 4.2 显式白名单

属性可编辑性必须由“对象类型 + PropertyKey”共同决定。例如：

```text
(Pole, Pole.PoleNumber)                     → 可编辑 Domain 属性
(Pole, PoleLayout.Position)                 → 可编辑 Layout 属性
(SwitchDevice, SwitchDevice.SwitchState)    → 可编辑 Domain 状态
(SwitchDevice, SwitchDevice.TerminalIds)    → 只读
(OverheadLine, OverheadLine.LineModel)       → 可编辑 Domain 属性
(OverheadLine, Connection.TerminalAId)       → 只读结构引用
```

禁止通过反射自动公开所有公共属性，也禁止以中文 DisplayName 作为提交键。未知键、对象类型不匹配、只读属性或值类型不匹配必须在生成 Command 前拒绝。

### 4.3 与当前 PropertyInspector 的关系

当前 PropertyRowViewModel 已包含 PropertyKey、Source 和 IsReadOnly。后续实现可以扩展值类型、单位和校验信息，但保持以下原则：

- Projector 仍只复制当前事实值。
- Inspector ViewModel 不持有可变 Domain 或 Layout 对象。
- 编辑状态由独立 PropertyEditSession / PropertyEditor 管理。
- 命令成功、Undo、Redo 或对象变化后，重新 Project 整个属性快照。
- 不能通过把 IsReadOnly 改为 false 就直接启用双向对象绑定。

## 5. PropertyEditor 与编辑会话

### 5.1 PropertyEditSession

用户开始编辑某一属性时，PropertyEditor 创建短生命周期 PropertyEditSession，保存：

- SelectionReference。
- PropertyKey。
- 原始显示值和类型化原值快照。
- 当前输入草稿。
- BaseDocumentStateId 或基础修订号。
- 输入解析状态和校验提示。

编辑会话不保存 Domain、Layout、SceneElement、DrawingVisual 或 WPF 控件引用，也不进入工程文件和 Undo 历史。

### 5.2 提交边界

一次属性编辑只在用户明确提交时生成 Command：

- 文本和数字输入在确认、失焦提交或明确应用时生成一条 Command。
- 枚举、布尔值和开关状态在用户确认选择后生成一条 Command。
- 连续键入、输入法组合和中间格式状态只更新草稿。
- 新值与当前事实值相同，不创建 Command。
- 取消编辑只丢弃草稿，不修改对象、不改变 Dirty。

具体采用何种控件、键盘手势或视觉样式由后续 UI 设计决定。

### 5.3 选择与文档变化

编辑期间 Selection 或工程状态变化时：

- PropertyEditRequest 仍只引用稳定 SelectionReference 和 PropertyKey。
- 提交时重新解析目标，不能使用开始编辑时缓存的对象引用。
- 对象已删除或 ParentId 不匹配时拒绝提交，并刷新 Inspector。
- BaseDocumentStateId 已过期时默认拒绝提交，不自动覆盖较新的修改。
- 未提交草稿如何提示用户保留或放弃属于后续交互策略；无论选择何种 UI，都不能自动写入工程。

## 6. 修改流程

### 6.1 统一提交流程

```text
用户确认输入
    ↓
PropertyEditor.ParseDraft
    ↓
PropertyEditRequest
  - SelectionReference
  - PropertyKey
  - TypedNewValue
  - BaseDocumentStateId
    ↓
PropertyCommandFactory
  - 解析当前对象
  - 校验白名单与值类型
  - 读取当前 Before 值
  - 创建类型化 ICommand
    ↓
CommandStack.ExecuteCommand
    ↓
Command.Execute
  - Domain 行为 / Layout Replace
  - 专业校验 / 布局校验
    ↓ 成功
历史、CurrentIndex、Dirty 更新
    ↓
DocumentChanged
    ↓
Scene、HitTestIndex、Selection、PropertyInspector 刷新
```

如果任何一步失败，不生成成功历史项，不手工改变 CurrentIndex，也不把未生效的新值显示为业务事实。

### 6.2 Domain 属性流程

Domain 属性命令执行时：

1. 通过 SelectionReference 和稳定 ID 从当前 EditorSession 重新解析目标。
2. 校验目标仍属于预期聚合，例如 SwitchDevice 仍属于指定 Interval。
3. 校验当前值与命令 Before 值或基础修订一致。
4. 调用明确的 Domain 行为，或使用现有聚合工厂生成通过校验的替换对象。
5. 执行实体、聚合及跨引用校验。
6. 成功后发布受影响对象 ID；失败则保持原状态。

当前 Domain 若没有某字段的公开修改行为，属性编辑实现阶段不得通过反射、私有 setter 或集合绕过封装。应先单独确认并设计相应领域行为；本设计不修改现有 Domain 模型。

### 6.3 Layout 属性流程

Layout 属性命令执行时：

1. 按稳定 LayoutKey 读取当前 Layout。
2. 校验当前值与 Before 或基础修订一致。
3. 由类型化 LayoutEditor 构造仅目标字段变化的新 Layout 值。
4. 保留 ID、所有权及其他未修改字段。
5. 校验坐标、尺寸和父子布局约束。
6. 通过 DrawingLayout / LayoutStore Replace 原子替换。
7. 发布 LayoutChanged 并重建场景与命中索引。

数值属性编辑和鼠标拖动应共用布局值构造与 Replace 规则。PropertyEditor 不自行复制一套坐标算法。

## 7. Command 类型

### 7.1 ChangePropertyCommand

ChangePropertyCommand 用于一个明确 Domain 属性的修改，至少记录：

- 目标 SelectionReference 或稳定 Domain ID。
- PropertyKey。
- 类型化 Before 值。
- 类型化 After 值。
- 基础状态标识或修订号。

其 Execute、Undo、Redo 分别应用 After、Before、After。每次应用都必须通过同一领域行为和必要校验，不能在 Undo 时直接写私有字段。

“通用”只表示统一 Command 生命周期，不表示通过 `object` + 反射修改任意属性。建议由每个受支持 PropertyKey 映射到类型化处理器，例如杆号处理器、线路参数处理器和开关状态处理器。

### 7.2 ChangeLayoutCommand

ChangeLayoutCommand 用于一个明确 Layout 属性或一组不可分割的布局值，至少记录：

- LayoutTargetKind 和稳定 LayoutKey。
- PropertyKey。
- 类型化 Before Layout 值或字段值。
- 类型化 After Layout 值或字段值。
- 基础 Layout 状态标识。

对于位置属性，现有 MoveCommand 可以继续作为专用 ChangeLayoutCommand。属性面板修改 PoleLayout.Position 与鼠标拖动 Pole 应得到等价的历史和恢复结果。

当 X、Y 作为一个 Point 共同表达位置时，应作为一条命令整体提交，避免 Undo 只恢复一个坐标分量。Width、Height 若存在组合约束，也应作为一个 Size 值处理。

### 7.3 CompositeCommand

CompositeCommand 用于一次用户操作需要同步修改多个字段或多个对象的场景，例如：

- 同一属性页明确点击一次应用，提交多个相互依赖字段。
- 修改布局尺寸时同步调整由设计明确允许的标签偏移。
- 后续结构操作同时更新 Domain 与 Layout。

CompositeCommand 必须满足：

1. 子命令按确定顺序执行。
2. 任一子命令失败时反向恢复已执行部分。
3. 整体只占 CommandStack 一个历史项。
4. Undo 按反向顺序执行，Redo 按原顺序执行。
5. 任何中间状态不对 Scene、PropertyInspector 或保存流程可见。

第一阶段不实现完整 CompositeCommand 运行时，但属性编辑架构不得假定所有修改永远只有一个字段。

## 8. 校验边界

### 8.1 UI / PropertyEditor 输入校验

UI 和 PropertyEditor 只负责与输入表达有关的检查：

- 必填值是否为空。
- 文本能否解析为目标 ValueType。
- 数字格式和单位转换是否成功。
- 输入是否为有限数值。
- 枚举值是否来自允许集合。
- 明确的长度或基础范围提示。

这些检查用于尽早反馈，不代表专业修改已经有效。UI 不判断设备联锁、电气拓扑、聚合完整性或跨对象引用。

### 8.2 PropertyCommandFactory 校验

Command 工厂负责编辑权限和请求一致性：

- SelectionTargetKind、解析对象类型和 PropertyKey 是否匹配。
- 属性是否位于显式可编辑白名单。
- TypedNewValue 是否符合 PropertyDefinition。
- 目标与 ParentId 所有权是否一致。
- BaseDocumentStateId 是否仍有效。
- 是否需要专用结构 Command 而不是普通属性 Command。

Command 工厂不自行修改对象。只有成功创建的 ICommand 才能交给 CommandStack。

### 8.3 Domain 校验

Domain 是专业规则和业务不变量的最终边界，包括：

- 名称、编号和业务参数的领域约束。
- SwitchState 修改涉及的已确认联锁规则。
- RingCabinet 聚合、Interval、SwitchAssembly 的完整性。
- Terminal、ElectricalNode、Connection 和所有权引用一致性。
- 线路参数、延续状态等对象内部约束。

PropertyEditor 不复制这些规则，也不能因为 UI 已限制输入就跳过 Domain 校验。对于尚未确认的专业规则，不在编辑层自行推断或自动修正其他字段。

### 8.4 Layout 校验

LayoutEditor / LayoutStore 负责：

- ID 与 LayoutKey 一致。
- 坐标和尺寸为合法有限值。
- 尺寸大于零并符合已确认的组合约束。
- 相对布局仍属于正确父对象。
- 修改未改变 Domain 所有权或电气连接。

页面边界、自动吸附、碰撞检测和自动排列尚未确认，本阶段不加入相应规则。

### 8.5 非法修改反馈

失败结果应使用结构化 PropertyEditResult，而不是依赖异常文本作为 UI 协议。结果至少区分：

| 类型 | 示例 | 处理 |
| --- | --- | --- |
| InputInvalid | 数字格式错误、必填为空 | 保留草稿并显示字段提示 |
| PropertyReadOnly | 尝试修改 TerminalId | 刷新属性定义并拒绝提交 |
| TargetNotFound | 对象已删除 | 清除或刷新 Selection |
| OwnershipMismatch | ParentId 已变化 | 拒绝提交并重新解析 |
| RevisionConflict | 编辑期间对象已变化 | 刷新当前值，要求用户重新输入 |
| DomainRuleViolation | 杆号、联锁或聚合规则失败 | 显示规则代码和可读说明 |
| LayoutInvalid | 坐标、尺寸或父子布局非法 | 保留草稿并显示布局提示 |
| UnexpectedFailure | 非预期内部错误 | 不改变历史，记录诊断并保持事实值 |

命令失败时：

- 不进入 History。
- CurrentIndex、CurrentStateId 和 Dirty 保持不变。
- 不清空已有 Redo 分支。
- Scene 和业务对象保持命令前状态。
- PropertyInspector 继续显示当前事实值；草稿是否保留由错误类型决定。

## 9. Undo/Redo 与 Dirty 状态

### 9.1 所有修改必须可撤销

每次成功属性提交必须形成一条 ICommand：

- Execute 应用 After。
- Undo 恢复 Before。
- Redo 再次应用 After。
- 新命令成功后由 CommandStack 按统一规则截断 Redo 分支。
- 命令失败不移动 CurrentIndex。

禁止存在“先直接改对象，再补一条仅用于记录的 Command”的路径。预览或草稿必须与正式对象隔离，正式修改由 CommandStack 执行。

### 9.2 属性编辑事务边界

- 连续文本输入在一次提交时形成一条命令，不按字符记录。
- Point、Size 等组合值整体形成一条命令。
- 多字段一次应用形成一条 CompositeCommand。
- 两次独立确认默认是两条历史，不按时间自动合并。
- 取消、无变化和校验失败不产生历史。

### 9.3 保存点与 Dirty

属性 Command 成功后，CommandStack 的 CurrentStateId 变化；若不同于 SavedStateId，则 IsDirty=true。

- Undo 回到保存点时 IsDirty=false。
- Redo 或新编辑离开保存点时 IsDirty=true。
- 属性输入草稿尚未提交时不改变 Dirty。
- 保存成功后由保存用例调用 MarkSaved；本阶段不实现保存工程。
- Selection、属性面板焦点和校验提示不改变 Dirty。

## 10. 刷新机制

### 10.1 Command 成功

Domain 或 Layout 修改成功后发布 DocumentChanged，至少携带受影响稳定 ID 和变更来源：

```text
CommandStack 成功改变状态
    ↓
DocumentChanged
    ├─ DomainChanged / LayoutChanged
    └─ AffectedObjectIds
          ↓
SelectionObjectResolver 重新解析当前 SelectionReference
          ↓
PropertyProjector 生成新的事实快照
          ↓
DrawingSceneBuilder 读取最新 Domain + Layout
          ↓
HitTestIndex、SelectionOverlay、Rendering 刷新
```

属性 Command 不直接调用 DrawingSceneRenderer，也不手工修改 PropertyInspectorViewModel 的 DisplayValue。

### 10.2 Undo 和 Redo

Undo、Redo 使用与 Execute 相同的刷新链路：

- 当前对象仍存在时保留 SelectionReference。
- 对象被撤销删除时清除失效 Selection。
- PropertyInspector 从事实源重新投影 Before 或 After。
- Layout 变化后重建 HitTestIndex 和高亮边界。
- Domain 状态变化后重新生成相应 SymbolRenderContext。

## 11. 典型编辑示例

### 11.1 修改杆号

```text
SelectionReference(Device, PoleId)
    ↓
PropertyKey = Pole.PoleNumber
TypedNewValue = "P-102"
    ↓
PropertyCommandFactory 创建 ChangePropertyCommand
    ↓
CommandStack.ExecuteCommand
    ↓
Pole 聚合行为校验并应用杆号
    ↓
Scene 标签和 PropertyInspector 刷新
```

Undo 恢复原杆号，Redo 再次应用 `P-102`。如果当前 Domain 尚无杆号修改行为，应拒绝进入实现，不通过私有字段绕过。

### 11.2 修改杆塔坐标

```text
SelectionReference(Device, PoleId)
    ↓
PropertyKey = PoleLayout.Position
TypedNewValue = DocumentPoint(120, 85)
    ↓
ChangeLayoutCommand / MoveCommand
    ↓
DrawingLayout.Replace(new PoleLayout)
    ↓
Scene、HitTestIndex、高亮和布局属性刷新
```

该操作不修改 Pole、PoleAttachment、Terminal、Connection 或 OverheadLine.SupportPoleIds。

### 11.3 修改开关状态

```text
SelectionReference(Device, SwitchDeviceId, IntervalId)
    ↓
PropertyKey = SwitchDevice.SwitchState
TypedNewValue = Closed
    ↓
ChangePropertyCommand
    ↓
Domain 校验已确认联锁规则
    ├─ 合法：应用并进入历史
    └─ 非法：返回规则代码，不修改其他开关
```

Rendering 不计算联锁、不自动调整其他 SwitchDevice，也不把 OperationalState 保存到 Symbol。

### 11.4 修改线路参数

一次提交 LineModel 和 LengthMeters 时，可使用一个类型化线路属性 Command 或 CompositeCommand。两项均通过校验后一起生效，任一失败则全部保持原值。

修改线路参数不改变 Connection 的 TerminalId、SupportPoleIds 或 Layout 路径；这些分别属于结构关系和图面布局。

## 12. 分阶段实现建议

后续实现可按以下顺序推进：

1. 定义 PropertyEditRequest、PropertyDefinition 和结构化结果。
2. 先开放 PoleLayout.Position，复用现有 MoveCommand 与 DrawingLayout.Replace。
3. 实现一个已有公开领域行为支持的简单 Domain 文本属性。
4. 接入 PropertyCommandFactory 白名单和 BaseDocumentStateId 校验。
5. 统一 Execute、Undo、Redo 后的 Scene 与 Inspector 刷新。
6. 再逐项开放杆号、线路参数和 SwitchState，不批量暴露全部属性。
7. 最后按需要实现多字段 CompositeCommand。

每开放一个属性前，都必须确认 Domain 行为、校验规则、Before/After 恢复和保存格式兼容性。

## 13. 校验与测试建议

后续实现至少覆盖：

- 未知 PropertyKey、只读属性和对象类型不匹配请求被拒绝。
- UI 输入解析失败不创建 Command。
- Domain 属性命令通过公开行为修改，失败不进入 History。
- Layout 属性只改变目标布局字段，不修改 Domain 或电气拓扑。
- PoleLayout.Position 的属性编辑与鼠标拖动具有相同 Undo/Redo 结果。
- Point 和 Size 作为单条命令整体恢复。
- 属性编辑期间目标被删除或修订过期时拒绝提交。
- DomainRuleViolation 返回稳定规则代码，不自动修改其他设备。
- Execute、Undo、Redo 后 PropertyInspector、Scene 和 HitTestIndex 一致刷新。
- 所有成功属性修改影响 Dirty；取消、无变化和失败不影响 Dirty。
- CompositeCommand 部分失败时完整回滚且不进入 History。
- Rendering 和 Derived 属性始终只读。

## 14. 本阶段不实现

- PropertyEditor、PropertyEditSession、PropertyDefinition 或 PropertyCommandFactory 代码。
- ChangePropertyCommand、ChangeLayoutCommand 或 CompositeCommand 代码。
- 对 Domain、Layout、PropertyInspector、CommandStack、Rendering 或 Desktop 的修改。
- 文本框、下拉框、数值输入、错误提示等具体 UI 控件设计。
- 工程保存、自动保存、属性模板或批量属性编辑。
- 结构关系编辑、自动布局、吸附、碰撞检测或电气仿真。
- WorkScope、GroundingPoint、PTInterval 和 DTUCabinet 属性编辑。
