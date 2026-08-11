# M4-A 工程文件与持久化架构设计

> 文档状态：设计稿，仅定义工程文件体系，不实现代码<br>
> 编制日期：2026-08-11<br>
> 依据：当前 Domain、Layout、Rendering、Editor、CommandStack，以及既有架构和设备模型设计

## 1. 目标与范围

本设计定义 10kV 配电工作票附图软件的可编辑工程文件边界，使当前 Domain 与 Layout 能够完整保存、重新打开并继续编辑，同时为未来 WorkScope、GroundingPoint 和工作票业务数据预留版本化扩展位置。

持久化主链路为：

```text
EditorSession 一致状态
    ↓
Domain + Layout → 版本化 DTO
    ↓
结构、引用和业务校验
    ↓
.kvdrawing 工程容器
    ↓
读取、迁移和 DTO 校验
    ↓
按稳定 ID 恢复 Domain + Layout
    ↓
建立新的 EditorSession
    ↓
Rendering 根据恢复结果重建 Scene
```

本阶段不实现序列化代码，不修改现有 Domain、Layout、Rendering、Editor 或 CommandStack 模型。

## 2. 持久化原则

工程文件必须遵守以下原则：

- Domain 和 Layout 是可编辑工程数据的事实源。
- 工程文件使用独立、版本化 DTO，不直接反射序列化运行时对象。
- 所有业务对象和关系使用稳定 ID，保存、打开和再次保存后 ID 不变。
- Domain 与 Layout 分区保存，但加载完成前必须执行跨区引用校验。
- Rendering、选择、高亮和撤销历史等可重建状态不进入工程文件。
- 派生运行状态和有效接地结果不保存，打开后根据已保存事实重新计算。
- 文件加载不能绕过 Domain 不变量；恢复路径与新建路径可以不同，但校验强度必须一致。
- 文件格式版本与应用版本分离；迁移只作用于 DTO，不直接修改 Domain 对象。
- 保存必须使用临时文件和原子替换，失败不能破坏上一份有效工程。
- 工程文件只包含数据，不允许嵌入可执行脚本、任意 XAML 或插件代码。

## 3. 保存内容边界

### 3.1 必须保存的 Domain 数据

Domain 区至少保存当前已实现对象的稳定身份、属性、状态、所有权及相互引用：

| 对象 | 必须保存的内容 |
| --- | --- |
| DrawingDocument | DocumentId、Title |
| Device | DeviceId、稳定类型标识、DisplayName，以及适用的业务属性 |
| Pole | PoleNumber、PoleType、DisplayName、架空锚点 TerminalId 声明 |
| SwitchDevice | SwitchKind、InstallationType、SwitchState、DispatchNumber、ParentId、TerminalIds |
| CableTermination | 两侧 TerminalId、InternalNodeId、DisplayName |
| RingCabinet | CabinetId、DisplayName、MainBusNodeId、有序 Interval 聚合 |
| RingCabinetInterval | IntervalId、ParentCabinetId、Sequence、DisplayName、IntervalKind、节点和 ExternalTerminalId |
| IntegratedFeederInterval | GroundingStructureKind、IntermediateNodeId，以及三台开关事实状态 |
| SwitchAssembly | AssemblyId、AssemblyType、ParentIntervalId、成员角色引用、InterlockRule / RuleSetRef 的稳定编码 |
| ElectricalNode | NodeId、NodeType、OwnerType、OwnerId、关联 TerminalId |
| Terminal | TerminalId、OwnerType、OwnerId、Role、VoltageLevel、连接策略、ElectricalNodeId |
| Connection | ConnectionId、ConnectionType、StartTerminalId、EndTerminalId、DisplayName、VoltageLevel、人工 ElectricalState |
| OverheadLine | 与 Connection 共用的 ConnectionId、LineModel、LengthMeters、SupportPoleIds、延续事实 |
| PoleAttachment | AttachmentId、PoleId、AttachedDeviceId |

