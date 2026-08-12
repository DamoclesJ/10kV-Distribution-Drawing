# M5-C-4-C-A WorkScope 编辑闭环实现设计

> 文档状态：实现前设计，仅定义 WorkScope Editor、Command 和刷新边界，不实现代码<br>
> 编制日期：2026-08-12<br>
> 依据：`docs/professional-editing-design.md`、`docs/professional-rendering-interaction-design.md`，以及当前 WorkScope、BoundaryPoint、DrawingDocument、GroundingPoint 编辑闭环、CommandStack、Selection、PropertyInspector 和 TerminalAnchorIndex 实现

## 1. 目标与第一阶段边界

本设计在 M5-C-4-B GroundingPoint 编辑链路上增加 WorkScope 的最小人工编辑闭环：

```text
显式选择 Boundary A
  ↓
显式选择 Boundary B
  ↓
输入 Description / 选择已有 GroundingPoint
  ↓
Professional CommandFactory
  ↓
CommandStack
  ↓
DrawingDocument Professional API
  ↓
Scene / HitTest / Selection / Overlay / PropertyInspector 刷新
```

第一版必须支持：

- 使用两个显式 BoundaryPoint 创建 WorkScope；
- 删除 WorkScope；
- 编辑 Description；
- 编辑对已有 GroundingPointId 的引用集合；
- Execute、Undo、Redo、Dirty 和统一刷新。

第一版不开放已有 WorkScope 的 BoundaryPoint 重绑。创建阶段已经需要双端子 Pick、设备归属确认和 Side 输入；如果同时开放边界重绑，会额外引入“选择修改哪一端、保留另一端、取消后恢复编辑表单、预览替换”等状态。该能力拆入后续 M5-C-4-C-C，不影响当前 Domain 或持久化合同。

## 2. 已有模型事实

### 2.1 WorkScope 聚合数据

当前 `WorkScope` 是 DrawingDocument 持有的独立 Professional 实体，包含：

```text
WorkScope
├── WorkScopeId
├── StartBoundary : BoundaryPoint
│   ├── DeviceId
│   ├── TerminalId
│   └── Side
├── EndBoundary : BoundaryPoint
│   ├── DeviceId
│   ├── TerminalId
│   └── Side
├── Description
└── GroundingPointIds[]
```

`BoundaryPoint` 是不可变内联值对象，没有独立 ID。Selection、Command 和 Persistence 都不能给它补充 ID。

### 2.2 DrawingDocument 是唯一业务修改入口

WorkScope Command 必须调用现有 API：

- `CreateWorkScope(...)`；
- `UpdateWorkScope(...)`；
- `RemoveWorkScope(...)`；
- `GetWorkScope(...)`。

不得直接修改 WorkScope 集合或内部属性。DrawingDocument 与 WorkScope 当前已经负责：

- WorkScopeId 全工程对象范围内唯一；
- BoundaryPoint 引用的 Terminal 存在；
- BoundaryPoint.DeviceId 对应有效 Device；
- Terminal 与 Device 的直接所有权一致；
- RingCabinet Interval 外部端子可通过 InternalAggregate 归属到其父 RingCabinet；
- 两个 BoundaryPoint 不能引用同一个 Terminal；
- Description 必填；
- GroundingPointId 存在且集合内不重复。

UI 可以提前提示空输入或明显重复选择，但不能复制或替代上述规则。

### 2.3 GroundingPoint 引用结构

当前 WorkScope 只保存 `IReadOnlyList<Guid> GroundingPointIds`，不保存 GroundingPoint 对象、TerminalId、Location、Number、Note 或图形信息。

因此：

- 创建或编辑 WorkScope 时，只能从当前 DrawingDocument 已有 GroundingPoint 中显式选择 ID；
- 创建 WorkScope 不创建 GroundingPoint；
- 删除 WorkScope 不删除 GroundingPoint；
- GroundingPoint 内容变化后，WorkScope 引用仍由稳定 ID 保持；
- GroundingPoint 删除仍由 DrawingDocument 的“被 WorkScope 引用时拒绝”规则保护。

