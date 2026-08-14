# P1-1-A Label Layout Engine Design

## 1. 目标与边界

本设计定义 Rendering 层的 Label 布局能力，为电缆、环网柜和杆塔提供稳定的文字标注位置。Label 是图形呈现结果，不是 Domain 电气事实。

Domain 不保存以下显示信息：

- Label Position；
- Font；
- Bounds；
- 文本避让结果；
- 渲染层的颜色、字重或对齐方式。

Domain 只提供可显示的业务文本和 Stable ID。Rendering 根据当前对象和 Layout 生成 Label 请求，再由 Label Layout Engine 计算最终位置。

## 2. Label 模型

### 2.1 LabelRequest

`LabelRequest` 表示一个待布局的标签请求，属于 Rendering。第一版建议包含：

- `TargetKind`：标签所属对象类型；
- `TargetId`：对应 Domain 对象的 Stable ID；
- `Text`：待显示文本；
- `Anchor`：来源对象或其图形 Layout 提供的锚点；
- `Offset`：相对于锚点的期望偏移；
- `PreferredAlignment`：首选对齐方式；
- `Priority`：碰撞时的保留优先级；
- 字体测量所需的最小样式信息。

LabelRequest 不持有 Domain 对象引用，不修改对象，也不生成 Stable ID。

### 2.2 LabelLayoutResult

`LabelLayoutResult` 表示一个已完成的布局结果，建议包含：

- `TargetKind`；
- `TargetId`；
- `Text`；
- `Position`；
- `Bounds`；
- 实际使用的对齐方式；
- 是否发生了 Offset 调整或碰撞避让。

LabelLayoutResult 是 Rendering 的只读投影输入。它可以被 `SceneText` 或具体 Renderer 转换为图形元素，但不回写 Domain。

## 3. Label Layout Engine 职责

Engine 的基本契约为：

```text
LabelRequest collection
          ↓
LabelLayoutEngine
          ↓
LabelLayoutResult collection
```

Engine 负责：

- 根据 Anchor 和 Offset 计算初始位置；
- 根据字体和文本内容估算 Bounds；
- 按确定的 Priority 和稳定顺序处理碰撞；
- 在允许范围内调整标签位置；
- 输出可直接用于 Rendering 的结果。

Engine 不负责：

- 读取或修改 Domain 集合；
- 创建设备、端子或连接；
- 解释电气拓扑；
- 修改 Layout Domain 事实；
- 保存 Label 位置到工程文件。

第一阶段应保持确定性：相同的请求顺序、字体测量参数和视图尺度应得到相同结果。若多个标签 Priority 相同，应使用 TargetId 或请求顺序作为稳定的次级排序依据，避免结果随字典枚举顺序变化。

## 4. Position、Offset 与 Collision

### 4.1 Position

Position 是 Rendering 坐标，通常由设备 Layout、CableLayout 或 JointLayout 投影出的 Anchor 转换而来。它不改变设备在 Domain 中的身份，也不改变电气连接。

### 4.2 Offset

Offset 表示标签相对于 Anchor 的首选偏移。引擎应先应用 Offset，再进行碰撞检查。不同对象可以有不同的默认偏移，例如：

- RingCabinet 标签优先放在柜体上方或侧方；
- Pole 标签优先放在杆体侧方；
- CableSegment 标签优先放在线段中部附近。

### 4.3 Collision

第一阶段碰撞处理只处理 Label 与 Label 之间的矩形重叠。处理策略按优先级依次尝试有限的候选偏移，例如上、下、左、右方向；找不到无碰撞位置时保留确定性的降级位置，并在结果中标记发生了避让或无法完全避让。

第一阶段不自动移动设备符号、线路、端子或其他 Domain/Rendering 几何对象。Label Collision 不能改变拓扑或设备布局。

## 5. 第一阶段支持对象

### 5.1 CableSegment Label

CableRenderer 根据 CableSegment 的业务属性生成 LabelRequest。文本可包括电缆型号、长度或当前允许显示的业务摘要。Anchor 来自 CableLayout 的线段中部或预定标注点。

### 5.2 RingCabinet Label

DeviceRenderer 或 RingCabinet Renderer 根据 RingCabinet 的 DisplayName/业务标识生成 LabelRequest。Anchor 来自 RingCabinetLayout，默认位于柜体外框上方或预留标题区域。

### 5.3 Pole Label

Pole Renderer 根据 PoleNumber 或显示名称生成 LabelRequest。Anchor 来自 PoleLayout，默认位于杆体侧方，避免遮挡杆上 Attachment 符号。

三类标签都通过 `TargetKind + TargetId` 保持与对象的稳定映射，不把 Domain 引用带入 Label 结果。

## 6. Rendering 集成

### 6.1 CableRenderer

CableRenderer 负责从 CableSegment 和 CableLayout 生成电缆线及其基础 LabelRequest。它不自行实现全局碰撞算法。由场景构建流程收集请求，并统一交给 Label Layout Engine。

### 6.2 DeviceRenderer

RingCabinetRenderer、PoleRenderer 等设备 Renderer 生成各自的符号和 LabelRequest。设备 Renderer 可以提供 Anchor、首选 Offset 和 Priority，但不直接决定与其他对象标签的最终位置。

### 6.3 Scene 构建流程

```text
Domain + Rendering Layout
          ↓
Device/Cable Renderers create LabelRequests
          ↓
Label Layout Engine
          ↓
LabelLayoutResults
          ↓
SceneText elements
          ↓
DrawingSceneRenderer
```

场景重新构建时重新计算标签布局。Selection、Zoom 和 Pan 只影响显示投影或屏幕测量参数，不改变 Domain。

## 7. 稳定性与生命周期

Label 结果是当前 Scene 的派生状态，不进入 Command、Undo/Redo 或 Persistence。设备移动、Cable Split、Reconnect 或属性编辑后，应通过重新构建 Scene 生成新的 LabelLayoutResult。

当对象被删除或不再存在时，不应继续保留其 LabelRequest。若对象 Stable ID 保持，标签的 TargetId 也保持；标签位置可以因当前 Layout 和碰撞环境变化而重新计算。

## 8. 后续切片

- **P1-1-B Label Layout Runtime**：实现 `LabelRequest`、`LabelLayoutResult` 和确定性的 Label Layout Engine。
- **P1-1-C Label Rendering Integration**：将 CableRenderer、RingCabinetRenderer、PoleRenderer 的请求接入 Scene 构建和文字渲染。

## 9. 非目标范围

本设计不实现：

- CAD 尺寸标注和工程制图标注体系；
- Label 文本编辑器；
- 自动换行、富文本或多行排版；
- 自动移动设备、线路或拓扑对象以解决碰撞；
- Label 持久化或用户自定义标签模板。

## 10. 设计结论

Label Layout Engine 是 Rendering 层的确定性派生组件。它接收来自 Cable、RingCabinet 和 Pole Renderer 的 LabelRequest，计算带有 Position、Offset 和 Bounds 的 LabelLayoutResult，再生成 SceneText。该过程不修改 Domain、Layout、电气拓扑或工程文件。
