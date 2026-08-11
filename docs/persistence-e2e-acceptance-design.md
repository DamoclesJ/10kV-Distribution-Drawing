# M4-C-4-A 持久化端到端验收方案

> 文档状态：验收设计，仅定义验证流程，不实现测试代码<br>
> 编制日期：2026-08-11<br>
> 依据：`docs/project-restoration-integration-design.md`，以及 M4-C-1、M4-C-2、M4-C-3 当前实现

## 1. 目标与范围

本方案用于验证当前 `.kvdrawing` 持久化链路能否把一个包含 Domain、Topology 和 Layout 的工程保存到文件，并在新会话中恢复为可重建 DrawingScene 的运行时状态。

验收主链路固定为：

```text
新建工程
  ↓
创建 Domain 对象
  ↓
创建 Topology
  ↓
建立并修改 Layout
  ↓
保存 .kvdrawing
  ↓
重新加载工程
  ↓
恢复 Runtime Layout
  ↓
重建 DrawingScene
  ↓
初始化 EditorSession
  ↓
逐项比较保存前后结果
```

本验收只覆盖当前已实现的工程文件、Domain/Topology DTO、Layout DTO 和运行时恢复链路。不把尚未实现的 UI 文件选择、自动保存、版本迁移、JPG、打印或工作票专业数据纳入通过条件。

## 2. 验收环境与基准数据

### 2.1 环境要求

- Windows 目标环境，能够运行当前 .NET/WPF 工程；
- 使用独立临时目录保存验收文件，不覆盖真实工程；
- 每次验收使用新生成的 ProjectId、DeviceId、TerminalId、ElectricalNodeId、ConnectionId 和 Layout 关联 ID；
- 保存完成后释放原运行时对象，再通过文件路径重新加载，禁止直接复用保存前对象；
- 验收过程记录应用版本、工程格式版本、文件路径、保存时间和失败阶段。

### 2.2 基准工程组成

基准工程应至少包含：

| 类别 | 基准对象 | 验收目的 |
| --- | --- | --- |
| 文档 | 一个 DrawingDocument | 验证 ProjectId、标题和工程根恢复 |
| 环网柜 | 一个包含普通负荷开关间隔和一二次融合间隔的 RingCabinet | 验证聚合、有序间隔、开关状态及混合柜布局 |
| 杆塔 | 两个 Pole | 验证基础 Device、杆号、杆型和位置恢复 |
| 杆塔附属关系 | 至少一个 PoleAttachment | 验证 Pole 与附属 Device 的稳定 ID 关联 |
| 电缆终端 | 一个 CableTermination | 验证两侧 Terminal 和内部 ElectricalNode |
| 架空线路 | 一个 OverheadLine 及其 Connection | 验证线路明细与外部电气连接的一对一关系 |
| Layout | 对上述对象建立完整布局 | 验证毫米坐标、尺寸、相对偏移和 Domain ID 绑定 |

基准数据应同时包含非默认值，例如自定义名称、非零坐标、非默认标签偏移、明确线路型号与长度。这样可以避免“对象创建成功但字段未实际持久化”的假阳性。

## 3. 验收执行流程

### 3.1 新建工程

操作：

1. 通过 `ProjectService.CreateProject()` 创建新的 `.kvdrawing` 工程；
2. 指定唯一 ProjectId、工程标题和可识别的 Metadata；
3. 保存初始 Manifest 与当前格式版本。

检查：

- 文件能够创建并由 `ProjectFileContainer` 再次打开；
- Manifest.ProjectId 与 DrawingDocument.Id 一致；
- FormatId 和 FormatVersion 为当前支持值；
- 新会话初始 `IsDirty=false`。

### 3.2 创建 Domain 对象

操作：

1. 创建基准 RingCabinet，并配置普通负荷开关间隔和一二次融合间隔；
2. 创建两个 Pole；
3. 创建 CableTermination；
4. 通过 DrawingDocument 的领域入口加入对象及环网柜聚合。

检查：

- 所有对象 ID 非空且工程内唯一；
- RingCabinet 的 Interval 顺序、IntervalKind、SwitchAssembly、SwitchDevice 状态和 GroundingStructureKind 符合创建数据；
- Pole 的杆号、杆型和名称正确；
- CableTermination 的电缆侧、架空侧 TerminalId 和 InternalNodeId 正确；
- 创建过程未绕过 Domain 聚合与校验入口。

