# M4-B-3-A Layout DTO 序列化架构设计

> 文档状态：设计稿，仅定义 Layout 持久化合同与恢复流程，不实现代码<br>
> 编制日期：2026-08-11<br>
> 依据：`docs/project-file-design.md`、`docs/domain-dto-design.md` 与当前 Layout / Scene 架构

## 1. 目标与范围

本设计定义 `.kvdrawing` 中 Layout 区的版本化 DTO、毫米文档坐标、稳定 Domain ID 关联、恢复顺序和完整性校验。

本阶段覆盖：

- `RingCabinetLayout`；
- `RingCabinetIntervalLayout`；
- `RingCabinetSwitchLayout`；
- `PoleLayout`；
- `AttachmentLayout`；
- `OverheadLineLayout`。

Layout 持久化只保存可继续编辑的工程布局事实。屏幕坐标、DIP、缩放和平移、命中区域、Scene、Symbol 及 WPF Visual 均不进入 Layout DTO。

本阶段不修改 Layout、Rendering 或其他代码，不实现 DTO、Mapper、Migration 和序列化服务。

## 2. Layout 与 DTO 的分离边界

### 2.1 职责划分

```text
Runtime Layout
  布局对象、不变量、编辑操作
        ↑                 ↓
Layout Rehydrator    Layout Snapshot Mapper
        ↑                 ↓
Current Layout DTO（纯数据合同）
        ↑
Version Migration
        ↑
document.json / layout
```

- Runtime Layout 是编辑器当前布局状态，不负责 JSON 兼容。
- Layout DTO 是版本化文件合同，只包含数值、字符串、稳定 ID 和嵌套列表。
- Mapper 显式映射所有字段，不直接序列化运行时 Layout 类型。
- Rehydrator 使用当前 Layout 公共构造和集合入口恢复对象，并执行同等级校验。
- Migration 只转换 DTO，不修改 Domain 或运行时 Layout。
- SceneBuilder 和 Rendering 只消费恢复完成的 Domain + Layout，不读取工程文件 DTO。

### 2.2 与当前物理项目的关系

当前 Layout 类位于 `DistributionDrawing.Rendering.Wpf` 项目，但其概念职责仍是工程布局。DTO 不得因此引用：

- `DocumentPoint` 的 CLR 类型信息；
- `System.Windows.Point`、`Rect`、`Transform`；
- `DrawingVisual`、`Geometry`、`Brush` 或 `Pen`；
- `SelectionReference`、HitTestIndex、Overlay 或 Command。

持久化合同应位于不依赖 WPF 的 Persistence Contracts 边界。是否在后续重构中把 Runtime Layout 移出 Rendering.Wpf，不影响本 DTO 合同。

### 2.3 不保存的派生或临时状态

明确不保存：

- 屏幕像素、WPF DIP 和显示器 DPI；
- 当前缩放比例、平移量、滚动条位置和窗口大小；
- 拖动 Armed、Preview、鼠标捕获和未提交位置；
- 选择、高亮、命中矩形、层级和 ZIndex；
- SymbolDefinition、SymbolRenderContext 和图元几何；
- SceneElement、DrawingScene 和 DrawingVisual；
- 由 Domain 或 Layout 可重建的设备端子显示锚点；
- Undo/Redo 命令与历史快照。

## 3. Layout DTO 根结构

`document.json` 的 `layout` 区建议为：

```text
LayoutDto
├─ coordinateUnit = "mm"
├─ ringCabinets[]
│  └─ intervals[]
│     └─ switches[]
├─ poles[]
├─ attachments[]
└─ overheadLines[]
```

`coordinateUnit` 在当前版本必须为固定字符串 `mm`。未知单位不允许按默认毫米解释。

集合在 JSON 中使用数组只是序列化形式，不表示通过数组位置关联 Domain。每项必须携带稳定 Domain ID，加载时按 ID 建立索引。无业务顺序的集合保存时按稳定 ID 排序，以获得确定性文件；环网柜间隔的业务顺序仍来自 Domain 的 `Sequence`，不能由 Layout 数组顺序替代。

## 4. 通用坐标 DTO

### 4.1 PointDto

所有点和偏移使用相同的纯数据结构：

```json
{
  "xMillimeters": 120.5,
  "yMillimeters": 76.0
}
```

字段必须是有限 JSON number，不允许 NaN、Infinity 或字符串数值。DTO 不记录 DPI，也不附带每个点各自的单位。

### 4.2 尺寸和局部标量

