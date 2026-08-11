# M5-C-4-A Professional 对象创建、删除与编辑架构设计

> 文档状态：实现前设计，仅定义 Editor Command、刷新和状态边界，不实现代码<br>
> 编制日期：2026-08-12<br>
> 依据：`docs/distribution-professional-object-model-design.md`、`docs/professional-object-implementation-design.md`、`docs/professional-rendering-interaction-design.md`，以及当前 DrawingDocument、CommandStack、PropertyInspector、SelectionManager 和 ProjectSession 实现

## 1. 目标与范围

本设计将当前只读显示的 `GroundingPoint` 和 `WorkScope` 接入现有 Editor Command 闭环，使用户明确发起的专业对象修改能够：

```text
用户意图
  ↓
稳定 ID + 输入值
  ↓
CommandFactory
  ↓
ICommand / CommandStack
  ↓
DrawingDocument Professional API
  ↓
Scene / HitTest / Overlay 重建
  ↓
Selection / PropertyInspector 刷新
  ↓
Dirty / SavePoint
```

第一版优先完成 GroundingPoint 创建、删除和文本业务属性编辑。WorkScope 的双边界选择、关联地线多选和完整编辑流程拆入 M5-C-4-C，避免在 M5-C-4-B 同时扩大命令、工具状态和 UI 范围。

## 2. 已有架构事实

### 2.1 DrawingDocument 是唯一修改入口

当前 Domain 已提供：

```text
CreateGroundingPoint
AddGroundingPoint
UpdateGroundingPoint
RemoveGroundingPoint

CreateWorkScope
AddWorkScope
UpdateWorkScope
RemoveWorkScope
```

所有 Editor Command 必须调用这些入口，不能直接修改集合、私有 setter 或内部对象。DrawingDocument 继续负责：

- 工程级 ID 唯一性；
- Terminal 是否存在；
- 同一 Terminal 不能重复拥有 GroundingPoint；
- WorkScope 两个 BoundaryPoint 的设备/端子归属；
- WorkScope 引用的 GroundingPoint 是否存在；
- 被 WorkScope 引用的 GroundingPoint 不得删除。

UI 和 CommandFactory 可以做输入级预检查，但 Domain 是最终校验者。

### 2.2 当前 CommandStack 语义

当前 `CommandStack.ExecuteCommand()` 先调用 `Execute()`，成功后才写入历史。因此 Domain 拒绝不会产生历史项，也不会改变 CurrentIndex 或 Dirty。

`Undo()` 和 `Redo()` 也是先调用命令，再改变 CurrentIndex；若命令抛出异常，索引保持不变。Professional Command 应沿用该语义，不修改 ICommand 的 `Execute / Undo / Redo` 合同。

### 2.3 Dirty 的事实源

当前运行时 Dirty 为：

```text
ProjectRuntimeSession.IsDirty
  = PersistenceSession.IsDirty
  || CommandStack.IsDirty
```

Professional 编辑全部进入 CommandStack，因此其 Dirty 应由 `CommandStack.CurrentStateId != SavedStateId` 表达。不得在每次 Professional Command 成功后调用 `ProjectService.MarkDirty()`，否则即使 Undo 回保存点，持久化 Session 的布尔 Dirty 仍会使工程无法恢复 clean。

成功保存后，由工程生命周期协调者使用新的持久化 Session 替换旧 Session，并调用 `CommandStack.MarkSaved()` 建立保存点。Domain 不保存 Dirty。

## 3. Terminal 选择来源

### 3.1 稳定来源

GroundingPoint 和 BoundaryPoint 的 Terminal 必须来自当前 DrawingDocument 中真实存在的 Terminal，并通过 M5-C-3-B 的 `TerminalAnchorIndex` 映射为毫米文档坐标：

```text
DrawingDocument.Terminals
        +
TerminalAnchorIndex
        ↓
可选 Terminal 热点
        ↓
SelectionReference(TerminalId)
```

只有同时满足以下条件的 Terminal 才能在画布上被用户选择：

- TerminalId 存在于当前 DrawingDocument；
- 当前 Runtime Layout 能建立明确 TerminalAnchor；
- Terminal 热点属于当前工程和当前 SceneRevision。

无法生成锚点的 Terminal 不显示可选热点，也不使用最近设备、文字、线路折点或屏幕坐标猜测。

### 3.2 Terminal Pick 模式

