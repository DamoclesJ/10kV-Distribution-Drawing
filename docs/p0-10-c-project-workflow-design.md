# P0-10-C-1 Project Workflow Design

## 1. 目标与边界

本设计定义 Desktop 工程工作流的生命周期边界：新建、打开、保存和 Dirty 状态管理。工作流负责协调现有 Domain、Application、Infrastructure 和 Rendering 能力，不重新定义任何电气模型或工程文件格式。

当前目标仍是具有基础电气拓扑识别能力的 10kV 工作票绘图工具。工程工作流只负责工作区生命周期，不实现工作票业务审批或专业计算。

## 2. New Project

新建工程由 Desktop 发起，由 Application/工作流服务完成：

```text
New request
    ↓
Create DrawingDocument
    ↓
Create initial project session
    ↓
Build empty Scene
    ↓
Show empty Canvas and clear Selection
```

新建流程应初始化：

- `DrawingDocument` 及其工程标识；
- 工程标题和必要元数据；
- 初始 Layout/Scene 状态；
- 空的 Selection 和 Inspector 状态；
- 初始保存点和 Dirty 状态。

新建失败时，当前已打开工程不得被半初始化对象替换。若当前工程存在未保存修改，应先由 Desktop 提示用户处理，再执行替换。

## 3. Open Project

打开工程由 Desktop 取得文件路径，交给工作流服务调用 Infrastructure：

```text
File path
    ↓
Persistence.Load
    ↓
Version migration
    ↓
Domain restore
    ↓
Create runtime Layout
    ↓
Build Scene
    ↓
Replace current session
```

恢复完成后：

- Domain Aggregate 使用文件中的 Stable ID；
- 现有拓扑和设备状态由 Persistence/Domain 恢复；
- Rendering 根据恢复后的 Domain 和 Layout 重新生成 Scene；
- Selection、HitTest 临时状态和 Inspector 状态重新初始化；
- 新工作区从已保存状态开始，Dirty 为 false。

打开失败时，保留当前工程和当前界面状态，并通过 Desktop 错误反馈告知用户。不得先清空当前工作区再尝试恢复，以免失败后丢失可继续工作的状态。

## 4. Save Project

保存流程由 Desktop 触发，Application/工作流服务协调 Domain、Layout 和 Infrastructure：

```text
Current session
    ↓
Prepare transient edits
    ↓
Project Domain + Layout snapshot
    ↓
Persistence.Save
    ↓
Mark saved
    ↓
Refresh workflow status
```

保存内容包括：

- 当前 Domain 工程对象及其结构事实；
- 当前需要持久化的 Layout；
- 工程元数据。

不保存：

- 当前 Selection；
- HitTest 临时结果；
- 拖拽 Preview；
- Undo/Redo 历史，除非未来建立独立版本化合同。

保存成功后，工作流更新当前文件路径和保存点，并清除 Dirty 状态。保存失败时保留当前 Domain、Layout 和 Dirty 状态，不将失败结果视为已保存。

## 5. Dirty State

Dirty 表示当前工作区与最近一次成功保存状态存在差异。Dirty 来源包括：

- Domain Command 执行、Undo 或 Redo；
- Layout Move 等可持久化图形变更；
- 未来纳入工程文件的属性编辑。

以下临时交互状态不应单独产生 Dirty：

- Selection 变化；
- HitTest 结果；
- 尚未提交的拖拽 Preview；
- Inspector 只读刷新。

建议由工作区会话统一维护 Dirty 判断：CommandStack 保存点记录 Domain/布局编辑状态，成功 Save 后更新保存点。若未来存在多个状态源，应由会话聚合，而不是由各个 ViewModel 分别判断。

状态转换如下：

```text
New/Open successful → Clean
Edit command         → Dirty
Undo to saved state  → Clean
Save successful      → Clean
Save failure         → Dirty remains
```

## 6. Desktop/Application 边界

Desktop 负责：

- 收集菜单、对话框和窗口输入；
- 请求文件路径和用户确认；
- 显示加载、保存、失败和 Dirty 状态；
- 在工作区替换后刷新 Canvas、Selection 和 Inspector。

Application/工作流服务负责：

- 创建和持有当前运行时会话；
- 调用 Persistence 服务完成 Load/Save；
- 组织 Migration、Domain Restore、Layout Restore 和 Scene Build 的顺序；
- 维护会话的一致性和保存点。

Infrastructure 负责：

- 工程文件读写；
- DTO 映射；
- 历史格式迁移。

Desktop 不直接操作 DTO、JSON、ZIP 或 Domain 集合；Infrastructure 不感知 WPF 控件；Rendering 只根据当前会话状态投影 Scene。

## 7. Scene 与工作区替换

New/Open 成功后，工作区应以新的会话作为一致性边界：

1. 准备 Domain 和 Layout；
2. 构建新的 Scene；
3. 初始化 Selection 和 Inspector；
4. 一次性替换当前会话引用；
5. 通知 MainWindow 刷新 Canvas 和状态栏。

替换过程中不应让 Canvas 短暂引用旧 Domain、新 Layout 的混合状态。旧会话中的 Selection、Preview 和命令历史不得泄漏到新工程。

## 8. 错误与未保存修改

当当前工程 Dirty 时，New、Open 或关闭窗口应先请求用户确认。取消操作保持当前会话不变；确认放弃后才允许替换或关闭。

Persistence Load/Save、Migration 或 Restore 失败时：

- 不修改当前有效会话；
- 不清除 Dirty；
- 通过 Desktop 显示可理解的错误；
- 将详细异常留在工作流/日志边界，不让 View 解析底层格式。

## 9. 后续 Runtime 切片

- **P0-10-C-2 Project Session Runtime**：定义运行时会话和 New/Open/Save 统一入口。
- **P0-10-C-3 Dirty State Runtime**：接入 CommandStack、Layout 编辑和保存点。
- **P0-10-C-4 Project Workflow UI**：接入菜单、确认对话框、错误反馈和状态栏。

具体切片应优先复用现有 `ProjectWorkspaceController`、`ProjectRuntimeSession` 和 Persistence 服务，避免建立第二套工程生命周期实现。

## 10. 非目标

本设计不实现：

- 最近文件列表；
- 导出、打印或外部格式转换；
- 工作票业务流程、审批和专业规则；
- 权限、登录和多人协作；
- 新的 Persistence 格式或拓扑模型。

## 11. 设计结论

Project Workflow 以运行时工作区会话为中心，保证 New/Open/Save 的原子替换、错误保留和 Dirty 一致性。Desktop 负责交互，Application/工作流服务负责生命周期编排，Infrastructure 负责文件合同，Rendering 只消费当前会话并重新生成 Scene。