- 宽度、高度统一以 `widthMillimeters`、`heightMillimeters` 保存。
- 主母线局部位置以 `mainBusYMillimeters` 保存。
- 绝对位置、相对偏移和标签偏移虽然使用相同 PointDto，但字段名必须明确语义。
- 不使用模糊的 `x`、`y` 或 `size` 配合运行时约定猜测单位。

## 5. 各 Layout DTO 保存合同

### 5.1 RingCabinetLayoutDto

保存：

| 字段 | 坐标语义 |
| --- | --- |
| `cabinetId` | 对应 `RingCabinet.Id` |
| `position` | 页面绝对毫米坐标 |
| `widthMillimeters` | 柜体宽度 |
| `heightMillimeters` | 柜体高度 |
| `mainBusYMillimeters` | 柜体局部 Y 坐标 |
| `labelOffset` | 相对柜体原点的毫米偏移 |
| `intervals[]` | 所属间隔布局集合 |

Interval 布局嵌套在所属柜体 DTO 中，以表达布局所有权，但关联身份仍由 `intervalId` 决定。

### 5.2 RingCabinetIntervalLayoutDto

保存：

- `intervalId`；
- `relativePosition`，相对柜体原点；
- `widthMillimeters`；
- `heightMillimeters`；
- `sequenceLabelOffset`；
- `nameLabelOffset`；
- `switches[]`。

不保存 Interval 的 Sequence、名称或 IntervalKind；这些属于 Domain。恢复时根据 `intervalId` 解析所属 Interval，并验证它确实属于外层 `cabinetId`。

### 5.3 RingCabinetSwitchLayoutDto

保存：

- `switchDeviceId`；
- `relativePosition`，相对所属 Interval 原点；
- `widthMillimeters`；
- `heightMillimeters`；
- `labelOffset`。

不保存 SwitchKind、SwitchState、OperationalState 或有效接地结果。Symbol 的状态表现由恢复后的 Domain 状态通过 `SymbolRenderContext` 提供。

### 5.4 PoleLayoutDto

保存：

- `poleId`；
- `position`，页面绝对毫米坐标；
- `widthMillimeters`；
- `heightMillimeters`；
- `labelOffset`。

`poleId` 必须解析为 `DeviceType.Pole` 的 `Pole`，不能只解析为任意 Device。

### 5.5 AttachmentLayoutDto

保存：

- `attachmentId`；
- `offset`，相对所属 Pole 的毫米偏移；
- `widthMillimeters`；
- `heightMillimeters`；
- `labelOffset`。

所属 Pole 和附属 Device 不在 Layout DTO 重复保存，而是通过 Domain 中同一 `PoleAttachment.AttachmentId` 解析。移动 Pole 时 Attachment 的 Offset 保持不变。

### 5.6 OverheadLineLayoutDto

保存：

- `connectionId`；
- `start`，页面绝对毫米坐标；
- `end`，页面绝对毫米坐标；
- `continuationOffset`，延续线相对末端的毫米偏移。

`connectionId` 必须同时解析到 `ConnectionType.OverheadLine` 的 Connection 和一对一 `OverheadLine` 明细。

当前 Runtime `OverheadLineLayout.IsContinued` 与 Domain 的 `OverheadLine.IsContinued` 表达同一语义。持久化时以 Domain 为唯一事实源：

- `OverheadLineLayoutDto` 不再保存独立 `isContinued` 副本；
- 保存前若 Runtime Layout 与 Domain 的 IsContinued 不一致，拒绝保存并报告一致性错误；
- 恢复 Runtime `OverheadLineLayout` 时，从 Domain 的 `OverheadLine.IsContinued` 传入该标志；
- `continuationOffset` 仍属于 Layout，在未延续时可以保留，以便后续状态改变时沿用用户布局，但 Rendering 仅在 Domain 指示延续时使用。

这一区分避免文件中出现两个可能冲突的线路延续事实。`SupportPoleIds`、线路型号、长度和 ContinuationState 均属于 Domain，不进入 Layout DTO。

## 6. 文档坐标与 DIP 转换边界

### 6.1 唯一持久化坐标系

Layout DTO 只保存文档坐标，固定单位为毫米：

```text
Layout DTO（mm）
      ↓ 恢复
Runtime Layout / DocumentPoint（mm）
      ↓ Scene 构建
DrawingScene（mm）
      ↓ DocumentCoordinateSystem
WPF Drawing（DIP）
```

毫米到 DIP 的换算只发生在 WPF Rendering 边界。当前换算为 `96 DIP / 25.4 mm`；该比例不写入工程文件，也不受显示器物理 DPI 影响。

打开文件时不先把毫米转为 DIP 再构造 Layout；保存时也不从当前屏幕位置反算工程坐标。Mapper 直接读取 Runtime Layout 中的毫米值。