### 3.3 创建 Topology

操作：

1. 注册 CableTermination 的内部 ElectricalNode 和两侧 Terminal；
2. 创建 Pole 的架空连接端点；
3. 创建 PoleAttachment；
4. 创建架空线路对应的 Connection；
5. 创建与该 Connection 共用 ConnectionId 的 OverheadLine；
6. 将全部 Topology 对象加入 DrawingDocument。

检查：

- Terminal 的 OwnerId、OwnerType 和 ElectricalNodeId 正确；
- CableTermination 两侧端子通过内部 ElectricalNode 固定导通，未使用 Connection 表达内部接线；
- Connection 的起止 TerminalId 存在且类型允许；
- OverheadLine.ConnectionId 与 Connection.Id 相同且关系唯一；
- SupportPoleIds 顺序保持；
- PoleAttachment 的 PoleId 和 AttachedDeviceId 均能解析；
- 不存在缺失 Terminal、无效 Connection 或孤立 ElectricalNode。

### 3.4 创建并修改 Layout

操作：

1. 为每个 Pole 创建 PoleLayout；
2. 为每个 PoleAttachment 创建 AttachmentLayout；
3. 为 OverheadLine 创建 OverheadLineLayout；
4. 为 RingCabinet、Interval 和内部 SwitchDevice 创建对应 Layout；
5. 至少修改一个 Pole 位置、一个 Attachment 相对偏移、一个线路端点及一个环网柜位置；
6. 将 Layout 快照交给 ProjectService 当前会话。

检查：

- 所有坐标和尺寸均使用毫米工程坐标；
- Layout 通过稳定 Domain ID 关联对象；
- Layout 数量与当前 Domain 对象数量一致；
- 不存在重复、缺失或孤立 Layout；
- Layout 中不包含 DIP、WPF Visual、Selection 或 Undo 数据；
- 修改 Layout 后工程进入 Dirty 状态。

### 3.5 保存工程

操作：

1. 调用 `ProjectService.SaveProject()`；
2. 确认 `.kvdrawing` 写入完成；
3. 记录保存后 Manifest、Metadata、Domain DTO 和 Layout DTO 的关键值；
4. 释放保存前的 Domain、Runtime Layout、Scene 和 EditorSession 引用。

检查：

- 文件可以作为合法 ZIP 容器打开；
- Manifest、Metadata、Domain 和 Layout 区域均存在；
- 文件不包含 Rendering、DrawingVisual、Selection 或 Undo 历史；
- 保存后 ProjectSession.IsDirty=false；
- 保存失败时不得把不完整候选状态当作成功结果。

### 3.6 重新加载

操作：

1. 新建 ProjectService 或清空旧应用会话；
2. 仅使用文件路径调用 `ProjectRuntimeSession.Load()` 集成入口，由其内部调用 `ProjectService.LoadProject()`；
3. 检查返回会话中的 Domain、Topology 和 Runtime Layout；
4. 确认集成入口已使用工程级 `DrawingSceneBuilder` 重建 DrawingScene。

检查：

- 加载顺序为 Manifest/Version → Domain → Topology → Layout → Scene；
- Domain 和 Topology 校验完成后才恢复 Layout；
- Runtime Layout 继续使用毫米坐标；
- RingCabinet、Pole、Attachment 和 OverheadLine 均生成 SceneElement；
- 重建 Scene 具有对应 HitTestIndex；
- 未从文件恢复 DrawingVisual、Selection 或 Undo 历史。

### 3.7 验证恢复结果

保存前建立一份只包含稳定事实的预期快照，重新加载后逐项比较。

#### 文件与会话

- ProjectId、Title、Metadata 和格式版本一致；
- 当前文件路径正确；
- 新 SelectionManager 无选中对象；
- PropertyInspector 处于未选择状态；
- CommandStack.History 为空；
- CommandStack 已建立保存点；
- ProjectSession 与 CommandStack 的 Dirty 状态均为 false。

#### Domain 与 Topology

