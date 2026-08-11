# M3-B-4-A 编辑器 Undo/Redo 架构设计

> 文档状态：设计稿，仅定义 Command 管理体系，不实现代码或 UI<br>
> 编制日期：2026-08-11<br>
> 依据：`docs/editor-architecture.md`、`docs/layout-editing-design.md`、当前 `MoveCommand`、`SelectionManager` 和只读 `PropertyInspector`

## 1. 目标与范围

本设计为编辑器建立文档级 Command 管理体系，使布局移动、后续属性修改及结构操作能够采用一致的执行、撤销、重做和保存点语义。

目标链路如下：

```text
用户提交编辑意图
        ↓
CommandManager
        ↓
Command.Execute
        ↓ 成功
CommandStack 记录历史并移动 CurrentIndex
        ↓
EditorSession 发布 DocumentChanged
        ↓
DrawingScene / Selection / PropertyInspector 刷新
```

本阶段只修改设计文档，不修改 Domain、Layout、Interaction、Rendering 或 Desktop 代码，不实现菜单、快捷键和运行时 Undo/Redo。

## 2. 设计边界

### 2.1 Command 管理对象

建议在后续 Application / Editor 层引入以下职责对象：

| 对象 | 职责 | 不负责 |
| --- | --- | --- |
| `EditorCommand` | 描述一次可撤销的工程修改，保存最小 Before/After 数据 | 鼠标捕获、WPF 绘制、保存文件 |
| `CommandManager` | 串行执行 Execute、Undo、Redo，协调校验、历史和变更通知 | 持久化业务对象、直接操作控件 |
| `CommandStack` | 保存有序历史、当前索引、容量和状态标识 | 执行业务规则、刷新 Rendering |
| `EditorSession` | 持有当前 Domain、Layout、CommandStack 和保存点 | 保存 Symbol、DrawingVisual 或 UI 焦点 |
| `DocumentChanged` | 通知场景、选择解析和属性投影刷新 | 携带完整对象快照 |

`SelectionManager`、缩放、平移、悬停和 PropertyInspector 展开状态不属于工程内容，默认不进入 CommandStack。

### 2.2 修改边界

- Layout Command 只修改 Layout，不改变 Domain、电气拓扑或开关状态。
- Domain Command 通过公开领域行为修改名称、属性或状态，不由 UI 直接写字段。
- 同时涉及 Domain 和 Layout 的结构操作必须使用一个原子 Command 或 CompositeCommand。
- Symbol、DrawingScene、HitTestIndex 和 DrawingVisual 都是派生或瞬时对象，不作为撤销数据。
- Undo/Redo 历史仅存在于当前 EditorSession，不写入工程文件。

## 3. Command 合同与生命周期

### 3.1 基础合同

后续统一 Command 接口至少应表达：

- `CommandId`：本次操作的唯一标识。
- `DisplayName`：用于后续菜单或日志，例如“移动杆塔”。
- `AffectedObjectIds`：受影响的稳定业务或 Layout ID。
- `Execute(EditorContext)`：从 Before 应用到 After。
- `Undo(EditorContext)`：从 After 恢复到 Before。
- `Redo(EditorContext)`：从 Before 再次应用到 After。
- 可选的合并判断与合并结果，但默认命令不可合并。

Command 不保存 `MouseEventArgs`、WPF 控件、SceneElement、Symbol 实例或屏幕坐标。布局命令保存文档毫米坐标；业务命令保存明确的业务值或受影响聚合的最小快照。

当前 `MoveCommand` 已具备 PoleLayout 的 Before、After、Execute 和 Undo，可视为最小原型。后续接入统一体系时应补齐稳定命令标识、Redo 语义、执行结果和必要的版本校验；本阶段不修改其代码。

### 3.2 生命周期

一条命令按以下生命周期运行：

```text
Created
   ↓ Execute 成功
Applied
   ↓ Undo 成功
Undone
   ↓ Redo 成功
Applied
```

约束如下：

1. 新命令只有 Execute 成功后才能进入历史。
2. Execute 失败时工程状态、历史列表、CurrentIndex 和 Dirty 状态保持不变。
3. Undo 只能作用于当前最后一条已应用命令。
4. Redo 只能作用于当前第一条未应用命令。
5. Undo 或 Redo 失败时不得先移动 CurrentIndex；历史必须继续对应实际工程状态。
6. 同一 EditorSession 的修改命令串行执行，不允许 Execute、Undo、Redo 并发交错。

命令本身不直接重绘。CommandManager 在状态变更成功后统一发布 `DocumentChanged`，由场景生成、命中索引、选择解析和属性查看器读取最新状态。

### 3.3 Execute

执行新命令时：