### 2.4 持久化能力核对

当前 FormatVersion 2 的 `ProjectWorkScopeDto` 已包含：

- WorkScopeId；
- StartBoundary；
- EndBoundary；
- Description；
- GroundingPointIds。

现有 DTO 能无损表达本设计全部第一阶段修改，因此 M5-C-4-C-B 不修改 `ProjectProfessionalDto`、ProjectFileFormat 或迁移逻辑。

## 3. BoundaryPoint Pick 状态机

### 3.1 状态定义

建议复用 M5-C-4-B 的 Terminal Pick 入口，增加一个仅存在于 Editor 的 WorkScope 创建状态：

```text
Idle
  ↓ StartAddWorkScope
PickingBoundaryA
  ↓ Pick Terminal A
ConfirmingBoundaryA
  ↓ 输入并确认 Side A
PickingBoundaryB
  ↓ Pick Terminal B
ConfirmingBoundaryB
  ↓ 输入并确认 Side B
ReadyToCommit
  ↓ Commit
Idle
```

任意非 Idle 状态都允许 `Cancel → Idle`。工程切换、清空场景或 TerminalAnchorIndex 所依赖的 Scene/Layout 修订失效时，也必须取消草稿并回到 Idle。

状态职责：

| 状态 | 允许操作 | 持有的临时值 |
| --- | --- | --- |
| Idle | 普通选择、启动创建 | 无 |
| PickingBoundaryA | 点击一个可解析 Terminal | 无或当前工具标识 |
| ConfirmingBoundaryA | 确认 DeviceId 和输入 Side A | TerminalId A、候选 DeviceId A |
| PickingBoundaryB | 点击另一个可解析 Terminal | 完整 Boundary A 草稿 |
| ConfirmingBoundaryB | 确认 DeviceId 和输入 Side B | Boundary A、TerminalId B、候选 DeviceId B |
| ReadyToCommit | 输入 Description、选择 GroundingPoint、提交或取消 | 两个完整 Boundary 草稿及表单输入 |

这些状态和草稿：

- 不进入 Domain；
- 不进入 CommandStack；
- 不保存到 `.kvdrawing`；
- 不产生 Dirty；
- 不保存 Terminal、Device 或 GroundingPoint 对象引用。

### 3.2 Terminal Pick

TerminalId 继续通过当前 `TerminalAnchorIndex` 的毫米文档坐标锚点显式选择：

```text
鼠标点击
  ↓ 屏幕坐标转换为毫米文档坐标
TerminalAnchorIndex 命中
  ↓
稳定 TerminalId
```

不能使用以下信息替代 Terminal Pick：

- 最近 Device 或杆号；
- OverheadLine 折点或中点；
- 开关状态或接地状态；
- Topology 自动路径；
- 图形朝向推断。

若 TerminalId 不存在、当前 Layout 无法生成锚点或锚点已因场景刷新失效，Pick 失败并保持当前选择步骤，不猜测坐标。

### 3.3 DeviceId 归属解析与确认

Terminal Pick 得到 TerminalId 后，Editor 根据当前 DrawingDocument 的既有所有权关系提供候选 DeviceId：

- `OwnerType == Device`：候选 DeviceId 为 Terminal.OwnerId；
- `OwnerType == InternalAggregate` 且 Terminal 属于 RingCabinetInterval：候选 DeviceId 为该 Interval.ParentCabinetId；
- 其他无法解析到顶层 Device 的情况：拒绝形成 BoundaryPoint 草稿。

该过程只解析既有聚合所有权，不分析电气方向或工作范围。UI 必须向用户显示候选设备及端子，并要求用户确认；不得把 IntervalId 误作为 BoundaryPoint.DeviceId。提交时仍由 DrawingDocument 重新验证 DeviceId 与 Terminal 的归属。

### 3.4 Side 必须由用户输入

`BoundaryPoint.Side` 当前是必填字符串，Domain 只确认非空并去除首尾空白，没有定义可自动推导的枚举或固定词表。

