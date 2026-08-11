# M4-B-4-A 工程加载与恢复架构设计

> 文档状态：设计稿，仅定义完整工程加载与恢复流程，不实现代码<br>
> 编制日期：2026-08-11<br>
> 依据：`docs/project-file-design.md`、`docs/domain-dto-design.md`、`docs/layout-dto-design.md` 与当前 Editor / Rendering 架构

## 1. 目标与范围

本设计定义 `.kvdrawing` 从文件容器到可编辑 WPF 会话的完整恢复事务，确保：

- Manifest 和版本在主数据反序列化前完成检查；
- Domain 先于 Layout 恢复；
- 所有对象及关系按稳定 ID 绑定；
- Layout 只关联已经通过校验的 Domain 对象；
- Scene、命中索引和 WPF Visual 全部重新生成；
- 任一步失败时，当前已打开工程不发生任何变化；
- 打开成功后选择为空、Undo/Redo 历史为空且 Dirty=false。

本阶段不实现加载器，不修改 Domain、Layout、Rendering、Editor 或项目结构。

## 2. 总体加载链路

标准加载顺序固定为：

```text
.kvdrawing
    ↓
ZIP 容器安全检查
    ↓
manifest.json
    ↓
FormatId / Version / MinimumReaderVersion 检查
    ↓
document.json 原始字节与摘要检查
    ↓
对应版本 Project DTO
    ↓
逐版本 Migration → Current Project DTO
    ↓
Domain DTO 结构与 ID 预校验
    ↓
Domain 聚合恢复 + 引用绑定 + 全量校验
    ↓
Layout DTO 结构与坐标预校验
    ↓
按 Domain ID 恢复 Layout + 跨区校验
    ↓
构建候选 EditorSession
    ↓
DrawingSceneBuilder 重建 Scene / HitTestIndex
    ↓
DrawingSceneRenderer 生成候选 DrawingVisual
    ↓
原子替换当前会话和 Visual
```

不得交换以下步骤：

- 未检查 Manifest 版本前，不按当前 DTO 读取 document.json。
- 未完成 DTO Migration 前，不构造当前 Domain。
- 未完成 Domain 恢复和校验前，不恢复 Layout。
- 未完成 Domain + Layout 跨区校验前，不构建正式 Scene。
- 候选会话和 Visual 未全部成功前，不替换当前工程。

## 3. 分层组件职责

### 3.1 ProjectFileContainer

负责文件和 ZIP 层：

- 打开只读文件流；
- 校验 ZIP 条目白名单、重复路径、危险路径和大小限制；
- 读取 Manifest 和主数据原始字节；
- 校验 Manifest 声明的摘要；
- 不构造 Domain、Layout、Scene 或 Editor 对象。

当前 M4-B-1 `ProjectFileContainer.Open` 只支持当前精确版本并返回 Manifest + Metadata，适合作为基础容器原型。完整加载实现需要在其上增加“先读 Manifest、再按版本读取主 DTO”的编排能力，不能把 Domain 恢复逻辑塞入容器类。

### 3.2 ProjectVersionDispatcher

负责：

- 检查 `formatId`；
- 根据 `formatVersion` 选择对应 DTO Reader；
- 检查 `minimumReaderVersion`；
- 执行连续、无跳级的 DTO Migration；
- 输出唯一的 CurrentProjectDto。

版本分派器不调用 Domain 构造器，也不访问 WPF。

### 3.3 DomainRehydrator

负责从 CurrentDomainDto 构造完整 `DrawingDocument`：

- 建立 ID 注册表；
- 恢复聚合和顶层设备；
- 绑定 Terminal、ElectricalNode、Connection 及其他关系；
- 执行 Domain 全量完整性校验；
- 输出不可见于当前 UI 的候选 DrawingDocument。

### 3.4 LayoutRehydrator

负责：

- 校验固定毫米单位和数值；
- 使用已经恢复的 Domain ID Resolver 解析每个 LayoutKey；
- 构造 Runtime Layout；
- 校验缺失、重复和孤立 Layout；
- 输出候选 LayoutSnapshot。

### 3.5 ProjectOpenCoordinator

Application 层的打开用例负责整个事务：

- 协调 Container、Version、Domain、Layout 和 Scene；
- 管理取消、进度和错误归类；
- 创建候选 EditorSession；
- 在 UI 线程完成候选 Visual 和会话的原子交换；
- 成功后更新当前文件路径、最近文件和标题；
- 失败时保留旧会话。

