# M5-C-3-A Professional 对象显示与基础交互架构设计

> 文档状态：实现前设计，仅定义显示、选择与只读属性查看边界，不实现代码<br>
> 编制日期：2026-08-12<br>
> 依据：`docs/distribution-professional-object-model-design.md`、`docs/professional-object-persistence-design.md`、`docs/professional-object-implementation-design.md`，以及当前 Domain、Rendering、Selection、PropertyInspector 实现

## 1. 目标与范围

本设计定义 `GroundingPoint` 和 `WorkScope` 如何进入现有 `DrawingScene`、WPF Rendering、单选机制和只读 Property Inspector。

第一阶段只建立以下闭环：

```text
DrawingDocument.WorkScopes / GroundingPoints
        +
Runtime Layout
        ↓
DrawingSceneBuilder
        ↓
Professional Scene Elements + HitTestIndex
        ↓
DrawingSceneRenderer
        ↓
显示 / 选择 / 高亮 / 只读属性查看
```

本阶段不改变 Professional 事实、Topology、电气状态或工程文件格式，不实现专业对象的创建、删除、拖动或编辑。

## 2. 已有事实与设计结论

### 2.1 Professional 事实源

- `DrawingDocument.WorkScopes` 和 `DrawingDocument.GroundingPoints` 是运行时唯一专业事实源；
- `GroundingPoint.TerminalId` 是工作地线电气位置的唯一拓扑引用；
- `BoundaryPoint.DeviceId + TerminalId + Side` 是工作范围边界的人工确认事实；
- Professional DTO 只负责保存，不直接驱动 UI，也不成为第二份运行时状态；
- Rendering、Selection 和 PropertyInspector 均不得创建、修改或补全 Professional 对象。

### 2.2 GroundingPoint.Location 的语义

现有 Domain 代码和设计文档均已明确：`GroundingPoint.Location` 是人工填写、面向用户的专业业务位置说明。其构造和更新规则只执行文本规范化与非空校验，持久化 DTO 也将其作为专业事实保存。

因此：

- `Location` 不是毫米工程坐标；
- `Location` 不是 DIP、屏幕坐标或 WPF 位置；
- `Location` 不能用于定位图元；
- Rendering 不得将图元坐标反写为 `Location`；
- PropertyInspector 将 `Location` 显示在“专业属性”中，而不是“布局”中。

如果后续需要人工调整地线符号或文字位置，应使用独立 Layout 数据，例如 `MarkerOffset`、`LabelOffset`，单位统一为毫米工程坐标。该数据不能加入 `GroundingPoint`。

## 3. 分层职责

| 层 | 负责 | 不负责 |
| --- | --- | --- |
| Domain / Professional | WorkScope、BoundaryPoint、GroundingPoint 及稳定引用 | 坐标、符号、高亮、命中范围 |
| Topology | Terminal、Connection、ElectricalNode 及引用关系 | 自动生成工作范围或工作地线 |
| Layout | 设备位置、端子可视锚点的布局依据，以及未来专业图元偏移 | 保存专业业务事实、推导 TerminalId |
| SceneBuilder | 解析 Domain + Layout，生成端子锚点、专业场景元素和命中条目 | 修改 Domain、自动扩展 WorkScope |
| Rendering | 把 SceneElement 绘制为 WPF 图形 | 保存 Professional 状态、处理业务校验 |
| Selection | 保存稳定 SelectionReference，完成命中与临时高亮 | 保存 Domain 对象引用、修改业务对象 |
| PropertyInspector | 将本次解析结果投影为只读值快照 | 双向绑定或直接修改 Domain / Layout |

## 4. TerminalId 到图面位置的解析

### 4.1 TerminalAnchorIndex

Professional 图元不能使用设备名称、杆号或“附近坐标”定位。建议在 Scene 构建期间同步生成只读 `TerminalAnchorIndex`：

```text
TerminalId
  ↓
Domain 所有权解析
  ↓
对应 Device / Interval / Attachment 的 Layout
  ↓
TerminalAnchor（毫米文档坐标）
```

建议锚点值至少包含：

