# P1-2-F-1 Rendering Number Integration Design Review

## 1. 审查范围

本审查基于 P1-2-A 冻结的编号合同，检查当前 RingCabinet、Interval 和
SwitchDevice 的 Rendering 编号来源，以及它们与 Label Runtime 的边界。

审查对象包括：

- `DrawingSceneBuilder` 及其 RingCabinet 调用链；
- `RingCabinetRenderer`、`RingCabinetSymbol` 和三个 Interval Symbol；
- `SwitchSymbolDefinition`；
- `LabelRequest`、`LabelLayoutEngine` 和现有 Cable/RingCabinet/Pole Label 路径；
- `RingCabinetInterval.BusinessNumber` 与 `GetSwitchBusinessNumber(...)`。

本阶段只冻结设计结论，不修改生产代码、测试或 Persistence。

## 2. 当前 Rendering 调用链

实际 RingCabinet Scene 构建路径为：

```text
DrawingSceneBuilder
    -> RingCabinetRenderer.Render
        -> RingCabinetSymbol.CreateElements(..., includeLabels: false)
        -> RingCabinetSymbol.CreateLabelRequests
        -> LabelLayoutEngine.Layout
        -> SceneText
```

`DrawingSceneBuilder` 不直接使用 RingCabinet 的旧名称标签路径。RingCabinet 的柜体、
间隔和开关符号由 `RingCabinetSymbol` 及具体 Interval Symbol 生成，业务名称标签由
`CreateLabelRequests` 统一交给 `LabelLayoutEngine`。

三个 Interval Symbol 仍保留 `includeLabels` 为 `true` 时的直接 `SceneText` 逻辑。
但 RingCabinet 主 Renderer 明确以 `includeLabels: false` 调用它们，因此当前主 Scene
路径不会同时产生旧标签和 Label Runtime 标签。后续实施应继续保持这一单一路径，避免
把直接 Symbol API 重新接入主 Scene。

## 3. 当前编号和文本来源审查

### 3.1 RingCabinet 标签

RingCabinet 主名称由：

- 来源：Domain `RingCabinet.DisplayName`；
- 生成：`RingCabinetSymbol.CreateLabelRequests`；
- 布局：`LabelLayoutEngine`；
- 输出：`SceneText`。

当前没有使用 RingCabinet 自行拼接业务编号。柜体名称属于显示属性，不等同于间隔或
开关业务编号。

### 3.2 Interval 标签

当前主 Scene 为每个 Interval 生成两类 LabelRequest：

1. `${interval.Sequence}#`：来源是 Rendering 直接读取的 `Sequence`，不是
   `RingCabinetInterval.BusinessNumber`；
2. `interval.DisplayName`：来源是 Domain 显示名称。

因此，当前序号标签仍是历史 Rendering 表达，不符合 P1-2-A 要求的正式业务标签合同。
即使常规模板中 `Sequence` 与 `BayIndex` 经常相同，也不能把 Sequence 视为 Business
Number。P1-2-F-2 必须将该标签改为直接显示 `interval.BusinessNumber`，并停止用
`Sequence` 生成业务编号文本。

当前代码没有根据 `IntervalKind`、`GroundingStructureKind` 或符号位置拼接
`-X`、`-X-2` 等编号；问题是使用了错误的事实来源，而不是存在一套完整的 Renderer
编号算法。

### 3.3 SwitchDevice 标签

当前主 Scene 的开关名称 LabelRequest 来源是：

- `switchDevice.DisplayName`；
- `switchDevice.Id` 作为 LabelRequest 的目标 ID；
- 开关位置和偏移来自 `RingCabinetSwitchLayout`；
- 最终位置由 `LabelLayoutEngine` 决定。

当前没有把 `interval.GetSwitchBusinessNumber(switchDevice.Id)` 接入 Rendering，
所以 `-X`、`-X-4`、`-X-2`、`-X-7`、`-X-47` 尚未作为正式开关业务编号显示。
P1-2-F-2 应改为由 Domain API 提供文本，Rendering 只创建对应 LabelRequest。

### 3.4 SwitchState 状态文字

`SwitchSymbolDefinition` 根据 `SwitchState` 继续生成“合”或“分”。这不是业务编号，
也不是设备名称标签，必须保持独立：

- `SwitchSymbolDefinition` 读取当前状态并绘制状态文字；
- `LabelLayoutEngine` 只处理名称和业务编号等标签；
- P1-2-F-2 不得把“合/分”复制为 LabelRequest。