内部对象只保存一次。环网柜内部 SwitchDevice、SwitchAssembly、Terminal 和 ElectricalNode 应归入对应 RingCabinet 聚合 DTO，不再作为顶层设备副本重复保存。

### 3.2 必须保存的设备关系和电气拓扑

以下关系必须使用 ID 明确保存，不能从坐标、文字或集合位置反推：

- Device、Interval 和内部 SwitchDevice 的所有权。
- SwitchAssembly 的成员设备和角色。
- ElectricalNode 的所有者及 Terminal 归属。
- Terminal 到 ElectricalNode 的固定内部拓扑引用。
- Connection 的两个 Terminal 端点。
- OverheadLine 与 Connection 的一对一关系。
- OverheadLine.SupportPoleIds 的物理经过顺序。
- PoleAttachment 对 Pole 和附属 Device 的关联。
- RingCabinet Interval 的物理顺序及 Sequence。
- IntegratedFeederInterval 的 GroundingStructureKind。

线路交叉、图元接触或坐标重合不生成电气连接，也不能在加载时用于修复缺失端点。

### 3.3 必须保存的 Layout 数据

Layout 区保存以毫米为单位、以 Domain 或关系 ID 为键的图面实例数据：

| Layout | 关联键 | 保存内容 |
| --- | --- | --- |
| RingCabinetLayout | CabinetId | Position、Width、Height、MainBusY、LabelOffset |
| RingCabinetIntervalLayout | IntervalId | RelativePosition、Width、Height、序号和名称标签偏移 |
| RingCabinetSwitchLayout | SwitchDeviceId | RelativePosition、Width、Height、LabelOffset |
| PoleLayout | PoleId | Position、Width、Height、LabelOffset |
| AttachmentLayout | AttachmentId | Offset、Width、Height、LabelOffset |
| OverheadLineLayout | ConnectionId | Start、End、IsContinued、ContinuationOffset |

后续新增 ConnectionRoute、WorkScopeLayout、GroundingPointLayout 或 AnnotationLayout 时，仍按相同原则以稳定业务 ID 关联，不把坐标写回 Domain。

### 3.4 必须保存的 Metadata 和版本信息

Metadata 至少包含：

- DocumentId，与 Domain 根对象一致。
- Title。
- CreatedAtUtc、ModifiedAtUtc。
- 可选的文档类型、规则集版本、图元包版本和页面设置；字段未确认前可以不出现，不保存无意义空占位。
- 创建应用版本和最近保存应用版本，仅用于诊断，不作为迁移判断依据。

Version 信息至少包含：

- 固定 FormatId，例如 `distribution-drawing-project`。
- `FormatVersion`，工程数据合同版本，第一版从整数 1 开始。
- `MinimumReaderVersion`，用于快速拒绝当前应用无法安全读取的新格式。
- 容器主数据入口名称及校验摘要。

日期统一使用 ISO 8601 UTC；坐标和长度使用明确单位字段或由合同固定为毫米、米，不依赖本机区域格式。

### 3.5 明确不保存的内容

以下内容不得进入工程文件：

- SymbolDefinition、SymbolLibrary 实例、SymbolRenderContext。
- DrawingScene、SceneElement、DrawingVisual、Geometry、Brush、Pen、Transform。
- HitTestIndex、SelectionReference 当前值、SelectionManager 状态和选择高亮。
- 鼠标捕获、拖动 Armed/Preview 状态、PropertyEditor 未提交草稿和 UI 焦点。
- CommandStack.History、CurrentIndex、Undo/Redo 命令及进程内 StateId。
- 窗口大小、屏幕 DPI、当前缩放和平移；若未来需要恢复视图，应作为用户偏好另行设计。
- OperationalState、IsEffectivelyGrounded、ViolatedRuleCodes 等派生结果。
- 从 Layout 和 Symbol 可重新计算的端子显示坐标与命中边界。
- JPG、打印页面或截图作为可编辑事实；可选预览图仅用于浏览。
- 数据库连接、云端账号、本机绝对路径和临时文件位置。

## 4. 工程文件物理结构

### 4.1 文件形式