建议为创建工具增加临时 `Terminal` 选择类型或等价的 `TerminalPickReference`，只保存 TerminalId。Terminal 热点只在以下显式工具模式显示：

- AddGroundingPoint；
- PickWorkScopeStartBoundary；
- PickWorkScopeEndBoundary；
- 未来 RebindGroundingPointTerminal。

普通选择模式不加入 Terminal 热点，避免端子小区域长期遮挡 Device、Interval 或 Connection 的命中。

Terminal Pick 模式是 Editor 临时状态：

- 不保存到工程文件；
- 不进入 Undo/Redo；
- 不保存 Terminal Domain 对象引用；
- 工程切换、取消工具或 SceneRevision 变化时清空；
- 选择结果必须在执行命令前按 TerminalId 重新解析。

### 3.3 不进行自动筛选推断

当前 DrawingDocument 对 GroundingPoint 的已确认规则是“Terminal 必须存在，且同一 Terminal 不允许重复 GroundingPoint”。本阶段不自行增加 Terminal.Role 白名单，也不根据以下状态过滤或自动选择 Terminal：

- SwitchState；
- GroundSwitch 状态；
- OperationalState；
- IsEffectivelyGrounded；
- Topology 路径或停电分析结果。

## 4. Command 数据边界

### 4.1 运行时快照

Professional Command 使用只包含标量和值对象的运行时快照，不复用 Persistence DTO：

```text
GroundingPointCommandSnapshot
├── GroundingPointId
├── TerminalId
├── Location
├── Number?
└── Note?

WorkScopeCommandSnapshot
├── WorkScopeId
├── StartBoundary : BoundaryPointCommandValue
├── EndBoundary   : BoundaryPointCommandValue
├── Description
└── GroundingPointIds[]
```

`BoundaryPointCommandValue` 只复制 `DeviceId + TerminalId + Side`。快照不包含 Domain 对象引用、WPF 对象、SceneElement、Selection 或属性 ViewModel。

### 4.2 Command 可以持有的引用

为兼容当前 `ChangePropertyCommand` 和同步 CommandStack，Professional Command 可以在运行时持有当前 `DrawingDocument` 聚合根，以及不可变 Before/After 快照。它不应长期持有具体 GroundingPoint、WorkScope、Terminal 或 BoundaryPoint 实例。

UI 只保存：

- SelectionReference；
- TerminalId、DeviceId 等稳定 ID；
- 用户输入缓冲值；
- Command 执行结果和错误文本。

每次 Execute、Undo、Redo 都通过 DrawingDocument 按 ID 重新执行聚合行为。

## 5. GroundingPoint 创建

### 5.1 创建请求

最小请求模型：

```text
AddGroundingPointRequest
├── TerminalId
├── Location
├── Number?
└── Note?
```

GroundingPointId 在创建 Command 前生成一次，进入 After 快照，并在 Execute、Undo、Redo 全生命周期保持稳定。Redo 不生成新 ID。

### 5.2 创建流程

```text
用户激活“添加工作地线”
  ↓
Terminal Pick 模式显示当前可解析端子热点
  ↓
用户明确点击 Terminal
  ↓
输入 Location / Number / Note
  ↓
GroundingPointCommandFactory
  ↓
AddGroundingPointCommand(AfterSnapshot)
  ↓ Execute
DrawingDocument.CreateGroundingPoint(...)
  ↓
CommandStack 写入历史并进入 Dirty
  ↓
重建 DrawingScene / TerminalAnchorIndex / HitTestIndex
  ↓
选择新 GroundingPoint
  ↓
重建 Overlay 和 PropertyInspectorSnapshot
```

CommandFactory 只负责：

- 检查请求字段是否完整；
- 拒绝空 Location；
- 规范化可选文本输入；
- 确认 TerminalId 可在当前工程解析；
- 生成稳定 GroundingPointId 和 Command。

同 Terminal 重复、ID 冲突及其他不变量仍由 DrawingDocument 最终判断。

### 5.3 AddGroundingPointCommand

| 阶段 | 行为 |
| --- | --- |
| Before | 对象不存在 |
| After | 完整 GroundingPointCommandSnapshot |
| Execute | `DrawingDocument.CreateGroundingPoint(After...)` |
| Undo | `DrawingDocument.RemoveGroundingPoint(After.Id)` |
| Redo | 使用同一 After 和 ID 再次 Create |

Execute 失败时不进入历史，不刷新场景，不改变 Selection，不产生 Dirty。