### 3.5 PT 符号文字

PT Interval Symbol 中的 `PT` 是固定的设备符号说明文字，不是 PT 间隔业务编号。
它不承担 `-X`、`-X-2` 或 `-X-7` 的编号职责，后续仍应与编号标签分开处理。

## 4. Domain / Rendering 编号边界

Domain 是编号合同的唯一来源：

- `RingCabinetInterval.BusinessNumber` 提供 `-X`；
- `RingCabinetInterval.GetSwitchBusinessNumber(switchDeviceId)` 提供设备角色编号；
- `IntervalKind` 和 `GroundingStructureKind` 只在 Domain 内参与编号合同计算；
- Rendering 不应重复实现这些规则。

Rendering 只负责：

- 从 Domain 读取已经确定的文本；
- 创建 `LabelRequest`；
- 从 Layout 读取 Anchor 和 Offset；
- 通过 `LabelLayoutEngine` 处理 Bounds、Collision 和最终位置；
- 输出 `LabelLayoutResult` 对应的 `SceneText`。

Rendering 不得根据以下信息生成业务编号：

- `IntervalKind`；
- `GroundingStructureKind`；
- `Sequence`（除非仅作为非业务的调试/物理位置显示）；
- 符号上下位置；
- SwitchKind 名称或数组顺序；
- 字符串模式或固定后缀。

## 5. 目标编号标签方案

### 5.1 RingCabinet

柜体标签继续显示 `RingCabinet.DisplayName`。它是显示名称，不从间隔编号派生，也不
改变柜体 Stable ID。

### 5.2 Interval

正式业务标签应为：

```text
interval.BusinessNumber
```

例如 `BayIndex = 3` 时显示 `-3`。Label Anchor、Offset、字体和碰撞处理继续来自
`RingCabinetIntervalLayout` 与 `LabelLayoutEngine`。Rendering 不读取柜体图形位置来
推断编号。

### 5.3 IntegratedFeeder

每个开关的显示文本应来自：

```text
interval.GetSwitchBusinessNumber(switchDevice.Id)
```

Domain 已冻结的结果为：

| GroundingStructureKind | CircuitBreaker | IsolationSwitch | GroundSwitch |
|---|---:|---:|---:|
| `UpperIsolationGrounding` | `-X` | `-X-4` | `-X-47` |
| `UpperLowerGrounding` | `-X` | `-X-4` | `-X-7` |
| `LowerLowerGrounding` | `-X` | `-X-2` | `-X-7` |

Rendering 只显示 API 返回值，不判断结构类型，也不从编号字符串反推 GroundSwitch
所在的 ElectricalNode。

### 5.4 PTInterval

PT 可以位于任意合法 BayIndex，包括 `-1`、`-2`、`-3`、`-5` 或 `-7`。PT 相关编号
同样来自 `GetSwitchBusinessNumber(...)`：

| 角色 | Domain 返回的编号 |
|---|---:|
| PT Interval | `BusinessNumber`，即 `-X` |
| IsolationSwitch | `-X-2` |
| GroundSwitch | `-X-7` |

Renderer 不得假设 PT 位于最左、最右或最后一个间隔，也不得硬编码 `-2`、`-7`。

### 5.5 LoadSwitchInterval

当前 Domain 明确提供的普通负荷开关编号只有 Cable-side GroundSwitch 的
`-X-7`。其他 LoadSwitch 主开关编号由 API 返回 `null`，因为当前业务合同尚未明确。

P1-2-F-2 必须遵守这一事实：

- 非空 API 返回值可以显示；
- `null` 不得由 Rendering 猜测或补成 `-X`；
- `DisplayName` 可以作为名称标签保留，但不能冒充正式业务编号。

## 6. Label Runtime 影响

当前 `LabelRequest` 已包含：

- `TargetKind`；
- `TargetId`；
- `Text`；
- Rendering `Anchor` 和 `Offset`；
- 对齐、优先级和字体大小。

这些字段足以支持本阶段的编号接入。`TargetId` 已分别使用 IntervalId 或
SwitchDevice.Id，能够保持 Stable ID 和选择映射；业务编号文本本身不需要进入 Domain
或 Persistence。