```text
TerminalAnchor
├── TerminalId
├── Position : DocumentPoint
├── Direction（可选，仅用于确定默认符号朝向）
└── SourceObjectId（只读诊断信息）
```

`TerminalAnchorIndex` 是当前场景修订的临时派生数据：

- 不保存到 Domain 或 Professional DTO；
- 不保存 DIP、屏幕坐标或 WPF 对象；
- 不改变 Terminal、电气拓扑或 Layout；
- 场景重建后整体重新生成；
- 缺失锚点时报告明确的 Scene 构建或显示诊断，不寻找最近图元，也不移动 Professional 对象。

### 4.2 锚点来源

- 环网柜间隔边界使用 `ExternalTerminalId` 对应的间隔外部端子锚点；
- 杆上设备和电缆终端使用其 TerminalId 对应的设备图元锚点；
- 普通 Pole 只有在现有 Domain 中确实具有相应 Terminal 时才能提供电气锚点；
- OverheadLine 的任意 Layout 折点、杆号文字或线路中点都不能替代 Terminal；
- 若某工作边界需要落在线路中间，必须先在 Domain / Topology 中建立合法 Terminal，本阶段不通过 Rendering 虚构端子。

## 5. GroundingPoint 显示模型

### 5.1 映射链路

```text
GroundingPoint
  └── TerminalId
        ↓ TerminalAnchorIndex
      TerminalAnchor
        ↓ 可选 Professional Layout 偏移
      GroundingPoint Scene Elements
```

`ProfessionalSceneBuilder` 读取 `GroundingPoint` 和锚点，生成专业接地线图形及必要文字。专业事实只来自 GroundingPoint：

- 是否存在工作地线，由 GroundingPoint 是否存在决定；
- 图面文字可显示 `Number`，并按需求显示 `Location` 或 `Note`；
- 不读取 GroundSwitch 状态决定是否显示；
- 不读取 `IsEffectivelyGrounded` 决定是否显示；
- 不根据现有接地图元反向创建 GroundingPoint。

具体线型、颜色和接地符号必须复用配电专业绘图规则中已确认的样式。Rendering 只选择并组合显示图元，不在 Symbol 或 Scene 中保存“已接地”业务状态。

### 5.2 图面位置与 Layout

第一阶段默认以 TerminalAnchor 为符号连接点，并使用确定性的默认方向和标签间距。因此 M5-C-3-B 不要求新增可编辑坐标，也不要求修改 Persistence 格式。

后续如需人工避让，可增加独立布局对象：

```text
GroundingPointLayout
├── GroundingPointId
├── MarkerOffset : DocumentVector
└── LabelOffset  : DocumentVector
```

偏移只使用毫米工程坐标，并以 `GroundingPointId` 关联专业对象。不得保存绝对屏幕位置、WPF Transform、Terminal 副本或 DeviceId。该 Layout 的 DTO 和格式升级应作为独立持久化里程碑实施，不在 M5-C-3-B 中顺带修改。

### 5.3 失败边界

以下情况不绘制猜测结果：

- GroundingPoint 引用的 Terminal 不存在；
- Terminal 存在但当前场景没有可解析锚点；
- Layout 缺失或锚点存在重复定义；
- GroundingPointId 与布局引用不一致。

加载阶段已验证的 Professional 引用若在运行时失效，应作为工程一致性错误报告。Rendering 不自动修复，也不静默改绑。

## 6. WorkScope 显示模型

### 6.1 第一阶段表现

M5-C-3-B 只显示两个显式边界标记：

```text
WorkScope.StartBoundary.TerminalId
        ↓
Start Boundary Marker

WorkScope.EndBoundary.TerminalId
        ↓
End Boundary Marker
```

边界标记应能区分起始与终止角色，并可按需要显示 `Side`。两端的 `DeviceId` 用于 Resolver 的归属校验和属性展示，不用于替代 TerminalId 定位。

第一阶段不绘制两点之间的自动路径、填充范围、范围内设备着色或停电覆盖层。`Description` 可以作为只读标签或属性面板内容，但不能被解析为几何范围。

### 6.2 禁止自动扩展

不得通过以下信息自动生成 WorkScope 覆盖范围：