正式工程文件建议使用扩展名 `.kvdrawing` 的 ZIP 兼容容器，第一版固定包含：

```text
project.kvdrawing
├─ manifest.json
├─ document.json
└─ preview.jpg          # 可选，仅用于文件预览
```

第一版不把图元定义、字体、程序集或任意 XAML 打包进工程文件。工程只记录受支持的图元包/规则集版本引用，实际资源由已安装应用提供。

### 4.2 manifest.json

Manifest 是容器入口信息，建议结构如下：

```json
{
  "formatId": "distribution-drawing-project",
  "formatVersion": 1,
  "minimumReaderVersion": 1,
  "documentId": "00000000-0000-0000-0000-000000000000",
  "mainEntry": "document.json",
  "createdByApplication": "DistributionDrawing",
  "createdByVersion": "1.0.0",
  "savedAtUtc": "2026-08-11T00:00:00Z",
  "entries": [
    {
      "path": "document.json",
      "sha256": "..."
    }
  ]
}
```

`formatVersion` 是迁移依据；`createdByVersion` 只是诊断信息。Manifest 与 document.json 中的 DocumentId 不一致时拒绝加载，不自动选择其中一个。

### 4.3 document.json 根对象

逻辑根对象采用明确分区：

```json
{
  "documentId": "00000000-0000-0000-0000-000000000000",
  "metadata": {},
  "domain": {},
  "layout": {},
  "workTicket": null
}
```

- `documentId`：工程根稳定 ID。
- `metadata`：标题、时间、规则和页面元数据。
- `domain`：设备、聚合、关系和电气拓扑事实。
- `layout`：全部可编辑布局事实。
- `workTicket`：为未来工作票数据预留的显式可选区；V1 为 null 或省略，不能写任意未定义字段。

Version 不在 document.json 中重复保存，避免与 manifest.json 形成两个冲突来源。

## 5. Document DTO 逻辑结构

### 5.1 Metadata 区

```text
metadata
├─ title
├─ createdAtUtc
├─ modifiedAtUtc
├─ documentType?             # 后续明确枚举后启用
├─ pageSettings?             # 后续页面模型确定后启用
├─ ruleSetVersion?
├─ symbolPackageVersion?
└─ confirmationRecords?      # 后续规则确认模型确定后启用
```

`modifiedAtUtc` 只在成功保存的新文件中更新。预览、选择、Undo/Redo 浏览和失败保存不改变正式文件时间。

### 5.2 Domain 区

建议采用聚合导向结构，避免同一内部对象在多个集合重复保存：

```text
domain
├─ devices[]                 # 非 RingCabinet 顶层设备
├─ ringCabinets[]            # 完整环网柜聚合
│  ├─ cabinet
│  ├─ intervals[]            # 有序
│  │  ├─ switches[]
│  │  └─ switchAssembly
│  ├─ electricalNodes[]
│  └─ terminals[]
├─ electricalNodes[]         # 非环网柜内部节点
├─ terminals[]               # 非环网柜内部端子
├─ connections[]
├─ overheadLines[]
├─ poleAttachments[]
├─ workScopes[]              # V1 可为空；预留稳定结构
└─ groundingPoints[]         # V1 可为空；预留稳定结构
```

顶层 `devices[]` 使用稳定字符串判别类型，例如 `pole`、`switch-device`、`cable-termination`。不得使用 CLR 完整类型名、程序集名或运行时反射元数据作为文件判别器。

RingCabinet 本体只出现在 `ringCabinets[]`，不再在 `devices[]` 保存第二份。环网柜内部 SwitchDevice、SwitchAssembly、ElectricalNode 和 Terminal 也只存在于所属聚合 DTO。

### 5.3 Layout 区

```text
layout
├─ coordinateUnit = "mm"
├─ ringCabinets[]
│  └─ intervals[]
│     └─ switches[]
├─ poles[]
├─ attachments[]
├─ overheadLines[]
├─ connectionRoutes[]        # 后续启用
├─ workScopes[]              # 后续启用
├─ groundingPoints[]         # 后续启用
└─ annotations[]             # 后续明确模型后启用
```