- Device、RingCabinet、Interval、SwitchDevice、Pole 和 CableTermination 数量一致；
- 所有稳定 ID 保存前后相同；
- RingCabinet 间隔顺序、类型、接地结构和单台开关状态一致；
- ElectricalNode、Terminal、Connection、PoleAttachment 和 OverheadLine 数量一致；
- 所有跨对象引用保存前后指向相同 ID；
- CableTermination 内部拓扑、OverheadLine/Connection 一对一关系和 SupportPoleIds 顺序一致；
- 完整性校验无错误。

#### Layout 与 Scene

- 每个 Layout 的关联 ID、位置、尺寸和偏移与保存前一致；
- 坐标数值按当前 JSON 精度往返后相等；
- RuntimeLayoutDocument 中不存在孤立或缺失布局；
- Scene 能同时显示 RingCabinet、Pole、Attachment 和 OverheadLine；
- SceneElement 数量大于零，且四类对象均有对应命中区域；
- Scene 重建不修改 Domain 和 Layout。

## 4. 通过与失败判定

### 4.1 通过条件

同时满足以下条件时，本轮端到端验收通过：

- 完整执行新建、建模、建拓扑、改布局、保存、重新加载和 Scene 重建；
- 文件、Domain、Topology、Layout 和 EditorSession 检查项全部通过；
- 保存前后稳定 ID、业务属性、拓扑引用和毫米坐标一致；
- 新会话 Selection 为空、Undo 历史为空、保存点已建立且 Dirty=false；
- 没有把 Rendering 或编辑器临时状态写入工程文件。

### 4.2 失败条件

出现以下任一情况即判定失败：

- 工程文件无法重新打开或版本检查异常；
- 任一稳定 ID 被重新生成；
- 引用恢复依赖数组顺序、名称或坐标；
- Terminal、ElectricalNode、Connection 或 OverheadLine 关系丢失或错绑；
- Layout 丢失、重复、孤立或单位改变；
- Scene 无法显示任一当前支持对象；
- 加载后继承旧 Selection、Undo 历史或 Dirty 状态；
- 工程文件出现 DrawingScene、DrawingVisual、命中索引或 UI 状态。

## 5. 异常验收用例

正常链路通过后，至少补充以下负向验收：

| 用例 | 输入 | 预期结果 |
| --- | --- | --- |
| 格式不支持 | 修改为未知 FormatVersion | 拒绝加载，不创建部分会话 |
| 工程 ID 不一致 | Manifest 与 Domain/Layout 的 DocumentId 不同 | 拒绝加载 |
| Terminal 缺失 | Connection 引用不存在的 TerminalId | 拒绝拓扑恢复 |
| Connection 无效 | 两端类型或关系不合法 | 拒绝拓扑恢复 |
| 孤立 Node | ElectricalNode 无有效所有者或端子关系 | 拒绝加载 |
| Layout 缺失 | 删除一个 PoleLayout 或 IntervalLayout | 拒绝 Runtime Layout 恢复 |
| Layout 重复 | 同一 Domain ID 出现两个 Layout | 拒绝加载 |
| Layout 孤立 | Layout 引用不存在的 Domain ID | 拒绝加载 |
| 坐标非法 | NaN、Infinity 或非正尺寸 | 拒绝 Layout 校验 |
| 文件损坏 | ZIP 或 JSON 不完整 | 返回明确失败，不替换已打开工程 |

负向用例必须验证：不自动生成替代 ID、不删除错误记录继续加载、不静默采用默认坐标，也不修改原工程文件。

## 6. 当前已覆盖能力

基于 M4-C-1、M4-C-2 和 M4-C-3，当前实现已经形成以下基础链路：

- `.kvdrawing` 容器创建、保存和打开；
- Manifest、格式版本和 Metadata 读取；
- 基础 Device、Pole 和 RingCabinet 聚合的 DTO 保存与恢复；
- Connection、OverheadLine、CableTermination、Terminal、ElectricalNode 和 PoleAttachment 的保存、引用绑定与校验；
- PoleLayout、AttachmentLayout、OverheadLineLayout、RingCabinetLayout、IntervalLayout 和 SwitchLayout 的保存与恢复；
- Layout 通过稳定 Domain ID 绑定，使用毫米工程坐标；
- RuntimeLayoutDocument 重建；
- DrawingSceneBuilder 合并环网柜、杆塔、附属设备和架空线路；
- 新 ProjectRuntimeSession 初始化 SelectionManager、PropertyInspector、CommandStack、保存点和 Dirty 状态。