Desktop View 不直接调用 DTO Mapper、Domain 构造器或 ZIP API。

## 4. 容器与 Manifest 加载

### 4.1 文件打开边界

打开使用只读共享策略，不修改源文件。加载期间：

- 不创建覆盖源文件的临时文件；
- 不因旧版本 Migration 自动保存；
- 不把文件路径写入当前会话，直到整个事务成功；
- 文件选择取消视为正常取消，不显示“文件损坏”。

### 4.2 容器安全检查

读取 JSON 前至少检查：

- 文件存在且是可读取的普通文件；
- ZIP 结构可解析；
- 条目名称在白名单内；
- 不存在绝对路径、`..`、反斜杠逃逸或重复条目；
- 条目数量、单条目解压大小、总解压大小和 JSON 深度未超过冻结限制；
- `manifest.json` 和 Manifest 指定的主入口存在且唯一；
- 主入口摘要与 Manifest 一致。

容器错误只报告文件级问题，不尝试从 preview.jpg 或部分 JSON 恢复 Domain。

### 4.3 Manifest 最小读取

Manifest 使用独立、最小且向前可诊断的 Header 合同读取，至少取得：

- `formatId`；
- `formatVersion`；
- `minimumReaderVersion`；
- `projectId` / `documentId`；
- `mainEntry`；
- 条目摘要。

读取 Manifest 不等于接受该版本。只有 VersionDispatcher 确认可读后，才能选择 document.json 的 DTO 类型。

当前基础 Manifest 尚未实现 `minimumReaderVersion` 和摘要；它们属于进入完整加载实现前的容器合同补充项，本阶段不修改代码。

## 5. 版本检查与 Migration

### 5.1 处理矩阵

| 条件 | 处理 |
| --- | --- |
| FormatId 不匹配 | 拒绝，错误为 UnsupportedFormat |
| FormatVersion 高于当前支持版本 | 拒绝，不按当前 DTO 尝试读取 |
| MinimumReaderVersion 高于当前 Reader | 拒绝 |
| 版本低于最早支持版本 | 拒绝，并提示使用中间版本转换 |
| 版本存在完整迁移链 | 读取该版本 DTO 并逐级迁移 |
| 迁移链缺失或迁移校验失败 | 拒绝，原文件不变 |
| 当前版本 | 直接进入 Current DTO 校验 |

应用版本号不参与格式兼容判断，只用于诊断。

### 5.2 Migration 顺序

```text
ProjectDtoV1
    ↓ V1ToV2Migration
ProjectDtoV2
    ↓ V2ToV3Migration
CurrentProjectDto
```

每一级 Migration 同时处理该版本的 Domain DTO、Layout DTO 和 Metadata，保持跨区 ID 引用一致。禁止先把旧 Domain 恢复为运行时对象，再迁移 Layout。

迁移后重新执行 Current DTO 的全部结构、ID 和引用预校验，不能假定迁移输出天然有效。

### 5.3 打开旧版本后的状态

成功迁移只发生在内存中：

- 原 `.kvdrawing` 文件保持不变；
- CommandStack 仍从空历史和保存点开始，`IsDirty=false`；
- EditorSession 另设 `NeedsFormatUpgrade=true`，不把格式升级混同为用户编辑；
- 用户后续保存时明确升级到当前格式，并遵守备份或另存为策略。

## 6. Current Project DTO 预校验

在创建任何 Domain 对象前完成：

- documentId 与 Manifest projectId 一致；
- Metadata、Domain 和 Layout 区存在；
- 必填字段、稳定枚举、字符串和数值有效；
- 全部对象 ID 非空；
- 工程级 ID 注册表无重复；
- DTO 类型判别器与字段结构匹配；
- 所有引用目标 ID 至少在正确 ID 类别中存在；
- 当前未支持的 PT、PTInterval、DTU 等类型被拒绝；
- Layout 坐标单位严格为 `mm`。

预校验只证明 DTO 结构可进入恢复阶段，不替代 Domain 构造器和聚合校验。

## 7. Domain 恢复

### 7.1 临时恢复上下文

DomainRehydrator 使用独立上下文保存：

- CurrentDomainDto；
- 全局 ObjectIdRegistry；
- 已构造对象的类型化索引；
- 待绑定引用；
- 分阶段错误列表；
- 候选 DrawingDocument。