第一版采用显式文本输入：

- A、B 两端分别输入 Side；
- 不能为空；
- 提交前去除首尾空白；
- 不根据端子方向、线路起止点、环网柜左右位置或图形坐标生成默认事实；
- UI 可以显示输入提示，但不能自动提交预填值。

若后续专业规范确认 Side 的固定词表，应先更新 Domain/设计，再改为下拉选择。本阶段不自行增加枚举。

### 3.5 草稿值对象

建议使用纯编辑器值对象：

```text
BoundaryPointDraft
├── DeviceId
├── TerminalId
└── Side

WorkScopeDraft
├── BoundaryA
├── BoundaryB
├── Description
└── GroundingPointIds[]
```

草稿不复用 Persistence DTO，也不长期保存 Domain 对象引用。提交前 CommandFactory 使用稳定 ID 重新解析当前工程，并构造 Domain `BoundaryPoint` 值对象。

## 4. 创建流程

### 4.1 用户流程

```text
用户选择“添加工作范围”
  ↓
显式 Pick Terminal A
  ↓
显示并确认设备归属，输入 Side A
  ↓
显式 Pick Terminal B
  ↓
显示并确认设备归属，输入 Side B
  ↓
输入 Description
  ↓
可选勾选当前工程已有 GroundingPoint
  ↓
ProfessionalCommandFactory.CreateAddWorkScope(...)
  ↓
CommandStack.ExecuteCommand(AddWorkScopeCommand)
  ↓
DrawingDocument.CreateWorkScope(...)
  ↓
统一刷新并选择新 WorkScope
```

创建界面不得根据两个端子自动计算两点之间的设备、线路、停电范围或 GroundingPoint。

### 4.2 创建前输入检查

CommandFactory 可以执行以下输入级检查：

- 两个 BoundaryPointDraft 均完整；
- DeviceId、TerminalId 非空；
- Side 和 Description 非空；
- 两端 TerminalId 不同；
- GroundingPointId 请求集合内不重复；
- WorkScopeId 生成一次且非空。

下列检查仍以 DrawingDocument 为最终判定：

- Terminal 和 Device 是否仍存在；
- DeviceId 与 Terminal 所有权是否一致；
- RingCabinet 聚合归属是否合法；
- GroundingPointId 是否仍存在；
- WorkScopeId 是否与任一工程对象冲突；
- 是否出现跨工程引用。

Command 执行前必须使用当前 ProjectRuntimeSession 的 DrawingDocument；旧工程的 Pick 草稿不得带入新工程。

## 5. Command 数据结构

### 5.1 WorkScopeCommandSnapshot

所有 WorkScope Command 复用一个不可变快照：

```text
WorkScopeCommandSnapshot
├── WorkScopeId
├── StartBoundary : BoundaryPointCommandValue
├── EndBoundary   : BoundaryPointCommandValue
├── Description
└── GroundingPointIds[]

BoundaryPointCommandValue
├── DeviceId
├── TerminalId
└── Side
```

快照要求：

- GroundingPointIds 在构造时复制为不可变数组或只读集合；
- 不保存 WorkScope、BoundaryPoint、Terminal、Device 或 GroundingPoint 对象引用；
- 不保存 Selection、Scene、Layout、WPF 对象或输入控件；
- 不复用 ProjectWorkScopeDto；
- `From(WorkScope)` 用于冻结删除和修改前的完整状态。

Command 可以沿用 M5-C-4-B 方式持有当前 DrawingDocument 聚合根和快照。工程切换时必须整体丢弃旧 CommandStack，避免跨工程重放。

### 5.2 AddWorkScopeCommand

| 阶段 | 行为 |
| --- | --- |
| Before | WorkScope 不存在 |
| After | 完整 WorkScopeCommandSnapshot |
| Execute | `DrawingDocument.CreateWorkScope(After...)` |
| Undo | `DrawingDocument.RemoveWorkScope(After.WorkScopeId)` |
| Redo | 使用同一 After 和 WorkScopeId 再次 Create |