- Connection 或 ElectricalNode 连通路径；
- SwitchState、OperationalState 或有效接地结果；
- 带电、停电或可能带电显示状态；
- 两个边界之间的最短路径；
- OverheadLine 的 SupportPoleIds 或 Layout 折点。

Topology 在本阶段只用于解析 Terminal 和校验引用，不授权 SceneBuilder 推断“范围内对象”。

### 6.3 未来 Layout

若以后需要人工绘制范围覆盖线或调整标签，应独立设计：

```text
WorkScopeLayout
├── WorkScopeId
├── StartMarkerOffset
├── EndMarkerOffset
├── DisplayPath[]（仅人工布局路径）
└── LabelOffset
```

`DisplayPath` 只表达图面路径，不是电气拓扑路径，也不能用于反向生成 BoundaryPoint。第一阶段不创建该模型，不修改 Layout DTO 或工程格式。

## 7. DrawingScene 集成

### 7.1 场景构建顺序

建议将现有场景构建扩展为可组合的阶段：

```text
DrawingDocument + RuntimeLayoutDocument
        ↓
基础设备 Scene Elements
        +
TerminalAnchorIndex
        ↓
ProfessionalSceneBuilder
        ↓
Professional Scene Elements + Professional HitTest Entries
        ↓
DrawingScene
        ↓
Selection Overlay（编辑器临时层）
```

Professional 场景元素属于工程图内容，正常显示，并为未来 JPG/打印输出保留；Selection Overlay 是编辑器临时状态，不进入导出和打印。两者不能混为同一层。

### 7.2 Professional Scene Elements

第一阶段可继续输出当前 `SceneLine`、`SceneRectangle`、`SceneText` 等通用 SceneElement，必要时再增加纯场景几何类型。不得让 `DrawingSceneRenderer` 直接接收 GroundingPoint 或 WorkScope Domain 对象。

`ProfessionalSceneBuilder` 应保持输入只读、输出可重建：

- 输入 DrawingDocument 中的专业集合；
- 输入 TerminalAnchorIndex；
- 可选输入只读 Professional Layout；
- 输出 SceneElement 和 SelectionHitTestEntry；
- 不缓存 GroundingPoint、WorkScope 或 WPF Visual；
- 不调用 Domain 的 Add、Update、Remove 方法。

### 7.3 与 SymbolLibrary 的关系

专业接地线和边界标记可以复用 SymbolLibrary 的无状态几何定义，但 Symbol 只接收本次绘制参数，不保存 GroundingPoint、WorkScope、TerminalId 或选择状态。

M5-C-3-B 不修改现有设备 Symbol 的业务定义。若需要新增专业符号定义，应放在独立 Professional Rendering 范围，并保持“输入描述 → SceneElement”的无状态合同。

## 8. Selection 方案

### 8.1 稳定 SelectionReference

扩展 `SelectionTargetKind`：

| Kind | ObjectId | ParentId | 说明 |
| --- | --- | --- | --- |
| GroundingPoint | GroundingPointId | null | 独立专业实体 |
| WorkScope | WorkScopeId | null | 工作范围整体 |

SelectionReference 不保存 TerminalId、BoundaryPoint、Domain 对象或 SceneElement。TerminalId 和边界数据在当前 DocumentRevision 中通过 ObjectId 重新解析。

BoundaryPoint 是 WorkScope 内联值对象，没有独立 ID，因此第一阶段不作为独立 SelectionTargetKind。点击任一边界标记均选择所属 WorkScope。

### 8.2 HitTest

- 每个 GroundingPoint 生成一个以地线符号可视范围为基础、适度扩大的命中区域；
- 一个 WorkScope 生成两个命中条目，分别覆盖起始和终止边界标记，但两个条目拥有同一个 WorkScope SelectionReference；
- 专业小图元的命中优先级高于容器和线路，避免点击边界时选中背后的设备；
- 命中区域只用于交互，不改变专业符号的打印几何；
- 命中结果只进入 SelectionManager，不直接触发 Domain 修改。

当前 `SelectionHitTestIndex.Find()` 只返回单个条目。为支持选中 WorkScope 后同时高亮两端，M5-C-3-B 应增加按 SelectionReference 返回全部条目的只读查询，或提供等价的多几何查询；不能用两个伪造的 WorkScope ID 规避该问题。