上下文不能引用当前 EditorSession 的对象，也不能把候选对象提前发布给 Selection、PropertyInspector 或 Rendering。

### 7.2 DrawingDocument 根

首先使用 DTO 中原始 `documentId` 和标题创建候选 `DrawingDocument`。Manifest、Metadata 与 Domain 中重复出现的工程 ID 或标题必须一致；冲突时失败，不自动选择某一来源。

### 7.3 RingCabinet 聚合恢复

每个 RingCabinet 必须通过 Domain 专用完整恢复入口一次性构造：

1. 读取 Cabinet、MainBusNode、全部 Interval、内部 SwitchDevice、SwitchAssembly、ElectricalNode 和 Terminal DTO。
2. 校验所有内部 ID 在工程级唯一，并属于当前柜体作用域。
3. 按 DTO 原始 ID 创建内部对象，不调用普通 `Create` 生成新 ID。
4. 根据 IntervalKind 和 GroundingStructureKind 恢复固定端子—节点拓扑。
5. 根据 AssemblyType、RuleSetRef 和成员角色重建 Domain 内置联锁规则。
6. 恢复各 SwitchState，不保存或反写 OperationalState。
7. 重建 ElectricalNode 的 Terminal 反向索引。
8. 执行 RingCabinet 完整结构、拓扑、顺序和硬联锁校验。
9. 成功后把完整 RingCabinet 添加到候选 DrawingDocument。

任何 Interval 恢复失败都使整个 Cabinet 和工程失败，不能丢弃单个 Interval 后继续。

### 7.4 顶层 Device 创建

对非环网柜设备使用类型明确的 Domain 创建/恢复入口：

- Pole：恢复 ID、杆号、杆型、名称和架空锚点 Terminal ID 声明；
- 柱上 SwitchDevice：恢复 ID、SwitchKind、InstallationType、TerminalIds、SwitchState 和调度编号；
- CableTermination：恢复 ID、两侧 Terminal ID、InternalNodeId、名称和电压等级。

不创建通用 Device 代替具体设备，不把未知 DeviceKind 降级为基础类型。柱上 SwitchDevice 当前需要新增 Domain 类型安全恢复入口，这是后续实现前置条件，本阶段不修改 Domain。

### 7.5 ElectricalNode 恢复

RingCabinet 内部节点由聚合恢复工厂处理。顶层节点在其 Owner Device 已加入候选文档后恢复：

- 保持原始 NodeId、NodeType、OwnerType、OwnerId 和人工 ElectricalState；
- Owner 必须存在且类型正确；
- Earth 节点的 ElectricalState 必须为空；
- 此时不从 Connection 或开关状态推导节点带电状态。

### 7.6 Terminal 恢复与引用绑定

RingCabinet 内部端子由聚合恢复工厂处理。顶层 Terminal 在 Device 和 ElectricalNode 均存在后恢复：

1. 按 TerminalId 创建 Terminal。
2. 校验 OwnerType / OwnerId。
3. 校验所属具体 Device 声明该 TerminalId。
4. 解析可选 ElectricalNodeId，并校验聚合边界。
5. 通过 DrawingDocument 的添加入口建立 Node 的 Terminal 反向索引。
6. 校验外部端子连接策略和 AllowedConnectionTypes。

不根据 Role、名称或集合位置查找端子，不为缺失 TerminalId 生成新 ID。

### 7.7 Connection 绑定

全部 Terminal 完成后再恢复 Connection：

- StartTerminalId 和 EndTerminalId 均必须存在且不同；
- 两端必须是 External Terminal；
- 两端均允许该 ConnectionType；
- 多连接数量不违反 Terminal 策略；
- ConnectionId 工程级唯一；
- 柜内固定拓扑不通过 Connection 恢复。

随后恢复关系明细：

- OverheadLine 必须与 `ConnectionType.OverheadLine` 共用 ConnectionId，并校验 SupportPoleIds、延续端子和延续事实；
- PoleAttachment 必须解析到 Pole 和允许的附属设备，且一个附属设备不能重复安装。

### 7.8 Domain 最终校验

所有对象和引用绑定后执行 DrawingDocument 全量校验，至少覆盖：

- ID 唯一性；
- Owner、Parent、Member 和端点引用；
- Device 具体类型一致性；
- Terminal—Node 固定拓扑；
- Connection 端点和占用策略；
- RingCabinet 聚合不变量；
- OverheadLine 与 Connection 一对一关系；
- PoleAttachment 所有权。