WorkScopeId 在创建 Command 前只生成一次。Redo 不生成新 ID，也不重新读取当前输入框或当前 GroundingPoint 选择。

### 5.3 RemoveWorkScopeCommand

删除命令创建前通过 `DrawingDocument.GetWorkScope(id)` 获取对象并冻结完整 Before 快照：

| 阶段 | 行为 |
| --- | --- |
| Before | 完整 WorkScopeCommandSnapshot |
| After | WorkScope 不存在 |
| Execute | `DrawingDocument.RemoveWorkScope(Before.WorkScopeId)` |
| Undo | `DrawingDocument.CreateWorkScope(Before...)` |
| Redo | 再次 Remove |

删除只影响 WorkScope 本身：

- 不删除或修改 GroundingPoint；
- 不修改 BoundaryPoint 所引用的 Device、Terminal；
- 不修改 Connection、ElectricalNode 或 Layout；
- 不进行级联操作。

### 5.4 ChangeWorkScopeCommand

Change Command 保存完整 Before/After，并要求两者 WorkScopeId 相同：

| 阶段 | 行为 |
| --- | --- |
| Execute | `DrawingDocument.UpdateWorkScope(After...)` |
| Undo | 使用 Before 调用 Update |
| Redo | 使用 After 调用 Update |

M5-C-4-C-B 的 CommandFactory 只允许构造以下 After：

- StartBoundary 与 Before 完全相同；
- EndBoundary 与 Before 完全相同；
- Description 可改变；
- GroundingPointIds 可改变。

这使快照和 Command 可为后续边界重绑复用，但第一版 UI 与 Factory 不开放 DeviceId、TerminalId、Side 的普通属性编辑入口。

## 6. WorkScope 编辑流程

### 6.1 Description 编辑

```text
选择 WorkScope
  ↓
PropertyInspector 展示当前值快照
  ↓
用户提交 Description
  ↓
PropertyEditor / ProfessionalCommandFactory
  ↓
ChangeWorkScopeCommand(Before, After)
  ↓
CommandStack → DrawingDocument.UpdateWorkScope
```

Description 必填。UI 不直接修改 WorkScope，也不使用双向绑定把未提交文本写入 Domain。

### 6.2 GroundingPoint 引用编辑

第一版提供当前工程已有 GroundingPoint 的显式多选列表。每一项使用 GroundingPointId 作为值，可以用 Number、Location 作为辅助显示文字，但不能用显示文字作为引用键。

提交时：

- 只传递选中的稳定 GroundingPointId；
- 不创建缺失 GroundingPoint；
- 不复制 GroundingPoint 字段；
- 不自动选择“位于两个边界之间”的 GroundingPoint；
- 不根据拓扑或图形位置推荐并自动确认关联；
- DrawingDocument 最终验证引用存在且不重复。

取消编辑时丢弃多选输入缓冲，不产生 Command 或 Dirty。

### 6.3 BoundaryPoint 重绑

M5-C-4-C-B 不支持在 PropertyInspector 中直接编辑：

- StartBoundary.DeviceId；
- StartBoundary.TerminalId；
- StartBoundary.Side；
- EndBoundary.DeviceId；
- EndBoundary.TerminalId；
- EndBoundary.Side。

后续重绑必须重新进入专用 Boundary Pick 工具，明确选择要替换的 A 或 B、Pick Terminal、确认 DeviceId、输入 Side，再生成 ChangeWorkScopeCommand。不得开放 Guid 文本框作为替代。

## 7. Undo、Redo 与 Dirty

WorkScope Command 进入 M3/M5 已有的同一个 CommandStack，不建立 Professional 专用历史。

### 7.1 行为

- Add → Undo 删除同一 WorkScopeId；Redo 使用原快照恢复；
- Remove → Undo 恢复同一 ID、两个边界、Description 和全部 GroundingPointId；Redo 再次删除；
- Change → Undo 恢复完整 Before；Redo 恢复完整 After；
- Redo 不读取当前 UI 草稿；
- Pick 和未提交表单不进入 Undo/Redo。