### 8.3 高亮

选择状态继续由 `SelectionManager.Selected` 保存。高亮由 Overlay 根据命中索引临时生成：

- GroundingPoint：高亮其符号命中范围；
- WorkScope：同时高亮起始和终止两个边界标记；
- 不改写符号颜色，不覆盖设备带电/停电专业显示；
- 不写入 Domain、Layout、Professional DTO 或工程文件；
- 不进入 JPG 和打印。

对象删除、工程切换或引用失效时，Resolver 返回未解析结果，SelectionManager 清除选择；不得继续展示旧快照。

## 9. PropertyInspector 方案

### 9.1 解析链路

```text
SelectionReference
        ↓
SelectionObjectResolver（读取当前 DrawingDocument）
        ↓
ResolvedSelection（短生命周期）
        ↓
PropertyProjector
        ↓
PropertyInspectorSnapshot（纯值）
        ↓
PropertyInspectorViewModel
```

`PropertyInspectionSource` 可增加当前 DrawingDocument 的 WorkScope、GroundingPoint、Terminal 和必要设备只读集合，或等价的按 ID 查询入口。Resolver 可以在投影期间短暂引用 Domain 对象，但 UI 和 PropertyInspectorViewModel 只能保留值快照。

### 9.2 GroundingPoint 只读投影

建议属性分组：

| 分组 | 属性 | 来源 |
| --- | --- | --- |
| 基本信息 | GroundingPointId | Domain |
| 专业属性 | Location、Number、Note | Domain |
| 拓扑与归属 | TerminalId、解析得到的设备名称/端子角色 | Domain + 当前解析结果 |
| 布局 | 未来 MarkerOffset、LabelOffset；首版无可编辑项 | Layout |
| 显示信息 | 命中范围、显示类型 | 当前 Scene 描述，只读 |

解析得到的设备名称和端子角色只作为 `Derived` 展示值，不写回 GroundingPoint，也不持久化为第二份事实。

### 9.3 WorkScope 只读投影

建议属性分组：

| 分组 | 属性 | 来源 |
| --- | --- | --- |
| 基本信息 | WorkScopeId、Description | Domain |
| 起始边界 | DeviceId、TerminalId、Side、解析名称 | BoundaryPoint + 当前解析结果 |
| 终止边界 | DeviceId、TerminalId、Side、解析名称 | BoundaryPoint + 当前解析结果 |
| 关联对象 | GroundingPointIds，以及可选的编号显示快照 | Domain + 当前解析结果 |
| 显示信息 | 两个边界命中范围、显示类型 | 当前 Scene 描述，只读 |

GroundingPointIds 的显示顺序可沿用 Domain 集合顺序，但身份和引用解析只能依赖 ID。PropertyProjector 不计算边界间路径、范围内设备或停电结果。

### 9.4 ViewModel 边界

- 所有 Professional 属性在 M5-C-3-B 中均为只读；
- PropertyKey 使用稳定编码，不使用中文显示名作为业务键；
- Selection、场景或文档变化后重新解析并生成新快照；
- UI 不保存 GroundingPoint、WorkScope、BoundaryPoint、Terminal 或 Device 引用；
- Rendering 不保存属性面板状态；
- 未解析引用显示明确错误，不保留上一个对象的数据。

## 10. 刷新与一致性

以下事件应触发 Scene 与属性快照重建：

- 打开或切换工程；
- Domain Professional 集合发生受控修改；
- 相关 Device / Terminal 或 Layout 变化；
- Undo / Redo 完成；
- Selection 变化只需重建 Overlay 和属性快照，主场景可按现有刷新策略复用或重建。

刷新始终从当前 DrawingDocument 和 Runtime Layout 读取。Scene、HitTestIndex、TerminalAnchorIndex 和 PropertyInspectorSnapshot 都是可丢弃的派生结果，不作为 Dirty 判断依据。

## 11. M5-C-3-B 最小实现范围

### 11.1 建议新增文件

```text
src/DistributionDrawing.Rendering.Wpf/Professional/
├── ProfessionalSceneBuilder.cs
├── TerminalAnchor.cs
└── TerminalAnchorIndex.cs
```