1. 校验目标对象和 Layout 仍存在。
2. 校验命令的 Before 值或基础修订与当前状态一致。
3. 原子应用 After 值，并完成 Domain 或 Layout 校验。
4. 若 CurrentIndex 后仍有历史，删除该 Redo 分支。
5. 按合并规则决定替换最后一条历史或追加新历史。
6. 更新 CurrentIndex 和当前状态标识。
7. 发布一次 DocumentChanged。

不得在 Execute 前清除 Redo 分支。否则命令执行失败会无故丢失原有 Redo 能力。

### 3.4 Undo

Undo 的目标是 `Entries[CurrentIndex - 1]`：

1. CurrentIndex 为 0 时不可撤销。
2. 调用目标命令 Undo，恢复其 Before 状态。
3. 校验恢复后的 Domain 与 Layout 一致性。
4. 成功后 CurrentIndex 减 1，并切换到该命令的 BeforeStateId。
5. 发布一次 DocumentChanged。

Undo 不删除历史项；被撤销的命令保留在 CurrentIndex 右侧，供 Redo 使用。

### 3.5 Redo

Redo 的目标是 `Entries[CurrentIndex]`：

1. CurrentIndex 等于 Entries.Count 时不可重做。
2. 调用目标命令 Redo，重新应用其 After 状态。
3. 校验重做后的 Domain 与 Layout 一致性。
4. 成功后 CurrentIndex 加 1，并切换到该命令的 AfterStateId。
5. 发布一次 DocumentChanged。

Redo 应使用命令已确认的 After 值，不重新读取当前鼠标位置、文本框内容或默认值，也不重新执行与原操作无关的自动推断。

## 4. CommandStack

### 4.1 历史结构

CommandStack 使用单一有序列表和当前索引：

```text
Entries: [C1, C2, C3, C4]
                  ↑
             CurrentIndex = 2

已应用：C1、C2
可 Redo：C3、C4
下一次 Undo：C2
下一次 Redo：C3
```

定义：

- `Entries`：按首次成功执行顺序保存的历史项。
- `CurrentIndex`：已应用历史项数量，范围为 `0..Entries.Count`。
- `CanUndo`：`CurrentIndex > 0`。
- `CanRedo`：`CurrentIndex < Entries.Count`。
- `MaximumCapacity`：历史最大容量的可配置预留值。
- `CurrentStateId`：当前工程内存状态的唯一标识。
- `SavedStateId`：最近一次成功保存的工程状态标识。

单列表模型与 UndoStack/RedoStack 语义等价，但更直接地表达当前索引、Redo 分支截断、容量裁剪和保存点位置。

### 4.2 历史项

每个 HistoryEntry 至少包含：

- Command。
- `BeforeStateId`。
- `AfterStateId`。
- 可选的提交时间和显示名称。

StateId 是会话内单调生成或全局唯一的状态身份，不等同于数组索引，也不写入工程文件。新分支即使内容偶然与旧分支相同，也生成新的 StateId；MVP 不进行完整工程深比较。

### 4.3 新分支

用户 Undo 后执行新的 Command 时：

```text
C1 → C2 → C3
      ↑ Undo 到 C2
      └─ Execute N1

结果：C1 → C2 → N1
原 C3 被移除，不能再 Redo
```

只有 N1 Execute 成功后才删除 C3 及其后续历史。Selection 或视图操作不会截断 Redo 分支，因为它们不是文档命令。

### 4.4 最大容量

第一阶段可以使用固定默认容量，并预留配置入口，不要求现在确定具体数值。达到 MaximumCapacity 后：

1. 从最旧端移除已应用历史项。
2. CurrentIndex 按移除数量同步减小。
3. 不改变当前工程内容和 CurrentStateId。
4. 不因裁剪历史改变 Dirty 状态。
5. 不移除 CurrentIndex 右侧仅用于 Redo 的新历史来为旧历史腾出空间；新增命令本身已先截断 Redo 分支。

若一次 CompositeCommand 的快照过大，容量不能解决内存峰值问题；后续应通过最小快照和命令大小预算处理，不在本阶段设计磁盘历史或历史分页。

## 5. 编辑事务

### 5.1 单次拖动

一次完整指针手势只形成一条 MoveCommand：

```text
MouseDown / Armed
        ↓
MouseMove / Preview（0..N 次，不进入历史）
        ↓
MouseUp
        ↓
MoveCommand(Before, After)
        ↓
Execute 一次并记录一个 HistoryEntry
```

取消拖动、捕获丢失和零位移都不创建历史项。拖动预览不是正式事务，不增加 CurrentStateId，不改变 SavedStateId。

