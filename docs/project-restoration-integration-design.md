# M4-B-7-A 完整工程恢复集成设计

> 文档状态：设计稿，仅定义最终工程打开集成流程，不实现代码<br>
> 编制日期：2026-08-11<br>
> 依据：`docs/project-file-design.md`、`docs/project-loading-design.md`、`docs/domain-dto-design.md`、`docs/topology-dto-design.md`、`docs/layout-dto-design.md`

## 1. 目标与范围

本设计把已有容器、Domain DTO、Topology DTO、Layout DTO、Scene 和 Editor 设计整合为一个最终工程打开事务。它定义各阶段输入输出、严格依赖顺序、候选状态边界、失败回滚和打开成功后的编辑器初始化。

最终结果必须满足：

- `.kvdrawing` 在通过容器和版本检查后才读取对应版本 DTO；
- Migration 在 DTO 层完成，之后只恢复当前版本模型；
- Domain 设备和聚合先建立稳定身份，Topology 再绑定引用；
- Layout 只引用完整且已校验的 Domain；
- Scene、命中索引和 WPF Visual 全部重新生成；
- Selection、PropertyInspector 和 CommandStack 使用新会话状态初始化；
- 任一阶段失败或取消时，当前已打开工程保持原样。

本阶段只新增设计文档，不修改 Domain、Layout、Rendering、Infrastructure、Desktop 或项目结构。

## 2. 最终恢复 Pipeline

标准打开链路固定为：

```text
.kvdrawing
    ↓
Container 安全读取
    ↓
Manifest
    ↓
Version 检查与 DTO Migration
    ↓
Current Project DTO
    ↓
Domain 设备与聚合恢复
    ↓
Topology 引用绑定与完整性校验
    ↓
Layout 恢复与跨区校验
    ↓
DrawingSceneBuilder
    ↓
DrawingScene + HitTestIndex
    ↓
DrawingSceneRenderer
    ↓
候选 DrawingVisual
    ↓
候选 EditorSession
    ↓
UI 原子提交
```

用户要求中的逻辑链路可归纳为：

```text
.kvdrawing → Manifest → Version → Domain → Topology
           → Layout → Scene → EditorSession
```

其中 Rendering 是 Scene 到候选 Visual 的必要技术阶段。Visual 不进入工程文件，也不是 Domain、Topology 或 Layout 的事实源。

## 3. 集成组件与职责

### 3.1 ProjectOpenCoordinator

`ProjectOpenCoordinator` 是 Application 层的唯一打开工程编排入口，负责：

- 按固定顺序调用各阶段服务；
- 持有候选恢复上下文；
- 传递取消请求和阶段化诊断；
- 确保候选对象不提前进入当前 UI；
- 在 UI 线程协调候选会话和 Visual 的最终交换；
- 成功后发布一次“工程已打开”通知；
- 失败时释放候选资源并保留旧会话。

它不解析具体 DTO 字段，不直接创建 Domain 对象，不绘制 Symbol，也不把修复逻辑放入 Desktop。

### 3.2 分层服务

```text
ProjectFileContainer       文件与 ZIP 安全边界
ProjectVersionDispatcher   格式选择与 DTO Migration
DomainRehydrator           文档根、设备和聚合恢复
TopologyRehydrator         Node、Terminal、关系和连接恢复
LayoutRehydrator           毫米布局与 Domain ID 关联
DrawingSceneBuilder        Domain + Layout → Scene + HitTestIndex
DrawingSceneRenderer       Scene → WPF DrawingVisual
EditorSessionFactory       组装全新的编辑器会话状态
EditorSessionHost          原子替换当前会话
```

各服务只接收上一阶段的已验证输出。禁止让后续服务回读 ZIP、原始 JSON 或旧 EditorSession 来补数据。

## 4. 各恢复阶段输入与输出