### 7.2 失败语义

现有 CommandStack 在 `Execute()` 成功后才写入历史，因此 Domain 拒绝时：

- History、CurrentIndex、CurrentStateId 不变；
- IsDirty 不变；
- 当前有效 Domain 状态不产生部分修改；
- Selection 和当前 Scene 保持有效；
- UI 显示 Domain 返回的错误，不自动修复引用。

Undo/Redo 抛出异常时，CommandStack 索引保持不变。Editor 不跳过失败项，也不伪造成功刷新。

### 7.3 Dirty 与保存点

- 成功 Add、Remove、Change 后由 CommandStack 状态自然进入 dirty；
- Undo 回到 SavedStateId 时恢复 clean；
- Redo 离开保存点时重新 dirty；
- Domain 不保存 Dirty；
- 不为 Professional 修改单独调用持久化 Session 的永久 Dirty 标记；
- 保存成功后仍由现有工程生命周期调用 `CommandStack.MarkSaved()`。

## 8. Selection 与统一刷新

### 8.1 成功后的刷新顺序

所有 Execute、Undo、Redo 成功后复用 M5-C-4-B 的同一入口：

```text
Domain 修改成功
  ↓
DrawingSceneBuilder 重建 Scene
  ↓
TerminalAnchorIndex / ProfessionalSceneBuilder 重建
  ↓
HitTestIndex 重建
  ↓
SelectionReference 有效性校验或更新
  ↓
SelectionOverlay 重建
  ↓
SelectionObjectResolver 按稳定 ID 重新解析
  ↓
PropertyProjector 生成新快照
```

不得创建 WorkScope 专用 Scene 缓存或第二套刷新机制。

### 8.2 Selection 策略

| 操作 | 成功后的 Selection |
| --- | --- |
| Add WorkScope | 选择新 WorkScope；两个 BoundaryPoint 标记同时高亮 |
| Change WorkScope | 保留当前 WorkScope；刷新双边界高亮和属性快照 |
| Remove WorkScope | 若当前选择是目标则清除；其他选择保持 |
| Undo Add | 对象消失，清除失效 WorkScope Selection |
| Redo Add | 对象恢复，第一版不自动恢复历史 Selection |
| Undo Remove | 对象恢复，第一版保持当前 Selection，不自动选中 |
| Redo Remove | 清除任何指向已删除对象的 Selection |
| Undo/Redo Change | 保留有效 WorkScope Selection |

BoundaryPoint 没有独立 ID，点击任一边界仍产生同一个 `SelectionReference(WorkScope, WorkScopeId)`。SelectionManager 不保存 WorkScope 或 BoundaryPoint 对象引用。

### 8.3 创建草稿显示

创建工具可使用临时 Editor Overlay 显示：

- 已确认的 Boundary A；
- 当前待确认的 Boundary B；
- Side 输入状态。

草稿 Overlay 不进入 DrawingScene 的工程内容层、不进入导出/打印、不写入 Domain，也不反向生成 BoundaryPoint。第一版若不实现草稿 Overlay，可仅用面板文字显示已选 ID 和 Side，不影响业务闭环。

## 9. 错误处理

至少处理以下失败：

| 场景 | 处理 |
| --- | --- |
| TerminalAnchor 无法命中 | 保持当前 Pick 步骤，提示重新选择 |
| Terminal 已失效 | Command 不执行或由 Domain 拒绝，草稿不提交 |
| DeviceId 与 Terminal 归属不一致 | Domain 拒绝，不进入历史 |
| RingCabinet Interval 错用 IntervalId 作为 DeviceId | Domain 拒绝；Editor 应显示父 RingCabinet 候选 |
| 两端使用同一 Terminal | 提前提示并由 Domain 最终拒绝 |
| Side 或 Description 为空 | 不创建 Command |
| GroundingPointId 缺失或重复 | Domain 拒绝，不自动移除或补全 |
| WorkScopeId 冲突 | Domain 拒绝，不重新生成并静默重试 |
| 工程在 Pick 期间切换 | 取消草稿，禁止跨工程提交 |
| 删除目标已不存在 | 不创建成功命令，刷新当前有效选择 |