Layout DTO 使用普通数值结构，例如 `{ "x": 50.0, "y": 65.0 }`，不序列化 WPF `Point` 或当前 `DocumentPoint` 的 CLR 类型信息。

## 6. DTO 设计规则

### 6.1 DTO 与运行时模型分离

每个工程文件版本拥有明确 DTO 合同：

- DTO 只包含可序列化基本类型、稳定枚举编码、列表和嵌套 DTO。
- DTO 不调用 Domain 行为，不继承 Device，也不引用 WPF 类型。
- Domain 私有字段、缓存和派生属性不会因代码重构自动进入文件。
- 文件字段重命名必须通过迁移处理，不依赖 C# 属性名称自动同步。
- DTO 到 Domain、Domain 到 DTO 使用显式 Mapper。

不使用 `JsonSerializer.Serialize(domainObject, domainObject.GetType())` 直接保存领域对象，也不启用任意类型多态反序列化。

### 6.2 枚举编码

DeviceType、SwitchKind、SwitchState、IntervalKind、GroundingStructureKind、ConnectionType 等使用稳定、小写连字符字符串编码，示例：

```text
load-switch
ground-switch
integrated-feeder-interval
upper-isolation-grounding
open
closed
```

编码与中文显示名称、C# 枚举成员名称分离。遇到未知编码时：

- 当前版本不认识且没有迁移器：拒绝加载并报告字段路径。
- 不得默认为第一个枚举值。
- 不得静默降级为普通 Device 或 Unclassified。

### 6.3 可选字段

- 业务上可选的值使用缺省或 null，并在合同中明确含义。
- 新增可安全缺省的字段可以由迁移器补充默认值。
- 必填 ID、状态和结构字段缺失时拒绝加载。
- 不为尚未实现的其他设备类型保存空对象或任意扩展字典。

### 6.4 确定性序列化

为便于测试、比较和摘要计算：

- JSON 固定 UTF-8，无 BOM。
- 属性顺序由 DTO 合同固定。
- 无业务顺序的集合按稳定 ID 排序。
- Interval、SupportPoleIds 等具有业务顺序的集合保留原顺序。
- Guid 使用标准 `D` 格式并统一小写。
- 数值使用 JSON number 和不依赖区域设置的格式。
- 不输出无意义默认字段和运行时哈希值。

## 7. 稳定 ID 与引用规则

### 7.1 ID 保持

以下 ID 保存后重新打开必须完全一致：

- DocumentId、DeviceId、CabinetId、IntervalId、SwitchDeviceId。
- SwitchAssemblyId、TerminalId、ElectricalNodeId、ConnectionId。
- AttachmentId，以及未来 WorkScopeId、GroundingPointId。

加载时不得根据名称、Sequence、集合索引或坐标重新生成 ID。对象显示名称和杆号允许后续编辑，但不改变引用身份。

### 7.2 全局唯一性

打开文件前建立全局 ID 索引，并至少校验：

- 同一工程内所有具有独立身份的对象 ID 唯一。
- 引用目标存在且类型正确。
- ParentId 与聚合嵌套位置一致。
- LayoutKey 在其类别中唯一，并指向正确 Domain 对象或关系。
- 同一 ConnectionId 最多对应一个 OverheadLine 明细。
- 同一 AttachedDeviceId 最多由一个 PoleAttachment 安装。

不能用“后出现覆盖前出现”的字典行为处理重复 ID。

### 7.3 当前 RingCabinet 恢复限制

当前 `RingCabinet.Create` 会为主母线、间隔、内部开关、节点、端子和 SwitchAssembly 生成新 ID，适合创建新设备，但不适合恢复已有工程。

后续持久化实现必须提供专用 Domain 重建入口，例如版本明确的 Rehydrate / Restore Factory：

- 接收 DTO 中全部原始 ID 和状态。
- 构造完整 RingCabinet 聚合，外部不能逐步拼装不完整间隔。
- 执行与新建工厂等价的结构、拓扑、联锁配置和引用校验。
- 不调用普通 Create 后再用反射替换 ID。
- 不因打开工程重新生成任何内部 ID。