正常的 LIFO 撤销顺序保证：如果后续 WorkScope 引用了该 GroundingPoint，必须先撤销 WorkScope 命令，之后才能撤销创建命令。若存在绕过 CommandStack 的外部修改导致 Undo 删除被引用，Domain 拒绝，CommandStack 索引保持不变并报告历史一致性错误，不级联删除 WorkScope。

## 6. GroundingPoint 删除

### 6.1 RemoveGroundingPointCommand

删除命令在创建前按 ID 解析当前对象，并复制完整 Before 快照：

| 阶段 | 行为 |
| --- | --- |
| Before | 被删除对象的完整快照 |
| After | 对象不存在 |
| Execute | `DrawingDocument.RemoveGroundingPoint(Before.Id)` |
| Undo | `DrawingDocument.CreateGroundingPoint(Before...)` |
| Redo | 再次 Remove |

### 6.2 引用冲突

若任一 WorkScope 引用目标 GroundingPoint：

1. DrawingDocument 拒绝删除；
2. Execute 抛出 Domain 规则异常；
3. CommandStack 不新增历史项，CurrentIndex 不变；
4. Dirty 不变；
5. Scene、HitTest 和 PropertyInspector 保持当前有效状态；
6. UI 显示“仍被 WorkScope 引用”的明确错误；
7. 不提供“强制删除并级联修改 WorkScope”。

用户必须在后续 WorkScope 编辑流程中显式解除引用，再重新执行删除。

### 6.3 删除后的选择

- 删除成功：若当前选中目标就是该 GroundingPoint，立即清除 Selection；
- 删除成功后先重建 Scene 和 HitTest，再清空或验证 Selection，避免 Overlay 引用失效条目；
- Undo 删除：对象恢复、Scene 恢复，但第一版 Selection 保持为空，用户可重新选择；
- Redo 删除：若该对象再次被选中则清除；
- Selection 不作为文档事实，不随 Domain Command 一起进入 Undo 快照。

第一版采用“对象恢复、选择不自动恢复”的保守策略，不为选择历史增加额外命令协议。

## 7. GroundingPoint 属性编辑

### 7.1 第一版允许字段

以当前 Domain 为准，M5-C-4-B 允许人工编辑：

| PropertyKey | Domain 字段 | 规则 |
| --- | --- | --- |
| `GroundingPoint.Number` | Number | 可空，去除首尾空白 |
| `GroundingPoint.Location` | Location | 必填，非空 |
| `GroundingPoint.Note` | Note | 可空，去除首尾空白 |

当前字段名为 `Note`，不是 `Notes`。本阶段不新增任何业务字段，也不自行增加 Number 唯一或必填规则。

### 7.2 TerminalId 单独评估

当前 Domain 的 `UpdateGroundingPoint()` 技术上支持修改 TerminalId，并会验证目标 Terminal 存在及同 Terminal 重复 GroundingPoint。但 TerminalId 是工作地线唯一拓扑引用，直接开放文本 Guid 编辑存在误绑和跨对象引用风险。

因此 M5-C-4-B 明确：

- PropertyInspector 中 TerminalId 保持只读；
- `ChangeGroundingPointCommand` 修改文本字段时携带原 TerminalId，不改变绑定；
- 不接受用户直接输入 Guid 改绑；
- 后续若业务确认允许改绑，应使用显式 Terminal Pick 模式和独立 `RebindGroundingPointTerminalCommand`，并作为单独里程碑验收。

### 7.3 ChangeGroundingPointCommand

Before 和 After 都是完整快照。第一版 After.TerminalId 必须等于 Before.TerminalId。

| 阶段 | 行为 |
| --- | --- |
| Execute | `DrawingDocument.UpdateGroundingPoint(Id, After.TerminalId, After.Location, After.Number, After.Note)` |
| Undo | 使用 Before 调用 Update |
| Redo | 使用 After 调用 Update |

即使只修改一个属性，Command 仍保存完整 Before/After，保证 Undo 不依赖当前 PropertyInspector 缓冲值。

### 7.4 PropertyEditor 流程

```text
PropertyInspector 可编辑值快照
  ↓ 用户提交
PropertyEditor.TryEdit(SelectionReference, PropertyKey, Input)
  ↓
SelectionObjectResolver 按 ID 重新解析
  ↓
Professional PropertyCommandFactory
  ↓
ChangeGroundingPointCommand
  ↓
CommandStack.ExecuteCommand
  ↓
Domain 更新成功后重建 Scene 与属性快照
```

