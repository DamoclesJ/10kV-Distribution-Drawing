# P0-10-D-1 Desktop Command Integration Design

## 1. 目标与边界

本设计定义 Desktop 用户操作入口如何接入现有 Application Command、工程工作流和 Rendering 交互能力。Desktop 负责呈现入口、收集输入和显示状态，不重新实现 Domain 规则、拓扑逻辑或持久化。

用户操作的目标链路为：

```text
View
  ↓
ViewModel ICommand
  ↓
Application / workflow service
  ↓
Domain Command or project workflow
```

Selection、Inspector 和 Canvas 手势属于交互状态；会改变 Domain 或 Layout 的操作必须经过可追踪的 Application/Command 边界。

## 2. Menu 设计

### 2.1 文件菜单

文件菜单提供：

- **New**：创建新工程；
- **Open**：打开工程文件；
- **Save**：保存当前工程；
- **Exit**：请求关闭应用。

New、Open 和 Exit 在当前工程 Dirty 时必须经过统一的未保存修改确认流程。Save 成功后更新保存点和 StatusBar 状态。菜单项的 Enabled 状态由当前工作区和工作流状态决定，不由 View 直接检查 Domain。

### 2.2 编辑菜单

编辑菜单提供：

- **Undo**：撤销当前 CommandStack 中的可撤销操作；
- **Redo**：重做已撤销操作；
- **Delete**：删除当前支持的 SelectionTarget。

Undo/Redo 只调用当前工作区 CommandStack，不复制命令逻辑。Delete 通过既有删除 Command 或 Application 入口执行，并在成功后同步 Scene、Selection 和 Inspector。

### 2.3 视图菜单

视图菜单提供：

- **Zoom**：调用 Canvas 视口控制器调整显示比例；
- **Grid**：切换 Rendering/Canvas 的网格显示状态。

Zoom 和 Grid 是显示状态，不修改 Domain、Connection 或 Graph，也不进入 Domain Undo 历史。若未来网格配置需要保存，应另行设计 Layout/Preferences 持久化合同。

## 3. Shortcut 设计

首版快捷键如下：

| 快捷键 | 操作 | 边界 |
| --- | --- | --- |
| `Ctrl+N` | New | 工程工作流，遵循 Dirty 确认 |
| `Ctrl+O` | Open | 工程工作流，遵循 Dirty 确认 |
| `Ctrl+S` | Save | 工程工作流 |
| `Ctrl+Z` | Undo | 当前会话 CommandStack |
| `Ctrl+Y` | Redo | 当前会话 CommandStack |
| `Delete` | Delete | 当前 Selection 对应的删除入口 |
| `Esc` | Cancel/Clear | 取消当前工具、Preview 或临时选择状态 |

快捷键应由 Window/Input 层路由到 ViewModel Command，避免在每个控件的 code-behind 中重复处理。文本编辑控件拥有焦点时，快捷键处理必须尊重控件默认行为，除非该操作明确属于窗口级命令。

## 4. Toolbar 设计

Toolbar 是 Menu 的可视化快捷入口，不建立第二套业务操作。首版可包含：

- New、Open、Save；
- Undo、Redo、Delete；
- Zoom In、Zoom Out、Fit；
- Grid 开关。

每个按钮绑定同一个 ViewModel `ICommand` 实例或同一命令适配器，保证菜单、Toolbar 和快捷键共享 CanExecute 状态、错误处理和 Dirty 更新逻辑。Toolbar 不直接调用 `ProjectService`、`DrawingDocument` 或 `Command.Execute()`。

## 5. MVVM Command Binding

### 5.1 View

View 只声明菜单项、按钮、快捷键输入绑定和状态显示。View 不解析 Domain 对象，也不决定某个设备是否允许删除或编辑。

### 5.2 ViewModel

MainWindow ViewModel 或工作区 ViewModel 暴露：

- `NewCommand`、`OpenCommand`、`SaveCommand`、`ExitCommand`；
- `UndoCommand`、`RedoCommand`、`DeleteCommand`；
- `ZoomCommand`、`GridCommand`、`CancelCommand`；
- `CanExecute` 状态和 StatusBar 文本。

ViewModel 将用户输入转换成 Application/工作流服务需要的参数，并订阅工作区 SessionChanged、SelectionChanged 和 CommandStack 状态变化。ViewModel 不持有 DTO、WPF `DrawingVisual` 或用于修改 Domain 的隐式引用。

### 5.3 Application 与工作流

- New/Open/Save/Exit 通过 Project Workflow 入口执行；
- Undo/Redo/Delete 通过当前运行时会话和 CommandStack 执行；
- Zoom/Grid 通过 Rendering/Viewport 服务执行；
- 失败结果转换为用户可理解的错误状态，不让 View 处理底层异常细节。

命令成功后由会话统一触发必要的 Scene 重建、Selection 修正、Inspector 刷新和 Dirty 更新。

## 6. Command 状态与错误反馈

CanExecute 应至少考虑：

- 是否存在当前工程会话；
- 是否存在可撤销或可重做操作；
- 是否存在可删除的 Selection；
- 工程是否正在执行 Open/Save；
- 当前控件焦点是否正在编辑文本。

命令执行失败时：

- Domain/工程状态不得留下半完成修改；
- Selection 和 Scene 保持可解释状态；
- StatusBar 显示简短提示，必要时由 Desktop 显示错误对话框；
- 不在 View 中吞掉异常或直接修改对象以“补救”。

## 7. StatusBar 设计

StatusBar 显示当前工作区的最小状态：

- 当前工程标题或文件路径；
- `Saved` / `Modified` 状态；
- 当前 Selection 或工具提示；
- 命令执行失败的简短信息；
- Grid、Zoom 等视图状态摘要。

StatusBar 只读 ViewModel 状态。它不从 Domain 集合自行计算电气信息，也不显示未经过 Inspector/Selection 解析的内部对象引用。

## 8. Selection 与 Canvas 交互

Canvas HitTest 得到 SelectionTarget 后更新 SelectionService/SelectionManager，Inspector 随 SelectionChanged 刷新。Delete 命令读取当前 Selection，成功后清除或修正 Selection；Undo/Redo 按既有 Selection transition 规则恢复界面状态。

Canvas 的拖拽、缩放和网格切换属于交互/显示层。Move 等会改变可持久化 Layout 的操作仍通过 CommandStack，不能由 Toolbar 或 ViewModel 直接写 Layout 属性。

## 9. 非目标

本设计不实现：

- 设备库和模板选择 UI；
- 工程导出、打印或外部格式发布；
- 工作票业务流程、审批和专业校核；
- 权限、登录和多人协作；
- 通过快捷键直接修改拓扑或绕过 Domain 规则。

## 10. 后续实施建议

- **P0-10-D-2 Command Binding Runtime**：实现 ViewModel Commands、CanExecute 和工作区状态订阅；
- **P0-10-D-3 Shortcut/Toolbar Runtime**：接入 WPF 输入绑定和 Toolbar；
- **P0-10-D-4 Command Feedback**：统一错误、Dirty、Selection 和 StatusBar 更新。

实现时优先复用现有 `ProjectWorkspaceController`、`ProjectRuntimeSession`、`CommandStack`、Selection 和 Viewport 服务，保持 Desktop 只是用户交互编排层。