| 阶段 | 输入 | 成功输出 | 不得输出或修改 |
| --- | --- | --- | --- |
| Container | 文件路径、读取限制 | `ProjectArchiveSnapshot`：Manifest 原始数据、主条目字节、摘要信息 | Domain、Layout、当前会话 |
| Manifest | Manifest 原始数据 | 已校验 `ProjectManifestHeader` | Current DTO、应用对象 |
| Version | Header、主条目字节、迁移注册表 | `CurrentProjectDto`、原格式版本、迁移信息 | Domain、Layout、源文件 |
| Domain | CurrentDomainDto、Metadata | 候选 `DrawingDocument` 基础对象、`DomainIdCatalog`、待绑定 Topology DTO | Layout、Scene、当前 UI |
| Topology | 候选 Domain、ID Catalog、Topology DTO | 完整候选 `DrawingDocument`、只读 `DomainObjectResolver` | Layout、Rendering 状态 |
| Layout | CurrentLayoutDto、Domain Resolver | `LayoutSnapshot`、`LayoutKeyIndex` | Domain 修改、DIP、Visual |
| Scene | 完整 Domain、LayoutSnapshot、SymbolLibrary | `DrawingScene`、`SelectionHitTestIndex` | WPF 视觉状态、业务修改 |
| Rendering | DrawingScene、只读 RenderContext | 候选 `DrawingVisual` | Domain、Layout、Selection |
| Session | 前述全部候选输出、Metadata、路径 | 候选 `EditorSession` | 当前会话替换 |
| Commit | 候选 EditorSession、候选 Visual | 新的当前工程 | 源文件修改、部分提交 |

任何阶段失败都不能返回“部分成功”对象供下一阶段使用。诊断信息可以保留，但候选业务状态必须整体丢弃。

## 5. Container、Manifest 与 Version

### 5.1 Container 输出边界

Container 负责：

- 只读打开文件；
- 校验 ZIP 可解析性、条目白名单、重复路径和危险路径；
- 执行条目数量、解压大小、JSON 深度等资源限制；
- 读取 Manifest 和 Manifest 指定的主条目；
- 校验声明的摘要。

Container 不按当前 DTO 类型直接猜测 document.json，也不调用 Mapper 或 Rehydrator。

### 5.2 Manifest 检查

最小 Header 至少包含：

- `formatId`；
- `formatVersion`；
- `minimumReaderVersion`；
- `projectId`；
- `mainEntry`；
- 主条目摘要。

Manifest 检查失败时立即终止。不得继续按当前 DTO 尝试读取未知格式。

### 5.3 VersionDispatcher 输出

VersionDispatcher 根据 Header 选择准确的版本 DTO Reader：

```text
ProjectDtoVn
    ↓ VnToVn+1Migration
ProjectDtoVn+1
    ↓ ...
CurrentProjectDto
```

输出同时携带：

- `OriginalFormatVersion`；
- `CurrentFormatVersion`；
- `WasMigrated`；
- `NeedsFormatUpgrade`；
- Migration 诊断记录。

Migration 只转换 DTO，并在一个 Project DTO 内同时处理 Metadata、Domain、Topology 和 Layout 引用。禁止先恢复 Domain，再迁移 Layout；禁止在打开时自动覆盖源文件。

## 6. Domain 与 Topology 的依赖顺序

### 6.1 为什么拆成两个阶段

Topology 属于 Domain 事实，但它依赖 Device、Interval、Terminal 所有者等稳定身份。为避免占位对象和无序绑定，恢复过程分为：

```text
Domain 身份和聚合建立
        ↓
Topology 引用绑定
        ↓
完整 DrawingDocument 校验
```

两个阶段共同产生一个候选 `DrawingDocument`，不能把 Domain 基础阶段的半成品发布给 Editor 或 Layout。

### 6.2 Domain 阶段

输入：

- CurrentDomainDto；
- 已校验 Metadata；
- 空的候选恢复上下文。

处理顺序：

1. 校验 Manifest、Metadata、Domain 的工程 ID 和标题一致；
2. 扫描全部对象 ID，建立工程级 `DomainIdCatalog`；
3. 创建候选 `DrawingDocument`；
4. 通过专用恢复入口恢复完整 RingCabinet 聚合；
5. 创建 Pole、CableTermination 和其他当前支持的顶层 Device；
6. 注册 Device、Interval、SwitchDevice、SwitchAssembly 及环网柜内部 Node、Terminal；
7. 保留尚未绑定的顶层 Node、Terminal、PoleAttachment、Connection 和 OverheadLine DTO。