UI 不双向绑定 GroundingPoint；命令失败时保留 Domain 原值，并用重新投影的快照覆盖无效输入。

## 8. WorkScope 创建

### 8.1 显式边界草稿

WorkScope 创建必须由用户分别确认两个边界：

```text
BoundaryPointDraft A
├── DeviceId
├── TerminalId
└── Side

BoundaryPointDraft B
├── DeviceId
├── TerminalId
└── Side
```

选择流程：

1. 用户进入 AddWorkScope 工具；
2. 显式选择第一个 Terminal，并确认 DeviceId / Side；
3. 显式选择第二个 Terminal，并确认 DeviceId / Side；
4. 输入 Description；
5. 可选地从现有 GroundingPoint 列表中勾选稳定 ID；
6. CommandFactory 创建 AddWorkScopeCommand；
7. DrawingDocument 完成最终归属和引用校验。

对 RingCabinet Interval 外部端子，DeviceId 使用所属 RingCabinet.Id。Editor 可以通过已有聚合关系提供候选 DeviceId，但必须让用户确认边界与 Side；不能把 InternalAggregate OwnerId 当作顶层 DeviceId，也不能根据拓扑路径自动选择第二边界。

创建草稿是临时 Editor 状态，不进入 Domain、Persistence 或 Undo。取消工具即丢弃。

### 8.2 AddWorkScopeCommand

| 阶段 | 行为 |
| --- | --- |
| Before | 对象不存在 |
| After | 完整 WorkScopeCommandSnapshot |
| Execute | `DrawingDocument.CreateWorkScope(After...)` |
| Undo | `DrawingDocument.RemoveWorkScope(After.Id)` |
| Redo | 使用同一 ID 和 After 再次 Create |

禁止从两端 Terminal 自动计算路径、范围内 Device、停电区域或 GroundingPoint 关联。

## 9. WorkScope 删除与编辑

### 9.1 RemoveWorkScopeCommand

删除前复制完整 WorkScope 快照：

| 阶段 | 行为 |
| --- | --- |
| Before | ID、两个 BoundaryPoint、Description、GroundingPointIds |
| Execute | `DrawingDocument.RemoveWorkScope(Id)` |
| Undo | `DrawingDocument.CreateWorkScope(Before...)` |
| Redo | 再次 Remove |

删除 WorkScope 只删除 WorkScope 自身，不删除、移动或修改任何 GroundingPoint、Device、Terminal 或 Connection。

### 9.2 ChangeWorkScopeCommand

允许的最小字段：

- Description；
- StartBoundary；
- EndBoundary；
- GroundingPointIds 集合。

Before/After 均使用完整 WorkScope 快照。Execute、Undo、Redo 分别使用对应快照调用 `DrawingDocument.UpdateWorkScope()`。边界修改必须重新进入显式 Terminal Pick 流程；GroundingPoint 引用集合只能从当前工程已存在对象中明确选择。

修改任一字段都不得触发自动范围计算、自动地线关联或开关状态变化。

### 9.3 M5-C-4-C 拆分理由

WorkScope 编辑同时需要：

- 两阶段 Terminal 选择工具；
- RingCabinet 内部端子到顶层 DeviceId 的归属解析；
- Side 输入与确认；
- GroundingPoint 多选；
- 两个边界的复合高亮和草稿取消；
- WorkScope 六类 Command 路径中的三类实现与测试。

这些能力显著大于单对象 GroundingPoint CRUD，因此不应强行进入 M5-C-4-B。

## 10. Command 类型总表

| Command | Before | After | Execute | Undo | Redo |
| --- | --- | --- | --- | --- | --- |
| AddGroundingPointCommand | 不存在 | GroundingPoint 快照 | Create | Remove | Create |
| RemoveGroundingPointCommand | GroundingPoint 快照 | 不存在 | Remove | Create | Remove |
| ChangeGroundingPointCommand | 原快照 | 新快照 | Update(After) | Update(Before) | Update(After) |
| AddWorkScopeCommand | 不存在 | WorkScope 快照 | Create | Remove | Create |
| RemoveWorkScopeCommand | WorkScope 快照 | 不存在 | Remove | Create | Remove |
| ChangeWorkScopeCommand | 原快照 | 新快照 | Update(After) | Update(Before) | Update(After) |

CommandFactory 在生成命令时解析当前对象并冻结 Before。Command 不信任 UI 缓存，不根据显示文字寻找对象。