该恢复入口属于后续实现任务；本阶段不修改 RingCabinet 或其他 Domain 模型。

## 8. Domain 恢复流程

### 8.1 加载阶段

加载应在独立临时上下文中完成：

```text
读取容器
  → 校验路径、大小、Manifest 和摘要
  → 解析当前版本 DTO
  → 必要时逐版本迁移 DTO
  → DTO 结构和 ID 索引校验
  → 重建 Domain 聚合
  → 重建 Layout
  → Domain / Layout 跨引用校验
  → 派生状态重新评估
  → 创建新的 EditorSession
  → Rendering 重建场景
```

任一步失败都不得部分替换当前已打开工程。

### 8.2 推荐重建顺序

为满足当前 DrawingDocument 校验依赖，建议：

1. 创建 DrawingDocument 根，恢复 DocumentId 和 Title。
2. 重建不依赖其他对象的顶层 Device：Pole、柱上 SwitchDevice、CableTermination 等。
3. 通过专用恢复工厂一次性重建 RingCabinet 完整聚合。
4. 注册非环网柜 ElectricalNode。
5. 注册非环网柜 Terminal，并连接到对应 ElectricalNode。
6. 添加 Connection，校验两端 Terminal 和连接策略。
7. 添加与 Connection 一对一的 OverheadLine 明细。
8. 添加 PoleAttachment，校验 Pole 和附属 Device。
9. 后续添加 GroundingPoint 和 WorkScope。
10. 执行 DrawingDocument 全量引用和聚合校验。

具体 API 由实现阶段根据 Domain 能力确定，但不能为了迁就 JSON 顺序而降低不变量。

### 8.3 派生信息恢复

以下内容打开后重新生成：

- RingCabinet.CompositionKind。
- OperationalState、IsEffectivelyGrounded、ViolatedRuleCodes。
- SceneElement、SymbolVisualState、HitTestIndex 和选择高亮。
- 实际端子显示坐标、标签排版和打印/JPG 场景。

重新计算结果与文件中的事实状态冲突时不存在“以文件派生值为准”的选择，因为派生值不应出现在文件中。

## 9. Layout 恢复流程

### 9.1 独立恢复

Layout DTO 在 Domain 完整恢复后构造：

- DTO 坐标转换为文档毫米值对象，不转换为屏幕像素。
- 每个 LayoutKey 必须在 Domain 或关系集合中存在。
- Position、Offset、Start、End、尺寸及标签偏移必须为有限数值。
- Width、Height 必须大于零。
- RingCabinetLayout 的 Interval 和 Switch 布局必须匹配所属聚合。
- AttachmentLayout 必须匹配现存 PoleAttachment。
- OverheadLineLayout 必须匹配 OverheadLine 的 ConnectionId。

### 9.2 缺失和多余布局

第一版对当前可绘制对象采用严格策略：

- 必须显示的对象缺少必需 Layout：加载失败并报告对象 ID。
- Layout 指向不存在对象：加载失败。
- 同一对象存在重复 Layout：加载失败。
- 不使用自动布局静默补齐缺失数据。

若未来允许“未放置设备”，必须新增明确的 PlacementState 或未放置区合同，不能把缺失 Layout 隐式解释为未放置。

## 10. 保存流程与 Editor 状态

### 10.1 一致快照

保存由 Application 用例协调，而不是由 View、Rendering 或 Domain 自行写文件：

1. 确认没有正在提交的 Command；未提交属性草稿和拖动预览不进入快照。
2. 捕获当前 Domain、Layout 和 CommandStack.CurrentStateId 对应的一致状态。
3. 映射为当前版本 DTO。
4. 执行 DTO 结构、ID、引用和业务校验。
5. 确定性序列化 document.json。
6. 生成 Manifest 和摘要。
7. 在目标文件同目录写入唯一临时文件并完成刷新。
8. 重新读取临时文件，至少验证容器、摘要和主 DTO 可解析。
9. 原子替换正式 `.kvdrawing` 文件；已有文件按恢复策略保留备份。
10. 保存成功后记录本次快照对应的 StateId。