输出：

- 未发布的候选 DrawingDocument；
- 类型化 ID Catalog；
- 已构造对象索引；
- 待绑定 Topology DTO 集合。

### 6.3 Topology 阶段

输入：

- Domain 阶段候选对象；
- ID Catalog；
- Topology DTO。

固定恢复顺序：

```text
非环网柜 ElectricalNode
    ↓
非环网柜 Terminal
    ↓
PoleAttachment
    ↓
Connection
    ↓
OverheadLine 明细
    ↓
工程级拓扑校验
```

该顺序保证：

- Terminal 创建前 Owner 和 ElectricalNode 已存在；
- Connection 创建前两个外部 Terminal 已注册；
- OverheadLine 创建前 Connection、Pole 和必要的 PoleAttachment 已存在；
- Node 的 Terminal 反向索引由 Domain 添加入口重建；
- CableTermination 两侧固定导通只通过内部 ElectricalNode 表达；
- RingCabinet 内部固定拓扑不重复恢复。

输出是完整且通过校验的候选 DrawingDocument，以及只读 `DomainObjectResolver`。Resolver 按稳定 ID 和明确类型解析，不允许名称、杆号、数组位置或坐标回退。

### 6.4 Domain/Topology 完成条件

进入 Layout 前必须同时满足：

- 工程级 ID 唯一；
- 所有 Owner、Parent、Member 和端点引用存在且类型正确；
- Terminal 与 ElectricalNode 正反向关系一致；
- 不存在孤立 Node；
- Connection 端点、允许类型、电压和连接容量有效；
- OverheadLine 与 Connection 为一对一关系；
- PoleAttachment 和支撑杆关系有效；
- RingCabinet 聚合通过固定结构校验；
- 派生状态可以重新计算，但未写回 DTO。

## 7. Layout 恢复与跨区校验

### 7.1 输入

LayoutRehydrator 只接收：

- CurrentLayoutDto；
- 完整候选 DrawingDocument；
- 只读 DomainObjectResolver。

它不得接收半完成 DomainContext，不得创建或修改 Domain 对象。

### 7.2 恢复规则

1. 校验 `coordinateUnit = mm`；
2. 校验所有坐标和尺寸为有限数值；
3. 按稳定 Domain ID 恢复 RingCabinet、Interval、Switch、Pole、Attachment 和 OverheadLine Layout；
4. 校验父子布局所有权；
5. 建立唯一 `LayoutKeyIndex`；
6. 校验所有当前可绘制对象恰好一个 Layout；
7. 拒绝重复、缺失、孤立和类型错误 Layout；
8. 输出不可变或一致只读的 LayoutSnapshot。

工程文件保存的是毫米文档坐标。恢复 Layout 时不读取屏幕坐标、DPI、缩放、平移、HitTest 区域或 WPF 状态。

### 7.3 Domain、Topology、Layout 依赖图

```text
Domain 设备与聚合
    ├─ 提供 Device / Interval / Switch 身份
    └─ 提供环网柜内部固定拓扑
              ↓
Topology
    ├─ 绑定顶层 Node / Terminal
    ├─ 绑定 Connection / OverheadLine
    └─ 输出完整 Domain Resolver
              ↓
Layout
    ├─ 通过 Domain ID 建立布局
    └─ 不改变任何电气关系
              ↓
Scene / Rendering
```

禁止通过 Layout 坐标相交、线路端点或 Symbol 接触反推 Topology。

## 8. SceneBuilder 与 Rendering 初始化

### 8.1 文档级 Scene 输入

DrawingSceneBuilder 接收一个一致的文档级输入：

```text
DocumentSceneBuildInput
├─ DrawingDocument
├─ LayoutSnapshot
├─ SymbolLibrary
└─ SymbolRenderContext
```

`SymbolRenderContext` 只包含本次渲染需要的状态投影，例如开关机械状态和由 Domain 提供的评估结果。SceneBuilder 不计算运行状态、有效接地或联锁结果。

### 8.2 Scene 输出

SceneBuilder 一次性生成：