最后重新计算可派生的 CompositionKind、OperationalState、有效接地和违规结果。派生结果用于验证模型可评估，不写回 DTO；已确认硬联锁非法时加载失败。

## 8. Layout 恢复

### 8.1 恢复前提

LayoutRehydrator 只能接收已经完整通过校验的候选 DrawingDocument 和只读 DomainIdResolver。它不能创建、修改或补齐 Domain 对象。

### 8.2 Domain ID 关联

按稳定 LayoutKey 恢复：

| Layout | 必须解析到 |
| --- | --- |
| RingCabinetLayout | RingCabinet.Id |
| RingCabinetIntervalLayout | 外层 RingCabinet 的 IntervalId |
| RingCabinetSwitchLayout | 外层 Interval 的柜内 SwitchDeviceId |
| PoleLayout | Pole.Id |
| AttachmentLayout | PoleAttachment.AttachmentId |
| OverheadLineLayout | OverheadLine ConnectionId |

数组位置、名称、杆号、Sequence、坐标相交和对象引用均不得作为恢复依据。

### 8.3 构造顺序

1. 校验 `coordinateUnit = mm` 和所有数值有限。
2. 为每个 RingCabinet 先构造 SwitchLayout。
3. 使用 SwitchLayout 构造 IntervalLayout。
4. 使用 IntervalLayout 构造 RingCabinetLayout。
5. 构造 PoleLayout。
6. 构造 AttachmentLayout。
7. 构造 OverheadLineLayout；运行时 IsContinued 从 Domain 取得。
8. 建立完整 LayoutKey 索引。
9. 生成候选 LayoutSnapshot。

所有 DTO 坐标直接恢复为毫米 DocumentPoint，不经过 DIP 或屏幕坐标转换。

### 8.4 孤立 Layout

以下均为孤立 Layout，必须加载失败：

- LayoutKey 找不到 Domain 对象或关系；
- IntervalLayout 不属于外层 Cabinet；
- SwitchLayout 不属于外层 Interval；
- AttachmentLayout 指向不存在的 PoleAttachment；
- OverheadLineLayout 找不到匹配 Connection / OverheadLine；
- 同一个 LayoutKey 重复出现。

不得静默删除、重新挂接或按最近对象猜测归属。

### 8.5 缺失 Layout

第一版要求所有当前可绘制对象恰好一个 Layout。缺少以下任一布局均失败：

- RingCabinet、其每个 Interval 和每个需要绘制的内部 SwitchDevice；
- Pole；
- PoleAttachment；
- OverheadLine。

不得在加载时调用自动布局补齐。未来若支持“未放置设备”，必须通过版本化 PlacementState 明确表达。

### 8.6 Layout 最终校验

- Width、Height 大于零；
- MainBusY 位于柜体高度范围内；
- 绝对和相对坐标语义正确；
- Domain 与 Layout ID 覆盖集合完全一致；
- OverheadLine Runtime Layout 的 IsContinued 与 Domain 一致；
- Layout 不包含 Domain 状态、拓扑或设备类型副本。

## 9. Scene 与 WPF 恢复

### 9.1 DrawingSceneBuilder 衔接

SceneBuilder 只接收候选 Domain + Layout，不接收 DTO。构建顺序建议为：

1. 线路；
2. Pole 与 Attachment；
3. RingCabinet 及其 Interval / Switch；
4. 标签和文字；
5. SelectionHitTestEntry。

当前 `DrawingSceneBuilder` 分别提供单个 RingCabinet 场景和架空系统场景入口。完整工程加载需要由后续 Rendering 协调器组合多个 RingCabinet 与 DrawingLayout，或扩展 Builder 的文档级入口。组合时必须保证：

- SceneElement 层级顺序稳定；
- HitTestIndex 汇总全部对象且 SelectionReference 唯一可解析；
- 不通过合并 Scene 推导或改变电气关系；
- 任一对象缺少 Layout 时构建失败。

本阶段只记录该衔接前置条件，不修改 Rendering。

### 9.2 DrawingSceneRenderer 衔接

候选 DrawingScene 构建完成后，在 UI 线程调用 DrawingSceneRenderer：

- Scene 中仍使用毫米文档坐标；
- Renderer 在最终 WPF 边界转换为 DIP；
- 生成新的候选 DrawingVisual；
- 只有 Render 成功后才替换 DrawingVisualHost 当前 Visual；
- 不从旧 Visual 复制状态。