### 10.2 Dirty 与并发编辑

- 保存开始时捕获 `snapshotStateId`。
- 文件成功落盘后，SavedStateId 应标记为该快照状态。
- 如果保存期间没有新命令，CurrentStateId 与 SavedStateId 相同，IsDirty=false。
- 如果保存期间发生新命令，当前状态不同，保存完成后仍为 Dirty。
- 保存失败不调用 MarkSaved，不清空 Undo/Redo，也不改变当前工程。

当前基础 CommandStack 只提供 `MarkSaved()`，尚不能标记任意历史 StateId。实现异步保存前需要扩展保存点 API，或在 EditorSession 内串行阻止保存期间编辑；本阶段只记录约束，不修改代码。

### 10.3 Undo 历史边界

Undo/Redo 历史暂不保存：

- 打开工程后 CommandStack 从空历史开始。
- 加载完成状态作为新的保存点，IsDirty=false。
- 工程文件只保存当前 Domain + Layout 结果，不保存达到该结果的操作过程。
- 关闭应用后不能撤销上一次会话的操作。

## 11. 版本与迁移

### 11.1 版本策略

FormatVersion 是整数、单向递增：

- V1 表示第一份冻结的工程文件合同。
- 新增、删除、重命名字段或改变字段语义时评估是否升级版本。
- C# 类重命名或内部重构若不改变文件合同，不升级 FormatVersion。
- 枚举编码语义变化必须升级版本并提供迁移。
- 容器结构变化和主入口变化必须升级版本。

### 11.2 迁移管线

迁移只在 DTO 层逐版本执行：

```text
V1 DTO
  → V1ToV2Migration
  → V2 DTO
  → V2ToV3Migration
  → 当前 DTO
  → Domain Mapper
```

禁止直接从任意旧版本跳到当前 Domain 并散布条件分支。每个迁移器必须：

- 声明输入版本和输出版本。
- 对输入 DTO 做最小必要转换。
- 保留全部现有稳定 ID。
- 同步更新所有受影响引用。
- 不根据当前图形坐标猜测电气拓扑。
- 产生可测试、确定性的输出。

### 11.3 新旧版本处理

- 低于最早支持版本：拒绝打开，并提示使用中间版本转换。
- 高于当前支持版本或 MinimumReaderVersion：只读预览也不能假定安全，默认拒绝编辑打开。
- 当前版本但包含未知必填类型或枚举：拒绝加载。
- 可安全缺省的新增字段由迁移器补齐，不在 Domain 构造器中散布文件版本判断。

已有 ID 在任何迁移中不得改变。如果旧版本确实没有某类新对象 ID，迁移器可以创建新 ID并在同一次迁移中更新全部引用；迁移后的首次保存会固化这些 ID。

### 11.4 迁移安全

- 迁移前不覆盖原文件。
- 成功打开旧版本后，在用户明确保存前保留原文件内容。
- 保存升级版本时先生成备份或使用另存为策略。
- 迁移失败应报告版本、字段路径和原因，不留下半迁移文件。

## 12. 未来专业数据兼容

### 12.1 WorkScope

未来 `domain.workScopes[]` 至少可保存：

- WorkScopeId。
- StartBoundary 和 EndBoundary。
- Description。
- 关联 GroundingPointIds。

BoundaryPoint 必须引用明确的 DeviceId、TerminalId 和已确认的侧别信息。不得使用杆号、画布坐标、线路折点或 SelectionReference 代替电气边界。

### 12.2 GroundingPoint

未来 `domain.groundingPoints[]` 至少可保存：

- GroundingPointId。
- TerminalId。
- Location / 位置说明。
- Number。
- Note。

GroundingPoint 是人工安全措施，不由 SwitchState 或拓扑自动生成。其图面偏移进入 `layout.groundingPoints[]`，电气语义位置仍以 TerminalId 为准。

### 12.3 工作票数据