### 6.2 缩放与平移

当前缩放、平移和滚动位置是会话视图状态，不属于工程布局，因此不保存：

- 打开工程后使用应用默认视图或“适合页面”策略；
- 缩放不会改变 Layout DTO 数值；
- 拖动时先通过 ViewTransform 逆变换得到 DocumentPoint，再提交毫米坐标；
- 不允许把缩放后的 DIP 或屏幕像素写回 Layout。

若未来需要恢复用户视图，应设计独立的 `ViewPreferences`，默认作为本机用户偏好，而不是混入 Layout DTO。除非后续业务明确要求，工程文件仍不保存它。

### 6.3 绝对与相对坐标

| Layout | 保存语义 |
| --- | --- |
| RingCabinetLayout.Position | 页面绝对坐标 |
| RingCabinetIntervalLayout.RelativePosition | 相对柜体原点 |
| RingCabinetSwitchLayout.RelativePosition | 相对间隔原点 |
| PoleLayout.Position | 页面绝对坐标 |
| AttachmentLayout.Offset | 相对所属 Pole |
| OverheadLineLayout.Start / End | 页面绝对坐标 |

恢复时不得把相对坐标预先展开为绝对坐标保存。父对象移动后，子布局仍通过相对坐标自然跟随。

## 7. Domain ID 与 Layout 身份关联

### 7.1 LayoutKey

当前 Layout 不需要另建独立 Guid。Layout 身份由类型和稳定 Domain ID 组成：

| Layout 类型 | LayoutKey |
| --- | --- |
| RingCabinetLayout | `(ring-cabinet, CabinetId)` |
| RingCabinetIntervalLayout | `(ring-cabinet-interval, IntervalId)` |
| RingCabinetSwitchLayout | `(ring-cabinet-switch, SwitchDeviceId)` |
| PoleLayout | `(pole, PoleId)` |
| AttachmentLayout | `(attachment, AttachmentId)` |
| OverheadLineLayout | `(overhead-line, ConnectionId)` |

使用复合 LayoutKey 可防止未来不同类别偶然复用同一外键时发生字典覆盖，但当前 Domain 的工程级 ID 唯一性仍应保持。

不得使用对象引用、对象哈希、显示名称、杆号、Interval Sequence 或 DTO 数组下标恢复 Layout。

### 7.2 一对一覆盖规则

第一版对所有可绘制对象采用严格一对一策略：

- 每个 RingCabinet 恰好一个 RingCabinetLayout；
- 每个 RingCabinetInterval 恰好一个 IntervalLayout；
- 每个需要绘制的柜内 SwitchDevice 恰好一个 SwitchLayout；
- 每个 Pole 恰好一个 PoleLayout；
- 每个 PoleAttachment 恰好一个 AttachmentLayout；
- 每个 OverheadLine 恰好一个 OverheadLineLayout。

缺少、重复或类型错误均加载失败。当前不以“缺少 Layout”隐式表示未放置对象；未来若支持未放置设备，必须增加明确的 PlacementState 合同并升级版本。

### 7.3 删除对象与孤立 Layout

对象删除必须由 Application/Edit Command 作为一个事务协调：

```text
删除 Domain 对象或关系
    + 删除其直接和从属 Layout
    + 处理 Domain 引用
    ↓
一次 Command 提交
```

- 删除 RingCabinet 时同时删除柜体、Interval 和 Switch Layout。
- 删除 PoleAttachment 时删除对应 AttachmentLayout。
- 删除 OverheadLine / Connection 时删除对应 OverheadLineLayout。
- 删除 Pole 前必须先按 Domain 规则处理 Attachment 和线路关系，再删除布局。

保存时发现孤立 Layout 必须拒绝保存，不能静默丢弃；加载时发现孤立 Layout 必须拒绝打开，不能猜测新归属。旧版本孤立数据只能由明确 Migration 规则处理。

## 8. Layout 恢复流程

### 8.1 恢复顺序

Layout 必须在 Domain 完整恢复并通过 Domain 校验后恢复：

```text
Current Layout DTO
    ↓ 结构、单位和数值校验
Domain ID Resolver
    ↓ 关联、类型和所有权校验
Runtime Layout 对象
    ↓ Layout 集合完整性校验
Layout Snapshot / Store
    + 已恢复 Domain
    ↓
DrawingSceneBuilder
    ↓
DrawingScene + HitTestIndex
    ↓
DrawingSceneRenderer / DrawingVisual
```

Scene、HitTestIndex 和 Visual 均在最后重新生成，不能从文件恢复。

### 8.2 具体构造顺序