- 全部 SceneElement；
- 稳定绘制层级；
- 每个可选对象的 SelectionReference；
- 完整 SelectionHitTestIndex。

若任一 Domain 对象缺少 Layout、Symbol 无法解析或 SelectionReference 冲突，Scene 构建失败，不能交付部分 Scene。

当前 DrawingSceneBuilder 分别支持环网柜场景和架空系统场景。后续实现完整加载器前，需要提供文档级组合入口，支持多个 RingCabinet 与 DrawingLayout 共同生成一个 Scene；不得由 Desktop 演示入口临时拼接。

### 8.3 Rendering

DrawingSceneRenderer 只读取候选 Scene：

- Scene 保持毫米文档坐标；
- 毫米到 DIP 的转换只发生在 WPF Rendering 边界；
- 生成新的候选 DrawingVisual；
- 不复用旧 Visual、Overlay 或命中缓存；
- 不修改 Domain、Topology 或 Layout。

Visual 创建和 DrawingVisualHost 替换必须在 UI 线程执行。候选 Visual 生成失败时，旧 Visual 保持不变。

## 9. Selection 与 PropertyInspector 初始化

### 9.1 Selection

每次成功打开工程都创建新的 SelectionManager：

- `Selected = null`；
- SelectionChanged 尚未向旧 ViewModel 发布；
- 高亮 Overlay 为空；
- HitTestIndex 使用新 Scene 的索引；
- 不按相同 ID 恢复旧工程选择；
- Selection 不进入工程文件，也不影响 Dirty。

不能在候选工程提交前清空旧 Selection，因为后续阶段仍可能失败。

### 9.2 PropertyInspector

候选 PropertyInspector 数据源由以下内容创建：

- 新 DrawingDocument；
- 新 LayoutSnapshot；
- 新 SelectionHitTestIndex；
- 文档级 SelectionObjectResolver。

PropertyInspectorViewModel 在提交后执行空状态初始化：

- Selection 为空；
- 标题显示“未选择对象”；
- 属性分区为空；
- 第一次选择后才通过稳定 SelectionReference 解析对象；
- 不保留旧工程业务对象、DTO 或 Layout 引用。

当前 PropertyInspectionSource 对 RingCabinet 仍有单对象入口。完整工程加载需要文档级对象索引，以支持多个环网柜；这是后续代码实现前置条件，本阶段不修改代码。

## 10. CommandStack、SavePoint 与 Dirty

### 10.1 新会话 CommandStack

打开工程不恢复 Undo/Redo 历史，也不创建 `OpenProjectCommand`。候选 EditorSession 使用全新 CommandStack：

- `History` 为空；
- `CurrentIndex = 0`；
- `CurrentStateId = 0`；
- `CanUndo = false`；
- `CanRedo = false`；
- 调用 `MarkSaved()`；
- `SavedStateId = 0`；
- `IsDirty = false`。

### 10.2 保存点语义

加载完成状态就是新会话保存点：

- 当前版本文件：`IsDirty=false`，`NeedsFormatUpgrade=false`；
- 迁移后的旧版本文件：`IsDirty=false`，`NeedsFormatUpgrade=true`；
- 第一条成功编辑 Command 后：`IsDirty=true`；
- Undo 回到 SavedStateId 后：`IsDirty=false`；
- Selection、高亮、属性查看、缩放、Scene 重建和 Rendering 刷新不改变 Dirty。

`NeedsFormatUpgrade` 是格式兼容状态，不等同于用户编辑 Dirty。用户后续保存时才按当前版本写出，不在打开时自动修改源文件。

### 10.3 Dirty 的唯一来源

完整 EditorSession 应以 CommandStack 的 CurrentStateId/SavedStateId 关系作为编辑 Dirty 的事实源。若 ProjectSession 仍保留独立 `IsDirty` 字段，集成实现必须由同一会话协调器同步更新，不能形成两个互相矛盾的 Dirty 值。

## 11. 候选 EditorSession

候选 EditorSession 至少包含：