字体或 WPF 渲染异常视为打开失败，旧会话和旧 Visual 保持不变。

## 10. Selection 与 PropertyInspector 衔接

### 10.1 Selection

Selection 不从工程文件恢复。候选会话创建新的 SelectionManager：

- `Selected = null`；
- 不复用旧 SelectionReference；
- 不尝试用相同 ID 自动选中新工程对象；
- 高亮 Overlay 为空；
- HitTestIndex 来自新 Scene。

在原子提交之前不能先调用旧 SelectionManager.Clear，否则后续加载失败会改变旧会话 UI。

### 10.2 PropertyInspector

候选 PropertyInspectionSource 由恢复后的 Domain、Layout 和新 HitTestIndex 创建。原子提交后：

- SelectionObjectResolver 切换到新数据源；
- PropertyInspectorViewModel 显示空选择状态；
- 第一次点击后按 SelectionReference 的稳定 ID 解析对象；
- UI 不保留旧工程业务对象引用；
- Inspector 不读取 DTO，也不保存恢复状态。

当前 PropertyInspectionSource 对 RingCabinet 使用单对象字段。支持完整多柜工程前，需要改为文档级集合或索引源；这是后续实现前置条件，本阶段不修改代码。

## 11. 候选 EditorSession 与原子提交

### 11.1 候选会话内容

候选 EditorSession 至少包含：

- 恢复后的 DrawingDocument；
- 完整 LayoutSnapshot / LayoutStore；
- 新 CommandStack；
- 新 SelectionManager；
- PropertyInspectionSource / Resolver；
- DrawingScene 和 HitTestIndex；
- 候选 DrawingVisual；
- Metadata、原文件路径和打开时格式版本；
- `NeedsFormatUpgrade` 状态。

候选会话在提交前不发布事件，也不连接旧 ViewModel。

### 11.2 原子提交点

在 UI 线程一次逻辑事务中：

1. 暂停当前输入分发。
2. 验证候选会话仍完整且未取消。
3. 替换当前 EditorSession 引用。
4. 替换 DrawingVisualHost 的 Visual。
5. 更新 PropertyInspector 数据源。
6. 更新窗口标题、当前文件路径和最近文件。
7. 重新启用输入。
8. 释放旧会话可释放资源。

如果第 3—6 步的 UI 交换不能由单一 API 保证原子性，后续实现应先保存旧引用，在异常时同步恢复旧 Session、Visual 和数据源。成功通知只能在所有交换完成后发布。

## 12. 打开后的编辑状态

### 12.1 CommandStack

打开工程创建全新的 CommandStack：

- `History` 为空；
- `CurrentIndex = 0`；
- `CanUndo = false`；
- `CanRedo = false`；
- `CurrentStateId = 0`；
- 调用 `MarkSaved()` 建立明确保存点；
- `SavedStateId = 0`；
- `IsDirty = false`。

不复用或清空旧 CommandStack，也不把文件内容恢复成一条“OpenProjectCommand”。

### 12.2 Dirty 与保存点

- 当前版本文件打开成功：IsDirty=false，NeedsFormatUpgrade=false。
- 旧版本迁移打开成功：IsDirty=false，NeedsFormatUpgrade=true。
- 用户执行第一条成功 Command 后：IsDirty=true。
- Undo 回到打开状态：CurrentStateId 与 SavedStateId 相同时 IsDirty=false。
- 选择、高亮、缩放、滚动和属性查看不改变 Dirty。
- Scene 重建和 Renderer 刷新不改变 Dirty。

当前文件路径只在原子提交成功后写入会话。打开失败时，旧工程的路径、Dirty 和保存点全部保持原值。

## 13. 失败处理

### 13.1 错误分类

| 阶段 | 示例错误 | 结果 |
| --- | --- | --- |
| File | 不存在、无权限、读取中断 | 保留旧会话 |
| Container | 非 ZIP、危险路径、重复条目、大小超限 | 拒绝打开 |
| Manifest | 缺失、FormatId 错误、摘要不匹配 | 拒绝打开 |
| Version | 过新、过旧、迁移链缺失 | 拒绝并显示兼容说明 |
| DTO | JSON 错误、必填字段缺失、未知枚举 | 拒绝打开 |
| Identity | 重复 ID、空 ID | 拒绝打开 |
| Domain | 聚合非法、硬联锁非法、所有权错误 | 拒绝打开 |
| Reference | Terminal、Node、Connection 或 Parent 引用缺失 | 拒绝打开 |
| Layout | 孤立、缺失、重复、坐标非法 | 拒绝打开 |
| Scene | Builder 无法解析对象或布局 | 拒绝打开 |
| Render | WPF Visual 生成失败 | 保留旧 Visual 和会话 |
| Cancel | 用户取消或取消令牌触发 | 安静结束，不报损坏 |