当前最小拖动实现会在预览期间替换 PoleLayout 并在 MouseUp 调用 MoveCommand。接入正式 CommandManager 时，应以 `layout-editing-design.md` 定义的 PreviewLayoutSnapshot 或等价临时状态为准，确保正式 Layout 只在 Execute 时提交；本阶段只记录该迁移方向，不修改代码。

### 5.2 属性修改

属性修改必须以“用户提交”作为事务边界：

- 文本属性：进入编辑时记录原值，按 Enter、失去焦点或明确“应用”时提交最终值；每个字符不创建 Command。
- 枚举、布尔值或开关状态：用户确认一次选择形成一条 Command。
- 同一面板一次应用多个字段：使用一个类型化 UpdatePropertiesCommand 或 CompositeCommand，全部成功或全部恢复。
- 输入校验失败：保留编辑提示，不执行 Command，不进入历史。
- 值未变化：不执行 Command。

Property Panel 只提交新值和目标 ID，不直接修改 Domain 或 Layout。属性 Command 成功、Undo 或 Redo 后，PropertyInspector 按当前 SelectionReference 重新解析并投影。

### 5.3 CompositeCommand

后续创建、删除、连接或模板拖入等多步骤操作可能同时修改 Domain 和 Layout，应作为一个 CompositeCommand：

1. 按确定顺序执行子步骤。
2. 任一子步骤失败时，按反向顺序恢复已经执行的子步骤。
3. 只有整体成功才写入一个 HistoryEntry。
4. Undo 按反向顺序撤销全部子步骤。
5. Redo 按原顺序重做全部子步骤。

CompositeCommand 对 CommandStack 是一个不可拆分的历史项。用户不能撤销到聚合对象已经创建但 Layout 尚未创建的中间状态。

## 6. Command 合并策略

### 6.1 拖动合并

MouseMove 预览天然合并在单次 MouseDown 到 MouseUp 的 MoveCommand 中，不需要 CommandStack 再合并。

两个独立的拖动手势默认保留为两条命令，即使目标相同且时间接近。这样可保持用户动作边界清晰，并避免跨越保存点、选择变化或其他操作。第一阶段不自动合并连续拖动。

后续如确有体验需求，可只在同时满足以下条件时合并：

- 两条命令类型相同、目标 LayoutKey 相同。
- 前一条 After 与后一条 Before 完全一致。
- 中间没有其他文档命令、保存操作或选择目标变化。
- 两次提交处于明确的短时间窗口。
- 合并不会跨越 SavedStateId。

合并结果保留第一条 Before 和最后一条 After，并生成新的 AfterStateId。任何条件不满足时追加历史项，不做猜测性合并。

### 6.2 属性合并

文本框内部的连续键入在 UI 编辑事务中先收集，提交时只生成一条命令。已经提交的两次属性修改默认不在 CommandStack 自动合并。

滑块或连续数值控件可借用拖动事务：PointerDown 记录 Before，交互过程只预览，PointerUp 以最终值提交一条 Command。不得用时间窗口把不同属性或不同对象的修改合并。

### 6.3 多步骤操作边界

以下操作必须保持独立历史边界：

- 不同选择对象的修改。
- Domain 修改与无关 Layout 修改。
- 用户明确点击两次“应用”的属性提交。
- 保存点前后的命令。
- 创建、删除、连接等结构事务与其前后普通移动。
- WorkScope、GroundingPoint 等后续安全措施操作。

批量移动或批量属性修改只有在用户以一次明确操作发起时，才作为一个 CompositeCommand；不能事后根据时间相近自动拼接。

## 7. 保存点与 Dirty 状态

### 7.1 状态判定

EditorSession 使用状态身份判定工程是否修改：

```text
IsDirty = CurrentStateId != SavedStateId
```

不建议只用 `CurrentIndex != SavedIndex`，因为 Redo 分支截断、历史合并和容量裁剪会改变索引含义。也不建议每次操作序列化整个工程进行深比较。

新建空工程初始化时，CurrentStateId 与 SavedStateId 相同；是否要求首次保存路径由保存用例决定，不与内容 Dirty 混为同一字段。

### 7.2 保存成功

保存流程应记录本次保存快照对应的 StateId：

1. 捕获一致的 Domain + Layout 快照及 CurrentStateId。
2. Infrastructure 写入并验证工程文件。
3. 保存成功后将 SavedStateId 设置为该快照的 StateId。
4. 如果保存期间没有新命令，IsDirty 变为 false。
5. 如果保存期间又产生新命令，当前 StateId 已变化，保存完成后仍保持 IsDirty=true。

保存失败不得修改 SavedStateId，也不清空 CommandStack。保存成功同样不强制清空 Undo/Redo，用户可以撤销到保存前状态；此时 CurrentStateId 与 SavedStateId 不同，工程再次标记为 Dirty。

### 7.3 Undo/Redo 与保存点