- 已恢复 DrawingDocument；
- LayoutSnapshot 和 LayoutKeyIndex；
- DrawingScene 和 SelectionHitTestIndex；
- 新 SelectionManager；
- 新 CommandStack；
- PropertyInspectionSource、SelectionObjectResolver 和 PropertyInspectorViewModel；
- Metadata、文件路径、OriginalFormatVersion；
- `NeedsFormatUpgrade`；
- 当前保存点状态；
- 与候选 DrawingVisual 的提交关联。

候选会话在提交前：

- 不挂接旧 ViewModel 事件；
- 不替换 Current；
- 不更新窗口标题或最近文件；
- 不清空旧 Selection 或 Inspector；
- 不发布 Dirty、Undo/Redo 或工程切换通知。

## 12. 原子提交与失败回滚

### 12.1 Prepare 阶段

在不修改当前工程的情况下完成：

1. Container、Manifest 和 Version；
2. Domain、Topology 和 Layout 恢复；
3. Scene 与 HitTestIndex 构建；
4. 候选 CommandStack、Selection 和 Inspector 初始化；
5. UI 线程创建候选 DrawingVisual；
6. 组装并最终校验候选 EditorSession。

Prepare 完成前，旧会话仍正常显示和保持原编辑状态。

### 12.2 Commit 阶段

最终提交在 UI 线程的一个逻辑事务中执行：

1. 暂停当前输入分发；
2. 保存旧 Session、Visual、Inspector 数据源和窗口状态引用；
3. 替换 EditorSessionHost.Current；
4. 替换 DrawingVisualHost 当前 Visual；
5. 连接新 Selection 与 PropertyInspector 数据源；
6. 更新标题、当前文件路径和最近文件；
7. 发布一次工程切换成功通知；
8. 恢复输入并释放旧会话资源。

若第 3—6 步不能由单一提交 API 保证，必须在同一协调器内捕获异常，并使用保存的旧引用恢复全部 UI 状态。成功事件只能在所有交换完成后发布。

### 12.3 失败回滚

任一 Prepare 阶段失败或取消时：

- 关闭候选文件流和 ZIP；
- 丢弃版本 DTO、候选 Domain、Layout、Scene、HitTestIndex、Visual 和 Session；
- 不修改当前 EditorSession；
- 不清空当前 Selection、PropertyInspector 或 CommandStack；
- 不修改当前 Dirty、SavePoint、文件路径、标题和最近文件；
- 不覆盖、迁移或修复源 `.kvdrawing`。

Commit 阶段异常时，恢复旧 Session、Visual、Inspector 数据源、标题和路径。恢复失败属于应用级严重错误，必须记录完整内部诊断，但仍不得修改源工程文件。

第一版不支持部分加载、跳过损坏对象、自动补 Layout 或打开后让用户修复。

## 13. 失败分类

| 阶段 | 典型错误 | 结果 |
| --- | --- | --- |
| File/Container | 文件不存在、ZIP 损坏、危险路径、大小超限 | 保留旧会话 |
| Manifest | 格式标识、入口或摘要错误 | 停止读取主 DTO |
| Version/Migration | 版本过新、迁移链缺失、迁移校验失败 | 拒绝打开 |
| Domain | 重复 ID、聚合非法、设备类型不匹配 | 丢弃候选 Domain |
| Topology | Terminal/Node 缺失、Connection 冲突、孤立 Node | 丢弃候选 Domain |
| Layout | 单位错误、孤立/缺失布局、外键错误 | 丢弃候选 Layout 与 Domain |
| Scene | Symbol、Layout 或 SelectionReference 无法解析 | 丢弃候选 Scene |
| Rendering | Visual 生成失败 | 保留旧 Visual 和 Session |
| Commit | UI 状态交换异常 | 恢复旧 UI 状态 |
| Cancel | 用户取消 | 安静结束，旧会话不变 |

错误结果至少包含稳定错误代码、失败阶段、用户摘要、FormatVersion，以及可用的 JSON 路径、ObjectId 或 LayoutKey。内部异常和调用栈只用于诊断。

## 14. Migration 扩展入口

### 14.1 注册方式

Migration 使用明确的相邻版本链：

```text
IProjectMigration
├─ SourceVersion
├─ TargetVersion = SourceVersion + 1
└─ Migrate(SourceProjectDto) → TargetProjectDto
```