本阶段不建议增加第二套 Label 模型，也不要求立即加入 `BusinessRole`。同一 Interval
可能同时产生业务编号和 DisplayName 两个 LabelRequest，但标签当前不承担 Domain
对象选择职责，`TargetKind + TargetId` 已足够。

如果后续需要对标签本身进行选择、过滤或 Inspector 展示，再单独评估增加只属于
Rendering 的 `LabelRole`；这不是 P1-2-F-2 的前置阻断。

## 7. Stable ID、HitTest 与 Selection

编号接入只改变 `LabelRequest.Text` 的来源，不改变对象身份：

- RingCabinet Stable ID 不变；
- IntervalId 不变；
- SwitchDevice Stable ID 不变；
- Terminal、ElectricalNode、Connection 不变；
- RingCabinet、Interval 和 SwitchDevice 的 HitTest/Selection 映射不变。

Label SceneElement 仍是 Rendering 输出，不应替代业务对象的 HitTest entry，也不应把
业务编号写回 Domain。类型变更后重新构建 Scene 即可读取新的 Domain 编号，不保存旧的
Label Position 或编号历史。

## 8. 现有 Label Runtime 一致性

现有三类主要对象都已经具备统一的 Label Runtime 入口：

| 对象 | LabelRequest | LabelLayoutEngine | 当前结论 |
|---|---|---|---|
| Cable | `CableRenderer`/`CableLabel` | 是 | 已统一，显示 CableType/Length |
| RingCabinet | `RingCabinetSymbol.CreateLabelRequests` | 是 | 已统一，但编号文本来源需修正 |
| Pole | `PoleRenderer`、`MixedPoleRenderer` 及其 Label | 是 | 已统一 |

因此当前未发现第二套 LabelLayoutEngine。RingCabinet 的直接 Symbol 标签代码属于兼容的
低层 API，但主 `RingCabinetRenderer` 已通过 `includeLabels: false` 禁止它在主链路中
重复输出名称标签。P1-2-F-2 应保持该约束。

## 9. 架构结论与实施阻断

### 已确认

- Domain 已提供 Interval 和 SwitchDevice 的编号 API；
- LabelRequest/LabelLayoutEngine 不需要为编号建立新系统；
- Layout 继续只提供位置事实；
- PT 任意间隔位置可以自然支持；
- SwitchState 的“合/分”显示合同与业务编号相互独立；
- Stable ID、HitTest 和 Selection 不需要因编号显示改变。

### 当前发现的 Rendering 缺口

当前 RingCabinet Interval 标签使用 `Sequence` 生成 `${Sequence}#`，而不是显示
`BusinessNumber`。当前 SwitchDevice 标签使用 `DisplayName`，尚未显示 Domain 提供的
业务编号。因此 P1-2-F-2 的核心工作是替换标签文本来源和补齐测试，不是重新设计
Domain、Topology 或 Label Runtime。

这属于 Rendering 实施缺口，不构成 Domain 架构阻断。若产品同时要求“名称”和“业务
编号”并存，二者应分别成为 LabelRequest；名称不能被静默替换成编号，编号也不能被
DisplayName 冒充。

## 10. 非目标范围

本设计不包含：

- 修改 Domain 编号合同；
- 修改 Interval Type Change；
- 修改 Persistence 或保存 BusinessNumber 字符串；
- 修改 Graph、ElectricalNode 或 GroundSwitch 拓扑；
- 修改 SwitchState、联锁或“合/分”状态文字；
- 修改 Selection、HitTest、Command、Undo/Redo 或 Desktop；
- PT 独立编号合同的重新定义；
- Label 编辑、引线、自动换行或复杂排版。

## 11. 后续 P1-2-F-2 实施计划

1. 将 RingCabinet Interval 业务标签从 `Sequence` 改为 `interval.BusinessNumber`。
2. 为非空 `GetSwitchBusinessNumber(...)` 结果生成 SwitchDevice 业务编号
   LabelRequest；名称标签按既有显示合同决定是否并存。
3. 保持柜体名称、间隔名称、开关名称/编号和状态文字的单一路径，禁止旧/新标签
   重复输出。
4. 继续使用当前 Layout Anchor、Offset 和共享的 `LabelLayoutEngine`。
5. 增加普通 LoadSwitch、三种 IntegratedFeeder、任意位置 PTInterval 的 Rendering
   测试，并断言编号来自 Domain API。
6. 验证 Type Change、Layout 移动、HitTest、Selection、Stable ID 和“合/分”状态不回归。