1. 校验 `coordinateUnit = mm`。
2. 解析并校验全部 PointDto 和尺寸。
3. 为每个 RingCabinet 按 ID 找到 Domain 聚合。
4. 先构造其 SwitchLayout，再构造 IntervalLayout，最后构造 RingCabinetLayout。
5. 构造 PoleLayout。
6. 根据 PoleAttachment ID 构造 AttachmentLayout。
7. 根据 ConnectionId 和 OverheadLine 明细构造 OverheadLineLayout；IsContinued 从 Domain 取得。
8. 建立 LayoutKey 索引并检查重复、缺失和孤立项。
9. 生成只读一致快照，再交给 EditorSession。
10. 使用 Domain + Layout 重建 Scene 和交互索引。

数组读取顺序不影响对象关联。环网柜的视觉组合顺序由 Domain Interval 顺序和 ID 映射共同确定。

### 8.3 原子加载

恢复过程使用临时 Layout 上下文：

- 任一 DTO、引用或构造校验失败时丢弃全部临时 Layout；
- 不把部分恢复对象写入当前 DrawingLayout；
- 只有 Domain 与 Layout 跨区校验全部成功后，才一次性替换 EditorSession；
- 加载成功后的 Scene 全量重建，Selection 为空，Undo/Redo 历史为空且 Dirty=false。

## 9. 当前 Layout 根模型的实现前置条件

当前 `DrawingLayout` 只集中保存 Pole、Attachment 和 OverheadLine；`RingCabinetLayout` 由演示/属性查看入口独立持有。完整工程需要一个文档级 Layout 根快照，至少能统一索引：

- 多个 RingCabinetLayout；
- PoleLayout；
- AttachmentLayout；
- OverheadLineLayout。

后续实现可扩展 `DrawingLayout`，或在 Application 层增加不依赖 WPF 的 `LayoutDocument/LayoutSnapshot`。无论采用哪一种，必须保证：

- 每个 LayoutKey 唯一；
- 可以获取一致只读快照；
- 替换和删除通过明确入口执行；
- 持久化 Mapper 不从 Desktop 演示字段或当前选中对象搜集布局。

本节只是实现前置条件，不在 M4-B-3-A 修改项目结构或代码。

## 10. 完整性校验

### 10.1 DTO 结构校验

- `coordinateUnit` 必须为 `mm`；
- 必填 ID 非空；
- Point、Offset、尺寸和 MainBusY 均为有限数值；
- Width、Height 大于零；
- `0 <= mainBusYMillimeters <= heightMillimeters`；
- DTO 集合和必填子集合不为 null；
- 同类 LayoutKey 不重复。

当前未冻结页面尺寸和允许坐标范围，不自行增加任意最大坐标限制。后续页面模型确定后，再通过正式版本合同增加页面边界校验。

### 10.2 Domain 关联校验

- CabinetId 指向 RingCabinet；
- IntervalId 指向外层 Cabinet 的 Interval；
- SwitchDeviceId 指向外层 Interval 的柜内开关；
- PoleId 指向 Pole；
- AttachmentId 指向 PoleAttachment；
- ConnectionId 同时指向 OverheadLine Connection 和 OverheadLine 明细；
- 不允许 Layout 指向当前不支持或不可绘制的对象类型。

### 10.3 覆盖与所有权校验

- 每个当前可绘制 Domain 对象存在且仅存在一个对应 Layout；
- RingCabinet 的 Interval 和 Switch Layout 集合与 Domain 聚合成员集合完全相等；
- AttachmentLayout 不因嵌套位置或数组顺序改变 PoleAttachment 的 Domain 归属；
- Layout 不能跨柜、跨 Interval 或跨 Attachment 所有权边界；
- Domain 删除后不存在孤立 Layout。

### 10.4 坐标语义校验

- 绝对位置和相对偏移按各自字段恢复，不互相转换后保存；
- 标签偏移允许位于图元边界之外，不据此判定文件损坏；
- 当前模型未规定 Interval 必须完全位于柜体边界内，不在持久化层自行裁剪或移动；
- OverheadLine Start/End 只影响显示，不改变 Connection TerminalId；
- 线路坐标与 Pole 坐标相交不生成拓扑关系；
- SupportPoleIds 不由线路折点或坐标推导。

### 10.5 跨层一致性校验

保存和加载均检查：

- Domain 与 Layout 使用同一稳定 ID；
- OverheadLine 的 Domain / Runtime Layout IsContinued 一致；
- Layout 不包含 Domain 状态、设备类型或拓扑副本；
- SceneBuilder 能为恢复后的 Domain + Layout 完整生成场景；
- Scene 构建失败视为加载失败，不把半成品会话交给用户。