## 11. 失败与原子性边界

### 11.1 Execute 失败

- CommandStack 不写入历史；
- CurrentIndex、CurrentStateId 和 Dirty 不变；
- Selection 保持原值；
- 不重建成功态 Scene；
- PropertyInspector 重新投影当前有效 Domain 快照；
- UI 显示 DomainRuleViolation 或明确的 TargetNotFound。

### 11.2 Undo / Redo 失败

当前 CommandStack 会在 ICommand 抛出时保留索引。Editor 层必须捕获异常并：

- 保留当前 Scene 和 Selection；
- 不伪造成功通知；
- 显示“撤销/重做失败，工程状态未改变”；
- 不跳过失败命令继续操作后面的历史项。

Professional Command 应调用原子 Domain API。禁止在一个 Command 中先直接改集合、再补校验。

### 11.3 目标失效

命令创建前和执行时都按稳定 ID 校验目标属于当前 DrawingDocument。工程切换后旧 CommandStack 必须随 EditorSession 一起丢弃，防止跨工程 Execute、Undo 或 Redo。

## 12. Undo / Redo 边界

### 12.1 对象语义

- 创建 → Undo 删除同一 ID；
- 删除 → Undo 用原快照恢复同一 ID；
- 属性修改 → Undo 恢复完整 Before；
- WorkScope 边界修改 → Undo 恢复原 BoundaryPoint 值；
- WorkScope 地线引用修改 → Undo 恢复原 ID 集合；
- Redo 始终重放原 After，不读取当前 UI 输入。

### 12.2 非文档状态

以下状态不进入 Professional Command：

- 当前 Selection；
- Terminal Pick 工具步骤；
- 属性输入框未提交内容；
- Scene、HitTestIndex、Overlay；
- 错误提示和对话框状态。

Undo/Redo 成功后统一重建派生状态，而不是恢复旧 DrawingScene 或 DrawingVisual。

## 13. Dirty 与保存点

### 13.1 修改成功

所有成功的 Add、Remove、Change Command 进入同一个现有 CommandStack。因此：

- Execute 成功后 `CommandStack.IsDirty = true`，除非新状态恰好对应保存点；
- Undo 回 `SavedStateId` 后恢复 clean；
- Redo 离开保存点后再次 dirty；
- Command 失败不改变 Dirty；
- Domain 对象不保存 Dirty 字段。

### 13.2 保存成功

```text
当前 DrawingDocument
  ↓ ProjectService.SaveProject
FormatVersion 2 Professional DTO
  ↓ 保存成功并重新打开校验
替换 PersistenceSession
  ↓
CommandStack.MarkSaved()
```

只有保存完全成功后才能 MarkSaved。保存失败时保持原 SavedStateId 和 Dirty。Professional 编辑继续使用现有 FormatVersion 2，不增加字段、不修改工程格式。

## 14. Scene、Selection 与 PropertyInspector 刷新

### 14.1 成功操作后的统一顺序

```text
Command 成功
  ↓
重建 DrawingScene
  ↓
重建 TerminalAnchorIndex / Professional Scene Elements
  ↓
重建 HitTestIndex
  ↓
校验或更新 SelectionReference
  ↓
重建 Selection Overlay
  ↓
SelectionObjectResolver 重新解析
  ↓
PropertyProjector 生成新值快照
```

不能只刷新属性面板而保留旧专业图元，也不能只重绘图元而继续使用旧 HitTestIndex。

### 14.2 各操作选择策略

| 操作 | 成功后的 Selection |
| --- | --- |
| Add GroundingPoint | 选择新 GroundingPoint |
| Change GroundingPoint | 保留当前 GroundingPoint；若已失效则清除 |
| Remove GroundingPoint | 清除被删除目标 |
| Undo Add | 对象消失，清除失效选择 |
| Redo Add | 对象恢复，第一版不自动恢复选择 |
| Undo Remove | 对象恢复，第一版 Selection 保持为空 |
| Redo Remove | 清除失效选择 |
| Add WorkScope | 后续 M5-C-4-C 选择新 WorkScope |
| Change WorkScope | 后续保留当前 WorkScope |
| Remove WorkScope | 后续清除目标，不影响 GroundingPoint 选择 |

SelectionManager 只保存 SelectionReference，不保存 Professional 对象。PropertyInspector 永远从当前 DrawingDocument 重新解析并生成值快照。