如现有场景几何无法表达接地线和边界标记，可在同一项目中新增最少量、无业务状态的 Professional 场景工厂或定义文件。不要复制 Device Symbol 或引入 Professional 专用 Domain。

### 11.2 建议修改文件

```text
src/DistributionDrawing.Rendering.Wpf/Rendering/DrawingSceneBuilder.cs
src/DistributionDrawing.Rendering.Wpf/Interaction/SelectionReference.cs
src/DistributionDrawing.Rendering.Wpf/Interaction/SelectionHitTestIndex.cs
src/DistributionDrawing.Rendering.Wpf/Interaction/SelectionOverlayBuilder.cs
src/DistributionDrawing.Rendering.Wpf/PropertyInspector/PropertyInspectionSource.cs
src/DistributionDrawing.Rendering.Wpf/PropertyInspector/SelectionObjectResolver.cs
src/DistributionDrawing.Rendering.Wpf/PropertyInspector/ResolvedSelection.cs
src/DistributionDrawing.Rendering.Wpf/PropertyInspector/PropertyProjector.cs
```

若需要在现有演示窗口验证，可只对 Desktop 的场景装配入口做最小修改；不得在 Desktop 保存专业对象副本或实现创建、删除、编辑逻辑。

### 11.3 明确禁止修改

M5-C-3-B 不应修改：

- `DistributionDrawing.Domain`；
- `DistributionDrawing.Infrastructure/Persistence` 和 FormatVersion；
- Topology、ElectricalNode、Connection；
- 现有设备 Symbol 的业务定义；
- Layout DTO 与工程文件结构；
- Editor Command / Undo；
- WorkTicketData、SafetyMeasure、OperationStep。

## 12. M5-C-3-B 验收标准

### 12.1 显示

- 有效 GroundingPoint 能通过 TerminalId 定位并显示专业接地线图形；
- GroundingPoint.Location 仅作为专业文字使用，不参与坐标计算；
- 有效 WorkScope 同时显示起始和终止边界标记；
- 不生成边界间自动路径、范围填充或范围内设备集合；
- 缺失 Terminal 锚点时提供明确错误，不显示猜测位置；
- Scene 重建不会修改任何 Domain、Topology 或 Layout 数据。

### 12.2 选择与高亮

- 点击 GroundingPoint 后得到 `GroundingPointId` 对应的 SelectionReference；
- 点击 WorkScope 任一边界后得到同一个 `WorkScopeId` 对应的 SelectionReference；
- GroundingPoint 选择后只高亮自身；
- WorkScope 选择后同时高亮两个边界；
- SelectionManager、HitTestIndex 和 Overlay 均不持有 Domain 对象引用；
- 选择和高亮不进入工程文件、JPG 或打印。

### 12.3 属性查看

- GroundingPoint 可只读查看 ID、TerminalId、Location、Number、Note 及解析归属；
- WorkScope 可只读查看 ID、Description、两个 BoundaryPoint 和 GroundingPointIds；
- PropertyInspectorViewModel 只保存值快照；
- 不出现可编辑 Professional 属性，不生成 Command；
- 未解析对象不会继续显示旧属性。

### 12.4 架构检查

- 不修改 Domain、Persistence、Topology、Layout DTO 或现有设备 Symbol 业务定义；
- 不从图形、状态或拓扑自动创建 Professional 对象；
- 不实现自动停电分析、自动安全措施或工作票数据；
- Rendering 只表现已经存在且通过校验的 Professional 事实。

## 13. 后续阶段

M5-C-3-B 完成最小显示与只读交互后，后续能力应分阶段设计和实现：

1. Professional Layout：人工调整符号和标签偏移，并独立升级 Layout DTO；
2. Professional Editor Command：创建、删除、修改 WorkScope / GroundingPoint，接入 Undo / Redo 与 Dirty；
3. WorkScope 人工显示路径：只保存用户绘制的 Layout 路径，不自动推断拓扑范围；
4. JPG 与打印验收：包含专业图元，不包含选择高亮；
5. WorkTicketData、SafetyMeasure、OperationStep：保持独立业务里程碑。