`document.workTicket` 预留为独立、版本化业务区，避免把工作票编号、任务、人员、许可和确认等字段混入设备或 Layout DTO。

当前尚未形成工作票数据字段基线，因此 V1 不自行定义具体字段。后续启用时必须：

- 为 workTicket 区增加独立 SchemaVersion。
- 明确必填字段、枚举和时间语义。
- 通过 WorkScopeId、GroundingPointId、DeviceId 或 TerminalId 引用图纸对象。
- 不保存 DrawingVisual、坐标截图或自由文本 ID 来替代结构化引用。
- 与工程 FormatVersion 的迁移策略协同，但允许工作票子合同独立演进。

### 12.4 兼容策略

V1 即保留命名明确的空集合或可选区：

- `domain.workScopes`。
- `domain.groundingPoints`。
- `document.workTicket`。
- 对应的未来 Layout 集合。

保留位置只用于稳定合同结构，不允许当前应用写入未经设计的任意 JSON 扩展数据。未来字段仍需正式版本升级和迁移测试。

## 13. 文件校验与安全边界

### 13.1 分层校验

加载校验分为：

1. 容器校验：扩展名、ZIP 条目、路径、大小、重复条目、摘要。
2. JSON 校验：编码、结构、必填字段、数值范围、枚举编码。
3. ID 校验：唯一性、引用存在性、所有权和类型。
4. Domain 校验：聚合不变量、拓扑约束、开关组合结构。
5. Layout 校验：坐标、尺寸、键和父子布局。
6. 跨区校验：每个可绘制对象与 Layout、规则版本和专业引用一致。

错误应包含稳定错误代码、JSON 路径、对象 ID 和可读说明，不自动丢弃无效对象后继续打开。

### 13.2 容器安全

- 拒绝绝对路径、`..` 路径和重复 ZIP 条目。
- 限制条目数量、单条目解压大小和总解压大小，防止压缩炸弹。
- 仅读取白名单入口；未知资源不执行、不加载为 XAML。
- 校验 Manifest 声明的 SHA-256。
- preview.jpg 解析失败不影响主数据，但不得作为 Domain 恢复来源。
- JSON 设置最大深度和合理集合上限；具体值由真实工程规模测试后冻结。

## 14. 验收与测试建议

后续实现至少覆盖：

- 当前所有 Domain 类型和 Layout 类型保存—打开往返一致。
- 混合 RingCabinet 的 Interval 顺序、内部 ID、开关状态和三种 GroundingStructureKind 不变。
- Pole、PoleAttachment、CableTermination、Connection、OverheadLine 和 SupportPoleIds 引用不变。
- Terminal、ElectricalNode 和 Connection 端点恢复后通过 DrawingDocument 全量校验。
- PoleLayout 拖动和 PoleNumber 编辑后保存重开保持最终结果。
- 打开工程后 Undo 历史为空且 IsDirty=false。
- Selection、高亮、拖动预览和 PropertyEditor 草稿未写入文件。
- OperationalState、有效接地和违规结果未写入文件，打开后重新计算一致。
- 缺失 ID、重复 ID、悬空引用、未知枚举和非法布局被拒绝。
- 旧版本逐级迁移后语义和已有 ID 不变。
- 保存中断不破坏原文件；临时文件验证失败不替换正式文件。
- 未知新版本和不满足 MinimumReaderVersion 的文件被明确拒绝。
- WorkScope 和 GroundingPoint 后续加入后仍以 TerminalId 保持专业引用。

## 15. 本阶段不实现

- `.kvdrawing` 容器、JSON DTO、Mapper、迁移器或文件服务代码。
- 对 DrawingDocument、RingCabinet 恢复工厂、Layout 或 CommandStack 的修改。
- 保存、另存为、打开、最近文件、备份、自动恢复和文件选择 UI。
- 工程文件加密、数字签名、云同步、数据库和多用户协作。
- Undo/Redo 跨会话持久化。
- WorkScope、GroundingPoint 和工作票数据的代码实现。
- JPG、打印、PDF、DWG 或其他交换格式实现。
