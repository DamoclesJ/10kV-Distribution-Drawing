# P0-10-A Desktop Shell Design

## 1. 目标与范围

本阶段定义桌面应用的壳层架构，为后续 Desktop Runtime 和工程工作流提供稳定边界。桌面壳层承载用户界面和交互编排，不重新定义 Domain、拓扑或持久化模型。

当前产品定位仍是具有基础电气拓扑识别能力的 10kV 工作票绘图工具。Desktop Shell 为该工具提供可操作的工作区，但不承担完整配网仿真、继保或 SCADA 职责。

## 2. Desktop 边界

Desktop 层负责：

- WPF 窗口、控件和视觉状态；
- 用户输入、命令触发和交互流程；
- ViewModel、对话框和页面导航；
- 将 Application 层的 Scene、Selection、Inspector 和 Command 能力编排到界面。

Desktop 不直接修改：

- Domain 对象；
- Electrical Connectivity Graph；
- Persistence DTO、工程文件或迁移状态。

用户操作应通过 Application Command、交互服务或明确的工作流控制器进入系统。Desktop 不复制 Domain 规则，也不在代码后置或 ViewModel 中创建隐式拓扑关系。

## 3. 分层技术架构

采用 WPF + MVVM，并保持以下依赖方向：

```text
Desktop
   ↓
Application
   ↓
Domain

Infrastructure ── provides persistence services to the application workflow
```

各层职责如下：

- **Domain**：保存设备、电气状态、端子、连接和聚合不变量；不依赖 WPF。
- **Application**：提供创建、编辑、拓扑查询、Selection、Inspector、Rendering 输入和 Command 编排；不依赖具体窗口布局。
- **Infrastructure**：提供工程文件读写、DTO 映射和版本迁移；不由 View 直接调用底层文件 API。
- **Desktop**：实现 WPF View、ViewModel、交互控制器和工作流入口，组合上述能力。

Desktop 可以依赖 Application 的公开服务和模型。Persistence 访问应通过 Application/工作流边界封装，避免 ViewModel 直接操作 `ProjectDomainDto` 或 ZIP/JSON 细节。

## 4. MainWindow 壳层

`MainWindow` 是工作区容器，不承载电气业务规则。首版布局由以下区域组成：

- **Menu**：New、Open、Save，以及后续可加入的编辑和视图命令入口；
- **Toolbox**：设备创建和交互工具入口；
- **Canvas**：显示当前 Drawing Scene，承载平移、命中和拖拽交互；
- **Inspector**：显示当前 SelectionTarget 对应的只读属性模型；
- **StatusBar**：显示工程状态、选择状态、操作提示和未保存标记。

推荐由 MainWindow ViewModel 持有各区域 ViewModel 或状态对象。区域之间通过 Selection、Command 和工作区状态协作，不通过互相访问 Domain 对象实现耦合。

## 5. Canvas 集成

Canvas 的数据和交互链路如下：

```text
DrawingDocument
      ↓
Rendering / Scene projection
      ↓
SceneElement collection
      ↓
HitTest → SelectionService
      ↓
MoveController / Command
      ↓
updated layout and scene projection
```

- **Scene**：由 Rendering 层根据当前 Domain 和 Layout 投影产生；SceneElement 只保存显示数据及 Selection 映射信息。
- **HitTest**：将鼠标位置映射为 `SelectionTarget`，不返回或保存 Domain 引用。
- **Selection**：由 Application/Interaction 层维护当前选择，并向 Inspector 和状态栏发布变化。
- **Move**：MoveController 将拖拽手势转换为可撤销的布局 Command，只改变可移动对象的 Layout，不改变 Domain 电气事实。
- **刷新**：Command 执行后由工作区重新投影 Scene，确保界面显示当前状态，而不是保存一份独立的电气状态。

Canvas 不直接创建 Device、Terminal、Connection，也不通过图形位置推断电气拓扑。

## 6. 工程生命周期

### New

New 创建一个新的 DrawingDocument 工作区，初始化必要的工程元数据、空场景和空 Selection。创建失败时不得留下半初始化的工作区。

### Open

Open 由工作流服务调用 Infrastructure 读取工程文件，完成版本迁移和 Domain Restore 后，再创建或替换当前工作区的 Scene、Selection 和 Inspector 状态。旧工程的 Stable ID 和电气拓扑由 Persistence/Domain 恢复，Desktop 不自行重建。

### Save

Save 将当前工作区的 Domain 与必要 Layout 投影交给 Persistence 服务写入工程文件。保存成功后清除 Dirty 状态；保存失败时保留当前工作区和 Dirty 状态，并将错误反馈给用户。

Save 不保存 Selection、HitTest 临时结果、Preview 或 Undo/Redo 历史，除非未来另有明确版本化设计。

## 7. 状态与交互边界

Desktop 可维护的界面状态包括：

- 当前工作区是否已加载；
- 当前 Selection；
- 当前工具和拖拽 Preview；
- Inspector 显示模型；
- Dirty、忙碌和错误提示状态。

Domain Command 的 Execute、Undo、Redo 由既有 Command 体系负责。Selection 变化不是 Domain Command，不进入 Undo 历史。Desktop 只负责触发和呈现这些状态变化。

## 8. 后续切片

- **P0-10-B Desktop Runtime**：实现 MainWindow 壳层、ViewModel 组合、Canvas/Inspector/Toolbox 基础接线和工作区状态。
- **P0-10-C Project Workflow**：实现 New/Open/Save、Dirty 状态、错误反馈和工程生命周期闭环。

后续切片仍应保持：Desktop 不直接修改 Domain，Rendering 不创建 Domain，Persistence 不由 View 直接驱动。

## 9. 非目标

本设计不实现：

- 工作票业务流程和专业审批；
- 打印、导出或外部格式发布；
- 用户权限、登录和协作；
- 完整配网仿真、潮流、继保或 SCADA；
- 自由 CAD 编辑器和任意拓扑生成。

## 10. 设计结论

Desktop Shell 是 WPF/MVVM 用户交互边界，负责把现有 Application 能力组合成可用工作区。它通过 Scene、HitTest、Selection、Move 和工作流服务连接用户操作，但不成为 Domain、Graph 或 Persistence 的替代实现。