- Undo 回到 SavedStateId 时，IsDirty 自动变为 false。
- Redo 离开 SavedStateId 时，IsDirty 自动变为 true。
- Undo 后建立新分支，如果新状态不是 SavedStateId，保持 Dirty。
- 历史容量裁剪掉保存点对应的命令时，仍保留 SavedStateId；Dirty 判断不受索引变化影响。
- 若保存点已不可通过剩余历史到达，界面仍可正确显示 Dirty，只是不再承诺能够 Undo 回到该状态。

选择、高亮、视图缩放、画布平移和 PropertyInspector 展开状态不生成新 StateId，也不改变 Dirty。

## 8. 刷新与现有组件关系

### 8.1 成功操作后的刷新

Execute、Undo 或 Redo 成功后采用同一刷新链路：

```text
CommandManager
    ↓ DocumentChanged(AffectedObjectIds, CurrentStateId)
DrawingSceneBuilder 读取最新 Domain + Layout
    ↓
重建 DrawingScene + HitTestIndex
    ↓
SelectionManager 保留仍有效的 SelectionReference
    ↓
PropertyInspector 重新 Resolve / Project
    ↓
DrawingSceneRenderer 刷新
```

若命令删除了当前选择对象，SelectionManager 清除选择；如果对象仍存在，则保持稳定 ID 选择，不恢复旧对象引用或旧 HitTestEntry。

### 8.2 失败处理

| 场景 | 处理 |
| --- | --- |
| Execute 校验失败 | 不写历史、不截断 Redo、不发布成功变更 |
| Undo 失败 | CurrentIndex 不变，保持当前工程状态并报告错误 |
| Redo 失败 | CurrentIndex 不变，保留 Redo 项并报告错误 |
| CompositeCommand 部分失败 | 反向恢复已执行步骤，整体不入历史 |
| 场景重建失败 | Command 状态仍以业务提交结果为准，报告渲染错误并保留上一个有效 Visual |
| SelectionReference 失效 | 清除选择和 PropertyInspector，不回滚已成功命令 |

如果命令的回滚本身失败，说明会话一致性无法保证，应停止继续编辑并提示重新载入，而不是继续移动历史索引。本阶段不设计自动修复损坏会话。

## 9. 与现有 MoveCommand 的衔接

当前 `MoveCommand` 仅服务 PoleLayout 最小拖动闭环，已经提供：

- Before PoleLayout。
- After PoleLayout。
- Execute：替换为 After。
- Undo：恢复为 Before。

后续 M3-B-4 实现阶段需要在不改变 Domain 和 Symbol 边界的前提下：

1. 将命令交由 CommandManager 执行，不由 Desktop 直接协调历史。
2. 增加 Redo 或确认 Execute 可作为严格等价的 Redo；建议接口仍显式提供 Redo。
3. 仅在 MouseUp 提交一条命令，预览不进入历史。
4. CommandStack 保存该命令及 BeforeStateId / AfterStateId。
5. Execute、Undo、Redo 后复用当前 DrawingScene 刷新链路。
6. 未来其他 Layout 类型使用各自类型化命令，不把 PoleLayout 命令扩展成弱类型 `object` 命令。

## 10. 校验与测试建议

后续实现至少覆盖：

- Execute 成功后历史增加一项、CurrentIndex 前移、Redo 分支清空。
- Execute 失败不改变工程、历史、索引和 Dirty。
- Undo/Redo 正确恢复 MoveCommand 的 Before/After。
- Undo/Redo 失败时 CurrentIndex 不移动。
- 一次拖动的多次 Preview 只产生一条历史。
- 两次独立拖动默认产生两条历史。
- Undo 后执行新命令会截断旧 Redo 分支。
- 属性文本一次提交只产生一条历史，校验失败不入历史。
- CompositeCommand 部分失败能完整恢复。
- 保存后 Dirty=false；新增命令后为 true；Undo 回保存点后再次为 false。
- 容量裁剪不改变当前工程和 Dirty 判断。
- Selection、缩放、平移和只读属性查看不进入历史、不改变 Dirty。
- Execute、Undo、Redo 后场景、命中索引、高亮和属性面板保持一致。

## 11. 本阶段不实现

- CommandManager、CommandStack、HistoryEntry 或 CompositeCommand 代码。
- 对当前 MoveCommand、Domain、Layout、Rendering、Interaction 或 Desktop 的修改。
- Undo/Redo 菜单、快捷键、按钮和状态提示 UI。
- 属性编辑命令和属性编辑控件。
- 工程文件保存、打开、自动保存或崩溃恢复。
- 多用户协同、永久事件日志、跨会话撤销或磁盘历史。
- 自动合并连续拖动、历史分组界面或历史时间线。