失败时不产生部分 WorkScope，不污染 CommandStack，不改变 Dirty，也不从 Topology、Rendering 或其他 Professional 对象猜测修复数据。

## 10. M5-C-4-C-B 最小实现范围

### 10.1 必须实现

1. WorkScope 创建工具状态机及 Cancel；
2. 复用 TerminalAnchorIndex 的 Boundary A / B 显式 Terminal Pick；
3. 直接 Device 和 RingCabinet Interval 外部端子的候选 DeviceId 解析；
4. A、B 两端 Side 的显式输入与确认；
5. Description 输入；
6. 当前工程已有 GroundingPointId 的可选多选；
7. WorkScopeCommandSnapshot / BoundaryPointCommandValue；
8. AddWorkScopeCommand；
9. RemoveWorkScopeCommand；
10. ChangeWorkScopeCommand；
11. 第一版 Change 仅编辑 Description 和 GroundingPointIds；
12. ProfessionalCommandFactory / PropertyEditor 接入；
13. CommandStack、Undo/Redo、Dirty 接入；
14. Scene、HitTest、Selection、Overlay、PropertyInspector 统一刷新；
15. Domain 拒绝时不产生历史、Dirty 或部分修改。

### 10.2 建议修改范围

```text
src/DistributionDrawing.Rendering.Wpf/Interaction/Professional/
├── BoundaryPointCommandValue.cs
├── WorkScopeCommandSnapshot.cs
├── AddWorkScopeCommand.cs
├── RemoveWorkScopeCommand.cs
├── ChangeWorkScopeCommand.cs
└── ProfessionalCommandFactory.cs

src/DistributionDrawing.Rendering.Wpf/Interaction/
└── 最小 WorkScope Pick 状态值对象（如需要）

src/DistributionDrawing.Rendering.Wpf/PropertyInspector/
├── PropertyEditor.cs
├── PropertyCommandFactory.cs
└── WorkScope 编辑值快照/投影的最小扩展

src/DistributionDrawing.Desktop/
└── 创建、取消、删除、编辑和统一刷新入口
```

除非现有实现暴露出无法调用的必要入口，M5-C-4-C-B 不修改 Domain、Topology、Persistence、Layout、Symbol 或工程格式。

### 10.3 验收标准

- 用户必须分别显式 Pick 两个不同 Terminal，并分别输入 Side；
- RingCabinet Interval 外部端子最终保存父 RingCabinetId；
- 创建成功后选择新 WorkScope，并同时高亮两个边界；
- 创建失败不新增对象、不产生历史和 Dirty；
- 删除 WorkScope 不影响其引用的 GroundingPoint；
- Undo 删除恢复同一 WorkScopeId 和完整快照；
- Description 和 GroundingPoint 引用修改可 Undo/Redo；
- GroundingPoint 只能引用当前工程已有对象；
- Undo 回保存点后 IsDirty 为 false；
- 保存继续使用 FormatVersion 2；
- 不自动计算工作范围或停电范围。

## 11. 明确推迟的能力

以下能力不进入 M5-C-4-C-B：

- 已有 WorkScope 的 BoundaryPoint A/B 重绑；
- Side 固定枚举或专业词表；
- 多 WorkScope 批量编辑；
- 自动边界选择；
- 自动 WorkScope 或自动覆盖路径；
- 根据 Topology 计算两边界之间的设备；
- 自动停电分析、自动安全措施、自动 GroundingPoint；
- WorkTicketData、SafetyMeasure、OperationStep；
- WorkScope 图面路径编辑或 WorkScopeLayout；
- Persistence FormatVersion 或 DTO 修改；
- Rendering 反推或修改 Professional Domain。

后续若实现 BoundaryPoint 重绑，应作为 M5-C-4-C-C 单独设计和验收，继续使用显式 Terminal Pick、Device 归属确认和 Side 人工输入。