### 13.2 错误信息

错误结果至少包含：

- 稳定错误代码；
- 失败阶段；
- 用户可读摘要；
- 文件路径；
- FormatVersion；
- 可选 JSON 路径；
- 可选 ObjectId / LayoutKey；
- 内部诊断异常，但不直接向用户暴露调用栈。

多个同层 DTO 错误可以汇总显示；发生容器、版本或安全错误时立即停止，不继续解析不可信主数据。

### 13.3 原子失败回滚

失败或取消时：

- 关闭候选文件流和 ZIP；
- 丢弃 Current DTO、候选 Domain、Layout、Scene 和 Visual；
- 不修改当前 EditorSession；
- 不清空当前 Selection、PropertyInspector 或 CommandStack；
- 不修改当前文件路径、最近文件和窗口标题；
- 不标记当前工程 Dirty 或 Saved；
- 不覆盖、迁移或修复源文件。

“部分加载后让用户自行修复”不属于第一版策略。

## 14. 线程与取消边界

可在后台线程执行：

- 文件和 ZIP 读取；
- 摘要计算；
- DTO 解析与 Migration；
- 不依赖 WPF 的 Domain / Layout 恢复和校验；
- DrawingScene 数据构建，前提是 Builder 不访问 DispatcherObject。

必须在 UI 线程执行：

- DrawingVisual 创建及 DrawingSceneRenderer 调用；
- DrawingVisualHost 替换；
- ViewModel、Selection 和 PropertyInspector 数据源交换；
- 窗口标题更新。

取消只在阶段边界或安全检查点生效。进入最终 UI 提交后不应留下半交换状态；要么完成交换，要么恢复旧会话。

## 15. 当前实现差距与后续任务边界

完整加载实现前需要分别补充：

- 容器的 Manifest Header 读取、摘要、大小限制和版本 DTO 分派能力；
- Current Project DTO、Domain DTO、Layout DTO 和 Migration 代码；
- RingCabinet 与柱上 SwitchDevice 的 Domain 恢复入口；
- DrawingDocument 全量校验入口；
- 可容纳多个 RingCabinetLayout 的文档级 Layout 根；
- 文档级 DrawingSceneBuilder / Scene 组合入口；
- 支持多 RingCabinet 的 PropertyInspectionSource 和 Resolver；
- EditorSession 及其原子替换协调器。

这些差距应按独立里程碑实现，不应通过 Desktop 演示代码直接拼装工程恢复。

## 16. 验收建议

后续实现至少验证：

- 当前版本空工程和完整工程均能打开；
- 普通、融合、混合 RingCabinet 的全部内部 ID 与状态保持；
- Terminal、Node、Connection、OverheadLine 和 Attachment 引用正确绑定；
- 六类 Layout 按稳定 Domain ID 恢复，数组乱序不影响结果；
- 缺失、重复、悬空和跨聚合引用均明确失败；
- 毫米坐标不经过 DIP 往返，Scene 重建结果一致；
- Selection 和 PropertyInspector 打开后为空，首次点击可正确解析；
- CommandStack 历史为空、保存点为 0、IsDirty=false；
- 旧版本打开后 NeedsFormatUpgrade=true，但用户编辑 Dirty=false；
- 任一 Domain、Layout、Scene 或 Render 阶段注入失败时，旧会话全部状态不变；
- 加载取消不修改旧工程；
- 未知新版本、摘要错误和压缩炸弹被安全拒绝；
- 打开后再次保存的稳定 ID 和 Domain / Layout 语义不变。

## 17. 本阶段不实现

- ProjectLoader、Rehydrator、Mapper、Migration、EditorSession 或错误类型代码；
- Domain、Layout、Rendering、Desktop 或 Infrastructure 修改；
- 文件选择、最近文件、自动恢复和打开进度 UI；
- 自动修复、部分加载、只读降级和未知类型插件加载；
- WorkScope、GroundingPoint、PTInterval、DTUCabinet 或工作票恢复；
- 云同步、数据库、多用户和 Undo/Redo 跨会话持久化。