错误应包含稳定错误代码、JSON 路径、LayoutKey、Domain ID 和可读说明。校验失败时不得自动生成默认 Layout、调整坐标或删除未知项。

## 11. Layout 版本迁移

### 11.1 版本来源

第一版不在 Layout 区重复保存独立版本号。Manifest 的 `formatVersion` 选择对应的版本化 Layout DTO：

```text
Manifest.formatVersion
    ↓
ProjectDtoVn.LayoutDtoVn
    ↓ ProjectVnToVn+1Migration
ProjectDtoVn+1.LayoutDtoVn+1
    ↓
Current Layout DTO
```

这样避免 Manifest 版本与 Layout 子版本冲突。未来若 Layout 确需独立演进，必须先定义兼容矩阵并通过总工程格式升级引入，不能临时增加未受管理的版本字段。

### 11.2 迁移规则

- 迁移只操作 DTO，不构造 Runtime Layout、Scene 或 WPF 对象。
- 所有已有 Domain ID 原样保留。
- 坐标字段重命名必须保持毫米语义。
- 单位变化必须显式、确定性换算，禁止依赖运行机器 DPI。
- 绝对坐标与相对坐标变化时，必须使用旧版本明确保存的父布局计算，不从 Scene 或屏幕状态推测。
- 新增字段只在存在历史合同确定的默认值时补齐。
- 不自动为旧文件缺失的可绘制对象生成 Layout；若旧版本允许缺失，迁移规范必须明确转换为新定义的 PlacementState 或拒绝升级。
- 删除或合并 Layout 类型时，必须同步检查其 Domain 外键和所有权。
- 未知 Layout 类型、单位或更高格式版本拒绝加载。

### 11.3 OverheadLine IsContinued 迁移

若旧版本 Layout DTO 曾保存 `isContinued`：

1. 与同一 ConnectionId 的 Domain `OverheadLine.IsContinued` 比较；
2. 一致时删除 Layout 副本，保留 Domain 事实和 `continuationOffset`；
3. 不一致时迁移失败并报告冲突，不静默选择任一值。

## 12. 保存流程

Layout 保存必须来自与 Domain 同一编辑状态的不可变快照：

1. 等待当前 Command 提交或取消，排除拖动 Preview。
2. 捕获 Domain + Layout 的一致 StateId。
3. 校验 Layout 覆盖、外键和所有权。
4. 映射为 Current Layout DTO，保持全部毫米数值。
5. 按稳定 ID 确定性排序无业务顺序集合。
6. 执行 DTO 结构和跨区校验。
7. 与 Domain DTO 一起写入同一个 `document.json`。
8. 保存成功后由 Editor 标记对应 StateId；保存期间若又发生编辑，当前会话仍为 Dirty。

保存失败不修改 Runtime Layout、不清空 Undo/Redo，也不更新正式工程文件。

## 13. 验收建议

后续实现至少覆盖：

- 六类 Layout 的 DTO 往返后 ID、毫米坐标、尺寸和偏移逐值一致；
- 混合 RingCabinet 的 Interval / Switch Layout 按 Domain ID 正确恢复，不依赖数组顺序；
- 多个 RingCabinet、Pole、Attachment 和 OverheadLine 可由文档级 Layout 根统一恢复；
- Pole 拖动后的最终毫米坐标保存并恢复，拖动 Preview 不进入文件；
- Attachment Offset 在 Pole 移动前后保持相对语义；
- 缩放、平移、DIP、DPI、Selection、Scene 和 Visual 不出现在 JSON；
- 重复 LayoutKey、缺失布局、孤立布局、错误类型和跨聚合引用被拒绝；
- NaN、Infinity、非正尺寸及越界 MainBusY 被拒绝；
- OverheadLine IsContinued 只以 Domain 为事实源，恢复后 Runtime Layout 与其一致；
- 旧版本迁移不改变已有 Domain ID 和毫米几何；
- 恢复后的 Domain + Layout 能完整重建 DrawingScene 和 HitTestIndex。

## 14. 本阶段不实现

- Layout DTO、Mapper、Rehydrator、Migration 或 JSON Schema 代码；
- Layout 根集合、LayoutStore 或现有 Layout 类修改；
- Domain DTO、Domain 模型或电气拓扑修改；
- Rendering、Symbol、SceneBuilder 和 WPF 视觉修改；
- 缩放/平移恢复、自动布局、吸附、线路自动布线和页面模型；
- WorkScopeLayout、GroundingPointLayout、AnnotationLayout 或 ConnectionRoute；
- 保存/打开 UI、Undo/Redo 跨会话和自动修复。