## 15. Persistence 边界

Professional 编辑不改变文件合同：

- 继续保存到 FormatVersion 2 的 `professional` 区域；
- WorkScope、BoundaryPoint、GroundingPoint DTO 不变；
- 不保存 CommandStack、Selection、Terminal Pick 草稿或 Dirty；
- 不保存 Scene、Overlay、DrawingVisual 或属性输入缓冲；
- ProjectService 保存时从当前 DrawingDocument 重新生成 Professional DTO。

本阶段不得因 Editor 实现修改 ProjectFileFormat、ProjectProfessionalDto 或迁移逻辑。

## 16. M5-C-4-B 最小实现范围

### 16.1 必须实现

M5-C-4-B 只实现 GroundingPoint：

1. 显式 Terminal Pick 入口，选择稳定 TerminalId；
2. AddGroundingPointCommand；
3. RemoveGroundingPointCommand；
4. ChangeGroundingPointCommand；
5. Number、Location、Note 三个 PropertyKey；
6. CommandFactory / PropertyEditor 接入；
7. Execute、Undo、Redo 后完整 Scene / HitTest / Selection / PropertyInspector 刷新；
8. CommandStack Dirty 与保存点联动；
9. 删除被 WorkScope 引用时展示 Domain 拒绝，不级联处理。

### 16.2 建议代码范围

在保持当前项目结构的前提下，新增或修改范围建议限制为：

```text
src/DistributionDrawing.Rendering.Wpf/Interaction/Professional/
├── GroundingPointCommandSnapshot.cs
├── AddGroundingPointCommand.cs
├── RemoveGroundingPointCommand.cs
└── ChangeGroundingPointCommand.cs

src/DistributionDrawing.Rendering.Wpf/Interaction/
├── SelectionReference.cs              # 如需 Terminal 临时选择类型
└── PropertyCommandFactory.cs

src/DistributionDrawing.Rendering.Wpf/PropertyInspector/
├── PropertyEditor.cs
├── PropertyProjector.cs
└── 必要的请求/结果值对象

src/DistributionDrawing.Desktop/
└── 最小工具入口、输入提交和刷新协调
```

当前 Editor 基础设施位于 Rendering.Wpf。M5-C-4-B 为控制改动量沿用该位置，不在本阶段迁移到 Application 项目。Command 仍然属于编辑职责，不得调用 WPF Renderer 或保存 DrawingVisual。

### 16.3 不应修改

- Professional Domain 业务模型；
- Device、Terminal、Connection、ElectricalNode；
- Persistence DTO、FormatVersion 和迁移；
- Symbol 业务定义；
- WorkTicketData、SafetyMeasure、OperationStep；
- WorkScope 创建、删除和编辑实现。

## 17. M5-C-4-B 验收标准

- 用户只能通过明确 Terminal Pick 创建 GroundingPoint；
- 创建成功后新对象显示、被选中并出现在只读/可编辑属性快照中；
- 同一 Terminal 重复创建由 Domain 拒绝，且不产生历史或 Dirty；
- 删除未被引用的 GroundingPoint 成功，Undo 恢复同一 ID 和全部字段；
- 删除被 WorkScope 引用的 GroundingPoint 失败，不删除 WorkScope；
- Number、Location、Note 修改均可 Undo / Redo；
- TerminalId 保持只读且文本编辑不改变绑定；
- 每次成功修改后 Scene、HitTestIndex、Overlay 和 PropertyInspector 一致；
- Undo 回保存点后 `CommandStack.IsDirty` 为 false；
- 保存仍使用 FormatVersion 2；
- 不从开关、接地刀、Topology 或 Rendering 自动创建对象。

## 18. 后续拆分建议

### M5-C-4-C：WorkScope Editor

- 双 BoundaryPoint Terminal Pick；
- Side 和顶层 DeviceId 确认；
- Add / Remove / Change WorkScope Command；
- GroundingPoint 引用多选；
- WorkScope Undo / Redo、Dirty 和刷新闭环。

### M5-C-4-D：专业对象高级编辑

- 经业务确认后的 GroundingPoint Terminal 显式改绑；
- Professional Layout 偏移和标签拖动；
- 人工 WorkScope 显示路径；
- 对应 Layout DTO 与格式升级设计。

以上阶段仍不得引入自动 WorkScope、自动停电分析、自动安全措施、自动操作、WorkTicketData、SafetyMeasure 或 OperationStep。