VersionDispatcher 只接受：

- 每一版本对最多一个迁移器；
- 从源版本到当前版本的连续完整链；
- 每一步输出经过对应版本 DTO 校验；
- 最终 CurrentProjectDto 经过完整结构和跨区引用预校验。

### 14.2 Migration 边界

Migration 可以：

- 重命名 DTO 字段；
- 转换稳定枚举编码；
- 在历史合同存在确定默认值时补字段；
- 同步更新 Domain、Topology、Layout 的稳定 ID 引用；
- 将旧 Layout 中重复的 Domain 事实迁移回唯一事实源。

Migration 不可以：

- 构造运行时 Domain、Layout、Scene 或 WPF 对象；
- 调用当前 Editor Command；
- 通过坐标猜测 Terminal 或 Connection；
- 为缺失业务对象生成随机 ID；
- 静默删除未知设备或孤立布局；
- 自动保存或覆盖源文件。

### 14.3 格式升级状态

Migration 成功只表示候选内存数据可由当前版本打开：

- `NeedsFormatUpgrade=true`；
- `IsDirty=false`；
- 原文件保持原版本；
- 用户执行保存时才升级格式；
- 升级保存继续使用临时文件和原子替换方案。

## 15. 线程与取消边界

可以在后台执行：

- 文件和 ZIP 读取；
- 摘要与 JSON 解析；
- DTO Migration；
- 不依赖 WPF 的 Domain、Topology 和 Layout 恢复；
- 不访问 DispatcherObject 的 Scene 数据构建。

必须在 UI 线程执行：

- DrawingVisual 创建；
- DrawingVisualHost 替换；
- EditorSession、Selection 和 PropertyInspector 数据源交换；
- 窗口标题和当前路径更新。

取消在安全阶段边界生效。进入 Commit 后必须完成全部交换或恢复旧状态，不能留下半提交 UI。

## 16. 当前实现前置条件

进入完整工程恢复编码前，当前项目还需要补齐：

- Project DTO 中完整 Topology 与 Layout 区的实现；
- Topology Mapper、恢复器和工程级完整性校验；
- 文档级 LayoutSnapshot/LayoutStore；
- 支持多个 RingCabinet 与架空对象的文档级 DrawingSceneBuilder；
- 文档级 PropertyInspectionSource 和 SelectionObjectResolver；
- 包含 Domain、Layout、Editor 状态的正式 EditorSession；
- EditorSessionHost 与 Visual 的可回滚提交协调器；
- Manifest 的 MinimumReaderVersion、摘要和版本 Reader/Migration 分派。

这些是后续独立实现任务。本设计不通过修改现有 Domain/Layout 模型提前实现它们。

## 17. 验收标准

后续实现至少验证：

- 当前版本空工程与完整工程可以打开；
- RingCabinet、Pole、CableTermination、Terminal、Node、Connection 和 OverheadLine 的稳定 ID 保持；
- Domain → Topology → Layout 顺序不可交换；
- Layout 只通过稳定 Domain ID 恢复；
- 多环网柜与架空系统能生成统一 Scene 和 HitTestIndex；
- 打开后 Selection 和 PropertyInspector 为空，首次选择可以正确解析；
- CommandStack 为空，SavePoint 为 0，Dirty=false；
- 迁移文件打开后 NeedsFormatUpgrade=true、Dirty=false；
- 任一阶段注入失败或取消时旧 Session、Visual、Selection、Inspector、CommandStack 和 Dirty 完全不变；
- 打开后再次保存不会改变稳定 ID、Topology 或毫米 Layout 语义。

## 18. 本阶段不实现

- ProjectLoader、ProjectOpenCoordinator、Rehydrator、Mapper 或 Migration 代码；
- Domain、Topology、Layout 或工程格式修改；
- DrawingSceneBuilder、Rendering、Selection、PropertyInspector 或 EditorSession 修改；
- 打开工程 UI、进度提示、最近文件或自动恢复；
- 自动修复、部分加载、只读降级；
- Undo/Redo 历史跨会话持久化；
- WorkScope、GroundingPoint、PTInterval、DTUCabinet 或工作票数据恢复。