以上“已覆盖”表示代码路径已经存在，仍需按本方案执行自动化与 Windows 实机验收后，才能作为发布质量结论。

## 7. 当前未覆盖能力

本轮验收不要求以下能力通过：

- PTInterval、DTUCabinet 及其 Layout/DTO/Scene 恢复；
- WorkScope、BoundaryPoint、GroundingPoint 和工作票数据；
- Selection、Undo/Redo 历史、DrawingScene、DrawingVisual 的保存；
- 多对象编辑、新增/删除对象后的完整保存闭环；
- UI 文件选择、最近工程、自动保存、崩溃恢复和备份恢复；
- 跨版本 DTO Migration 和旧版本升级保存；
- 文件摘要、签名、加密或并发写入；
- JPG 导出、打印和预览图；
- 大型工程性能、超大文件资源限制和长时间稳定性；
- 非 Windows 平台运行。

这些项目不得通过放宽当前验收条件或在加载时猜测数据来替代正式实现。

## 8. 后续测试自动化方向

### 8.1 Infrastructure 集成测试

建立临时 `.kvdrawing` 文件，直接执行：

```text
CreateProject
→ 写入 Domain/Topology/Layout
→ SaveProject
→ 新 ProjectService.LoadProject
→ 深度比较稳定事实
```

测试应逐字段比较 DTO 和恢复对象，并覆盖全部负向校验。临时文件由测试独占并在测试结束后清理。

### 8.2 Round-trip 参数化测试

将以下变量参数化：

- 环网柜间隔数量与混合组合；
- 三种 GroundingStructureKind；
- 全部已确认 SwitchState 组合；
- Pole 数量、PoleAttachment 数量及 SupportPoleIds 顺序；
- OverheadLine 延续状态；
- 正数、小数、负坐标和较大毫米坐标。

每组数据执行 `Domain → DTO → JSON → DTO → Domain` 与 `Layout → DTO → JSON → DTO → Runtime Layout` 两条往返断言。

### 8.3 Rendering 集成测试

在 Windows/WPF 测试环境中加载基准工程并重建 Scene，验证：

- 四类当前支持对象均生成 SceneElement；
- HitTestIndex 能按稳定 ID 找到对应对象；
- Scene 重建不修改 Domain；
- 同一输入重复构建得到等价 Scene；
- 不要求像素级完全一致，图元视觉回归应另设受控截图基线。

### 8.4 EditorSession 状态测试

加载完成后断言：

- SelectionManager.Selected 为 null；
- PropertyInspector 为未选择状态；
- CommandStack.History 为空且不能 Undo/Redo；
- SavedStateId 与 CurrentStateId 一致；
- `IsDirty=false`；
- 首次有效编辑后 Dirty=true，执行保存并建立新保存点后恢复 false。

### 8.5 CI 与实机验收

- 非 WPF 的容器、DTO、Domain 和 Layout 校验测试应进入常规 CI；
- Runtime Layout 与 SceneBuilder 测试在 Windows runner 执行；
- WPF Visual、字体和 DPI 相关验证保留 Windows 实机检查；
- 每次工程格式升级必须保留上一正式格式的固定样本，执行向后兼容或明确拒绝测试；
- 自动化测试通过后，仍需使用脱敏真实配电图执行一次人工可视验收。

## 9. 验收记录模板

每次执行至少记录：

| 项目 | 记录内容 |
| --- | --- |
| 工程版本 | Commit ID、应用版本、FormatVersion |
| 环境 | Windows 版本、.NET 版本、测试方式 |
| 基准工程 | ProjectId、对象数量摘要、文件大小 |
| 正常链路 | 各阶段通过/失败及耗时 |
| 负向用例 | 输入、预期、实际结果 |
| 恢复比较 | ID、属性、拓扑、Layout、Scene、EditorSession |
| 遗留问题 | 缺陷编号、严重度、是否阻断发布 |
| 结论 | 通过、限制通过或不通过 |

验收报告必须区分“代码路径存在”“自动化测试通过”和“Windows 实机验收通过”，不能用其中一项替代另外两项。
