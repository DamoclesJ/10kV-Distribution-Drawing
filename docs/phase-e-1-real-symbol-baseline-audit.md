# Phase E-1 — Real Symbol Baseline Audit

> 状态：**Audit Completed / Symbol Implementation Not Started**<br>
> 审计日期：2026-08-19<br>
> 代码基线：`36e9f17`<br>
> 业务参考：`配电专业附图图元.docx`

## 1. 审计目的与边界

本审计把《配电专业附图图元.docx》作为后续 Rendering 图元校准的业务视觉基准，完成 Word 图元提取、现有代码对照、差异分类、统一 Drawing Metrics 设计和 Phase E-1 实施拆分。

本轮已检查：

- Word 的 3 个实际页面；
- 1 个 16 行 × 4 列图元表；
- 38 个嵌入图片；
- 环网柜组合图；
- 环网柜与架空线路混合组合图；
- 图片、表格标题、状态文字和组合图说明之间的对应关系。

本轮没有修改 Domain、Persistence、RuntimeLayout、Rendering 或 Desktop 代码，也没有实施图元重绘、Routing、Snap、Alignment、避让、Crossing Detection 或 Line Jump。

## 2. Word 图元清单

### 2.1 配电站内设备

| 图元 | Word 中的状态或结构 |
| --- | --- |
| 配电站内断路器 | 拉开、合入 |
| 配电站内负荷开关 | 拉开、合入 |
| 配电站内隔离开关 | 拉开、合入 |
| 配电站内熔断式负荷开关 | 拉开、合入 |
| 熔断器 | 单一图元，未定义状态集合 |
| 接地刀闸 | 单一接地支路图元，表中未定义状态集合 |
| 站内电缆头 | 表格列出名称，但对应图元单元格为空；组合图用向下三角形表达电缆终端 |
| 电压互感器 | 两个相交圆形线圈并标注 `PT` |
| 配电变压器 | 两个相交圆形线圈 |
| 配电站用变 | 两个相交圆形线圈并标注 `ZB` |
| 10kV 出线断路器间隔 | 运行位置、检修位置 |

### 2.2 柱上设备和架空对象

| 图元 | Word 中的状态或结构 |
| --- | --- |
| 柱上断路器 | 拉开、合入；矩形设备框内使用断路器状态符号 |
| 柱上负荷开关 | 拉开、合入；矩形设备框内使用负荷开关状态符号 |
| 柱上隔离开关 | 拉开、合入 |
| 跌落式熔断器 | 拉开、合入 |
| 水泥杆 | 图元表单元格未给出图形；组合图明确为圆圈 |
| 电缆终端杆 | 圆圈上方附加三角形；三角形表示电缆终端 |
| 隔离开关杆 | 圆圈与柱上隔离开关组合 |
| 断路器杆 | 圆圈与柱上断路器组合 |
| 负荷开关杆 | 应由圆圈与柱上负荷开关组合；Word 表给出设备图元，但组合样例未单独展示该杆型 |
| 跌落式熔断器杆 | 圆圈与跌落式熔断器组合 |
| 架空变压器 | 组合图中为杆塔、跌落式熔断器与架空变台图元的组合 |
| 架空变台 | 圆形变压器主体和附属小圆 |
| 用户架空变台 | 三角形与两个小圆组合 |

### 2.3 一二次融合与三工位设备

Word 分别给出：

- 三工位断路器（上刀上接地）：拉开位置、接地位置、运行位置；
- 三工位断路器（下刀下接地）：拉开位置、接地位置、运行位置。

六张状态图不是同一个图形旁加自由文字，而是刀闸、断路器、接地支路的组合几何发生变化。上刀／下刀和上接地／下接地决定各部件沿垂直主回路的相对位置。

### 2.4 线路与其他图元

| 图元 | Word 中的视觉表达 |
| --- | --- |
| 架空线路 | 实线 |
| 电缆 | 虚线 |
| 封闭母线 | 粗实线 |
| 低压电缆分支箱 | 母线与多分支组合图 |
| 用户站 | 房屋轮廓内含三角形 |
| 电抗器 | 圆弧线圈图形 |
| 电力电容器 | 平行极板图形 |
| 可调电容器 | 电容器加斜向调节箭头 |
| 配电电容器 | 三角形组合图形 |
| 设备状态 | 在运、拆除；Word 用普通线与加斜杠线作对照 |

## 3. Word 明确的视觉规则

以下规则由 Word 图元表、组合图或组合图正文明确给出：

1. 架空线路使用实线。
2. 电缆线路使用虚线。
3. 杆塔在组合图中使用圆圈。
4. 电缆终端杆使用“圆圈 + 三角形”。
5. 三角形表示电缆终端；环网柜出线下方也使用向下三角形。
6. 目标环网柜主要表现名称、水平母线、等距／等宽间隔分支、间隔内部电气图元和电缆终端，不使用外围整体矩形作为最终视觉主体。
7. 环网柜名称位于母线上方，组合图中的线路名称位于名称与母线之间；名称整体相对母线居中。
8. 环网柜母线为水平线，间隔沿母线从左到右排列，间隔主回路从母线向下延伸。
9. 普通间隔的开关位于母线与下方电缆终端之间；业务编号位于对应开关附近。
10. PT 位于独立 PT 间隔的垂直支路下方，以双圆线圈加 `PT` 文字表示，不是矩形框内写 `PT`。
11. 一二次融合设备必须区分上刀上接地和下刀下接地的结构差异，并在各结构中区分运行位置、拉开位置、接地位置。
12. 架空组合图中，架空线路穿过杆塔圆圈中心附近；柱上设备与杆塔作为组合对象显示，而不是脱离杆塔悬空。

Word 组合图用红色和黑色区分不同电气显示状态，但未给出由开关状态自动推导线路颜色的算法。颜色继续遵守 `docs/drawing-rule.md` 的人工电气状态边界，本审计不建立自动带电传播规则。

## 4. Word 未明确或存在歧义的内容

### 4.1 未明确的量化内容

Word 没有给出以下绝对值：

- 间隔宽度和高度；
- 母线与名称、开关、终端的绝对距离；
- Switch、GroundSwitch、PT、杆塔圆圈和三角形的绝对尺寸；
- 线宽、虚线段长和间隔；
- 字体、字号、Label 偏移；
- Terminal 锚点的数值坐标；
- 不同设备之间的统一缩放比例；
- Line Jump 半径。

这些项目统一标记为：**视觉关系明确，但绝对尺寸待定义**。后续只能通过集中式 Drawing Metrics 和 Windows 截图验收确定，不能把 Word 图片像素直接当成工程毫米值。

### 4.2 来源内部歧义

1. 组合图正文中的 `NK1192` 已确认为文字错误；中间四间隔普通环网柜的正确名称是嵌入组合图所示的 `NK1191`。该文字错误不影响视觉结构。
2. 图元表的“站内电缆头”和“水泥杆”图形单元格为空，组合图正文和实际组合图补足了三角形与圆圈语义。
3. 组合图中有一个标注“改造前”的四间隔示例带外围矩形；其他环网柜和当前产品目标均不以外围整体矩形为最终视觉。该历史示例不得成为继续保留整体柜框的依据。
4. Word 展示了相对比例，但没有声明这些比例是标准尺寸；因此只采纳拓扑顺序和视觉关系，不采纳截图像素比例。

## 5. 当前代码逐项差异

### 5.1 分类定义

- **A — 基本符合**：现有表达与 Word 基线一致，仅需回归。
- **B — Geometry 调整**：语义对象和布局大体可用，但图形形状不符合。
- **C — Layout 调整**：设备相对位置、尺寸、锚点或组合排列不符合。
- **D — Scene primitive 缺口**：现有 SceneElement 无法直接、稳定地表达目标图形。
- **E — Domain 状态不足**：没有足够的业务事实决定图元。
- **F — 已有 Domain 状态未被 Rendering 正确表达**。

### 5.2 Rendering、Layout 与 Scene 对照

| 当前对象 | 分类 | 审计结论 |
| --- | --- | --- |
| `DrawingScene` / `SceneElement` | D | 只有 `SceneLine`、`SceneRectangle`、`SceneText`；没有 Ellipse、Arc、Polyline/Path，也没有可复用的闭合 Geometry。 |
| `SceneLine` / `DrawingSceneRenderer` | D | Pen 只有颜色和线宽，没有 dash pattern、line cap、line join；无法表达电缆虚线合同。 |
| `SymbolLibrary` | B、D | 已按 CircuitBreaker、LoadSwitch、IsolationSwitch、GroundSwitch、DropoutFuse 区分 `SymbolKind`，但五类全部复用同一个矩形式 `SwitchSymbolDefinition`。类型分派可复用，几何定义需要分化。 |
| `SwitchSymbolDefinition` | B、F | 当前统一绘制“矩形 + 斜线／横线 + 分／合文字”；Word 为不同开关种类定义不同专业几何。Open/Closed 已有但未被正确表现。 |
| `GroundSwitch` | B、F | 没有独立定义文件，使用通用 `SwitchSymbolDefinition`；已有 `GroundSwitch` 状态和位置事实，但接地刀闸几何未按 Word 表达。 |
| `RingCabinetSymbol` | B、C | 当前通过 `FrameSymbolDefinition` 绘制外围整体矩形，母线横跨整个柜体 Layout；目标应去除整体框，并重新校准名称居中、母线跨度和间隔分支锚点。 |
| `IntervalSymbol` | A | 按 `IntervalKind` 分派到普通、一二次融合、PT 的架构正确，可继续复用。 |
| `LoadSwitchIntervalSymbol` | B、C、F | 当前绘制间隔矩形框和贯穿竖线，开关仍是通用矩形；没有 Word 中明确的下方电缆终端三角形。已有负荷开关和接地开关状态未专业化表达。 |
| `IntegratedFeederIntervalSymbol` | B、C、F | 能按 `GroundingStructureKind` 调整设备上下关系，但三个开关仍是通用矩形，外部端子被画成矩形电缆终端，不能形成 Word 的六张三工位状态图。 |
| `PTIntervalSymbol` | B、C | PT 当前是矩形加 `PT` 文字，目标是双圆线圈；隔离、PT、接地的垂直关系需要按 Word 校准。正式 `PTInterval` 和 `PTSymbolPosition` 可继续作为输入。 |
| `RingCabinetLayoutFactory` | C | `CabinetPadding`、`IntervalGap`、`IntervalWidth`、Switch 坐标、PT 坐标等 magic numbers 集中在单个工厂内，但没有统一 Metrics；柜体尺寸仍围绕外框设计。 |
| `TerminalAnchorIndex` | C | Stable TerminalId → 文档坐标架构正确；Integrated terminal 尺寸在此重复硬编码。图元校准后应从统一 Metrics 计算同一锚点，不能改变 TerminalId 或拓扑。 |
| `PoleSymbolDefinition` | B、C、D | 当前是竖杆加横担，Word 组合图的杆塔主体是圆圈；现有 Scene 无 Ellipse。`PoleLayout` 的窄宽高也按竖杆图设计。 |
| `CableTerminationSymbolDefinition` | B、C、D | 当前为矩形加短引线；Word 为三角形。杆上终端还必须与杆塔圆圈组合，并保持电缆侧／架空侧锚点语义。 |
| `MixedPoleRenderer` | C、F | Pole + Attachment 的组合架构正确，但只允许 IsolationSwitch/CircuitBreaker，拒绝 Domain 已允许的 LoadSwitch/DropoutFuse；附件目前从竖杆侧向引出，不符合圆圈杆塔组合。 |
| `CableRenderer` / `CableSymbol` | D、F | `CableLayout.Path` 可包含多个点，Renderer 会逐段画线；但每段仍为实线，无法表达 Word 的虚线。当前场景构建通常只生成首尾两点，正交 Routing 不属于 E-1。 |
| `OverheadLineSegment` / `LineSymbolDefinition` | A | 当前架空线为黑色实线，符合本轮线型基线；仍需在引入通用 StrokeStyle 后做回归。仓库没有独立 `OverheadLineRenderer`，该职责位于上述对象和 `DrawingSceneBuilder`。 |
| `DrawingSceneBuilder` | C、F | 统一从 Domain + RuntimeLayout 重建 Scene 的架构正确；Pole Attachment 白名单和 cable/terminal 视觉输入需要随 E-1 子阶段校准，不应在 Builder 中新增业务推导。 |
| `PoleLayout` / `AttachmentLayout` | C | 当前尺寸和偏移服务于竖杆与侧挂矩形，不能直接得到圆圈杆塔、三角终端和贴线柱上设备组合。 |

### 5.3 Magic number 现状

当前图元尺寸分散在：

- `RingCabinetLayoutFactory`；
- `IntegratedFeederIntervalSymbol`；
- `PTIntervalSymbol`；
- `TerminalAnchorIndex`；
- `PoleLayout`、`AttachmentLayout`、`RingCabinetIntervalLayout`、`RingCabinetSwitchLayout` 默认值；
- `SwitchSymbolDefinition`、`PoleSymbolDefinition`、`CableTerminationSymbolDefinition`；
- Label request 和各 Renderer 的线宽／字号。

后续不能继续在各 Renderer 中独立追加数字。E-1A 应先建立统一 Metrics，再做实际图元校准。

## 6. Domain 状态能力审计

### 6.1 已足够的 Domain 事实

普通开关的 `SwitchState.Open/Closed` 足以表达 Word 的拉开／合入。当前缺口主要是不同 `SwitchKind` 没有使用各自的专业几何，而不是 Domain 状态不足。

普通负荷开关三工位由 `LoadSwitch + GroundSwitch + SwitchAssembly` 表达，并可派生：

- `Running`：负荷开关闭合、接地开关拉开；
- `Disconnected`：两者均拉开；
- `Grounded`：负荷开关拉开、接地开关闭合。

一二次融合间隔已有：

- `IsolationSwitch`；
- `CircuitBreaker`；
- `GroundSwitch`；
- 三台开关各自的 Open/Closed；
- `GroundingStructureKind.UpperIsolationGrounding`；
- `GroundingStructureKind.UpperLowerGrounding`；
- `GroundingStructureKind.LowerLowerGrounding`；
- 组合联锁、运行方式评估和有效接地判断。

这些事实足以选择 Word 中上刀上接地／下刀下接地及运行／拉开／接地的组合图元。当前 Rendering 只逐台绘制通用矩形，没有把现有组合事实映射成专业组合几何，属于 **F**，不应为了图形重绘把三工位改成单一三值 Device。

`Pole + PoleAttachment + SwitchDevice + CableTermination` 足以表达：

- 普通水泥杆；
- 隔离开关杆；
- 断路器杆；
- 负荷开关杆；
- 跌落式熔断器杆；
- 电缆终端杆；
- 同一杆上组合多个已支持 Attachment。

其中 LoadSwitch 和 DropoutFuse 已存在于 Domain `SwitchKind`，`SwitchDevice.CreateForPole` 也允许创建；当前主要缺口是 Rendering 白名单与图形。

### 6.2 明确的 Domain／产品范围缺口

以下 Word 对象当前没有足够 Domain 事实：

1. 架空变压器／架空变台：当前没有 Transformer Device、端子、PoleAttachment、Layout、Persistence 或状态模型。既有设计明确把 Transformer 留在未来边界，E-1D 不得用假 Attachment 或纯图片绕过。
2. `10kV 出线断路器间隔` 的运行／检修位置：当前没有对应的人工保存状态维度。
3. 设备“在运／拆除”生命周期状态：当前没有对应 Domain 字段。
4. 独立配电变压器、配电站用变、用户站、低压电缆分支箱、电抗器和电容器等：不在当前 MVP Device Catalog 中。
5. 脱离环网柜的自由站内断路器、负荷开关、隔离开关和熔断式负荷开关：当前 `SwitchInstallationType` 只有 CabinetInterval/Pole，没有自由站内安装语境。

这些缺口应作为未来产品范围与 Domain 设计输入，不属于 E-1 图元校准的隐含授权。当前 E-1 可完成的目标是已有 RingCabinet、PTInterval、Pole、PoleAttachment、CableTermination、Cable 和 OverheadLine 的真实视觉表达。

### 6.3 需要后续语义复核但不阻塞绘图的事项

`UpperIsolationGrounding` 的有效接地组合当前可由拓扑和 `IsEffectivelyGrounded` 表达，但其派生 `OperationalState` 使用 `Maintenance`，Word 图元文字使用“接地位置”。图元选择可直接依据结构类型和三台开关状态，不需要先修改 Domain；如果产品界面以后直接显示运行方式名称，应另行确认术语映射。

## 7. 环网柜视觉基线

### 7.1 总体结构

后续目标结构为：

```text
              环网柜名称
             （线路名称）
        ───────────────  主母线
          │     │     │     │
        间隔1  间隔2  间隔3  PT间隔
          │     │     │     │
        开关   开关   开关    PT/接地
          │     │     │
          ▽     ▽     ▽       电缆终端
```

该示意只表达相对关系，不定义尺寸。

### 7.2 普通环网柜

- 不绘制外围整体矩形；
- 不把每个间隔画成封闭业务柜格作为主要视觉；
- 名称位于母线上方并相对整体居中；
- 水平母线覆盖所有间隔分支；
- 分支等距排列，视觉上保持等宽间隔；
- 每个分支从母线向下连接开关状态图元；
- 电缆出线以向下三角形结束；
- 编号靠近对应开关，不依赖几何坐标生成业务编号。

### 7.3 一二次融合环网柜

- 继续使用同一水平母线和等宽间隔结构；
- `GroundingStructureKind` 决定刀闸、断路器和接地分支的上下关系；
- 三台开关状态共同决定专业组合图，不能只在通用矩形内显示“分／合”；
- Word 展示的上刀上接地、下刀下接地状态必须分别截图验收；
- CableTermination 保持在外部回路末端，TerminalAnchor 与实际三角形连接点一致。

### 7.4 PT 间隔

- PT 仍是 RingCabinet 内的 `PTInterval`，不是独立 PT 柜；
- 支路顺序保持母线 → 隔离刀闸 → PT → 接地关系；
- PT 使用双圆线圈图形并标注 `PT`；
- PT 相对隔离刀闸、接地支路和母线的位置由统一 Layout/Metrics 产生；
- 本审计不确定 PT/DTU 最终左右组合，不创建 DTU。

## 8. 柱上设备视觉基线

### 8.1 组合原则

Pole 是杆塔主体，PoleAttachment 是安装关系，Attached Device 决定附属专业图元。视觉上应先画杆塔圆圈，再按 Attachment 类型组合：

| 组合 | 目标视觉 |
| --- | --- |
| 普通杆塔 | 单一圆圈，杆号位于附近 |
| 隔离开关杆 | 圆圈 + 柱上隔离开关 |
| 断路器杆 | 圆圈 + 柱上断路器 |
| 负荷开关杆 | 圆圈 + 柱上负荷开关 |
| 跌落式熔断器杆 | 圆圈 + 跌落式熔断器 |
| 电缆终端杆 | 圆圈 + 三角形电缆终端 |
| 架空变压器 | 圆圈 + 跌落式熔断器 + 架空变台；当前 Domain 缺口，E-1 不实现 |

Word 明确组合关系，但没有给出附件相对圆圈的绝对偏移。该偏移进入 Pole/Attachment Metrics，并由 Windows 截图校准。

### 8.2 Terminal 语义

- 杆塔圆圈是杆位视觉，不改变 Pole 本体不导电的 Domain 原则；
- 架空线仍连接显式 Pole overhead anchor terminal；
- CableTermination 的 CableSide/OverheadSide 两个 Terminal 保持不变；
- 三角形的两侧锚点应与这两个 Terminal 对应；
- 改图元后必须同步 `TerminalAnchorIndex` 的几何计算，但不得修改 TerminalId、Connection 或 ElectricalNode。

## 9. 线路视觉基线与 Scene primitive 缺口

### 9.1 线路合同

- `OverheadLine`：实线；
- `Cable`：虚线；
- 本阶段不改变现有直线端点语义，不实施正交 Routing；
- `CableLayout.Path` 已能保存多个点并逐段渲染，未来 Routing 可以复用，但 E-1 不生成新折点。

### 9.2 最小 Scene 扩展建议

E-1A 建议最小增加：

1. `SceneStrokeStyle`：集中表达 Solid/Dash、dash pattern、cap、join；至少让 `SceneLine` 可区分实线和虚线。
2. `SceneEllipse`：表达杆塔圆圈、PT/变压器线圈和部分开关节点。
3. `ScenePolyline` 或通用 `ScenePath`：表达三角形、开关刀片、接地符号和闭合／非闭合折线；二者择一，避免平行重复抽象。
4. 若通用 Path 不能清晰表达圆弧，再增加 `SceneArc`；优先保持最小 primitive 集合。
5. 同步扩展 `DrawingSceneRenderer` 和 `DrawingSceneBoundsCalculator`；HitTest 仍使用现有业务 bounds，不从像素相交反推拓扑。

Line Jump 只在 Metrics 中预留半径字段，不在 E-1A 创建 crossing 或 jump 渲染行为。

## 10. 统一 Drawing Metrics 设计

### 10.1 推荐结构

建议在 Rendering.Wpf 内建立只读、集中式的 `DrawingMetrics`，由 LayoutFactory、SymbolDefinition、TerminalAnchorIndex 和 Label 计算共同使用：

```text
DrawingMetrics
├── RingCabinetMetrics
│   ├── IntervalWidth / IntervalHeight
│   ├── MainBusOffset
│   ├── IntervalSpacing
│   ├── CabinetNameAnchor
│   └── IntervalLabelAnchor
├── SwitchMetrics
│   ├── CircuitBreakerSize
│   ├── LoadSwitchSize
│   ├── IsolationSwitchSize
│   ├── GroundSwitchSize
│   ├── DropoutFuseSize
│   └── StateSpacing
├── PTMetrics
│   ├── CoilSize / CoilOverlap
│   └── LabelAnchor
├── PoleMetrics
│   ├── CircleDiameter
│   ├── AttachmentOffsets
│   └── PoleNumberAnchor
├── CableTerminationMetrics
│   ├── TriangleWidth / TriangleHeight
│   └── CableSideAnchor / OverheadSideAnchor
├── LineMetrics
│   ├── ConductorThickness
│   ├── OverheadStrokeStyle
│   └── CableStrokeStyle
├── TerminalMetrics
│   ├── VisibleMarkerSize
│   └── PickTolerance
├── LabelMetrics
│   ├── FontSizes
│   └── StandardOffsets
└── CrossingMetrics
    └── LineJumpRadius   （预留，不实现）
```

### 10.2 使用原则

1. Metrics 只属于 Rendering/Layout，不进入 Domain。
2. 使用毫米文档坐标，不使用屏幕像素。
3. 默认 Metrics 是唯一事实源；Renderer 不再各自保存相同尺寸常量。
4. RuntimeLayout 仍保存实际布局值；Metrics 只用于创建／重建标准布局和图元内部几何。
5. `TerminalAnchorIndex` 必须调用与图元相同的 Metrics 计算锚点，避免画面与连线端点漂移。
6. 不引入主题系统或用户可配置尺寸；当前只需要一个经业务验收的基线。
7. Word 未给出绝对值，所有初始数值都必须标记为实现候选，并通过 Windows 对照截图冻结。

### 10.3 待冻结指标

| 指标 | Word 给出的关系 | 当前决定 |
| --- | --- | --- |
| 间隔宽／高 | 间隔等距、垂直展开 | 视觉关系明确，但绝对尺寸待定义 |
| 母线位置 | 名称下方、间隔上方、水平 | 视觉关系明确，但绝对尺寸待定义 |
| Switch / GroundSwitch 尺寸 | 各专业符号相对主回路可辨识 | 视觉关系明确，但绝对尺寸待定义 |
| PT 尺寸 | 双圆线圈，位于 PT 支路 | 视觉关系明确，但绝对尺寸待定义 |
| Pole 圆圈 | 架空线穿过圆圈附近 | 视觉关系明确，但绝对尺寸待定义 |
| CableTermination 三角形 | 环网柜下方或杆塔圆圈上方 | 视觉关系明确，但绝对尺寸待定义 |
| 线宽／虚线节奏 | 架空实线、电缆虚线 | 线型明确，绝对线宽与 dash pattern 待定义 |
| Label / Terminal anchor | 与对应设备邻近并避免压线 | 视觉关系明确，但绝对偏移待定义 |
| Line Jump 半径 | Word 未给出 | 仅预留字段，E-1 不实现 |

## 11. Phase E-1 实施拆分

### E-1A — Drawing Metrics 与 Scene primitive 基础

范围：

- 建立单一默认 `DrawingMetrics`；
- 增加最小 Ellipse、Polyline/Path 和 StrokeStyle 支持；
- 让 Scene renderer 和 bounds calculator 支持新 primitive；
- 为 Cable 虚线提供能力，但暂不大规模重绘业务图元；
- 预留但不使用 LineJumpRadius。

验收标准：

- 新 primitive 的 Geometry 和 bounds 测试通过；
- Solid/Dash Pen 映射测试通过；
- 原有 Scene 仍可构建；
- Domain、RuntimeLayout、Persistence 无新增 View 类型或 Metrics 字段；
- Renderer 中不再为新代码引入重复尺寸常量。

### E-1B — 普通 RingCabinet 图元校准

范围：

- 去除 RingCabinet 外围整体矩形；
- 校准居中名称、水平母线和等宽间隔；
- 校准普通负荷开关、接地刀闸和出线电缆终端三角形；
- 保持 Selection、TerminalAnchor、Cable endpoint 和现有 Command 行为。

验收标准：

- 3/4/5/6 间隔普通环网柜均无外围整体框；
- 名称、母线、分支和编号位置与 Word 关系一致；
- Open/Closed 产生对应专业符号；
- 每个外部 Terminal 的线端点落在可见终端锚点；
- Save/Open、Undo/Redo 不因视觉修改改变 Stable IDs 或拓扑。

### E-1C — IntegratedFeeder、三工位与 PT 图元

范围：

- 按 `GroundingStructureKind` 校准三工位组合；
- 用已有开关状态绘制运行、拉开、接地位置；
- 把 PT 改为双圆线圈；
- 校准 PT、隔离刀闸和接地支路相对位置。

验收标准：

- 上刀上接地的三种状态均与 Word 对照图可辨识；
- 下刀下接地的三种状态均与 Word 对照图可辨识；
- 已支持的第三种 `GroundingStructureKind` 保持 Domain/Layout 一致并有明确图形回归；
- PT 不再使用矩形占位图；
- Switch operation 后 Scene 立即使用正确状态几何；
- 不新增单一三值 Switch Device。

### E-1D — Pole、PoleAttachment 与 CableTermination 图元

范围：

- 杆塔主体改为圆圈；
- 校准隔离开关、断路器、负荷开关、跌落式熔断器附件；
- 电缆终端改为三角形并与杆塔圆圈组合；
- 调整 Pole/Attachment Layout 与 TerminalAnchor。

验收标准：

- 普通杆、四类开关杆和电缆终端杆可分别构建；
- LoadSwitch/DropoutFuse 不再被 `MixedPoleRenderer` 拒绝；
- Attachment 仍通过 PoleAttachment 关联，不出现悬空设备；
- 架空线和电缆端点落在正确 Terminal anchor；
- 架空变压器明确显示为“未实现／等待独立 Domain 设计”，不得用假对象通过验收。

### E-1E — 线路线型校准

范围：

- OverheadLine 使用 Solid stroke；
- Cable 使用 Dash stroke；
- 正式 Scene 与 Cable/OverheadLine preview 使用同一线型合同；
- 保持现有端点、Selection 和 HitTest 语义。

验收标准：

- 同图中的架空线与电缆无需文字即可由线型区分；
- Zoom/Pan 后 dash 显示稳定；
- Cable 标签、选择和 reconnect picking 正常；
- 不产生正交折点，不引入 Routing、Snap、避让、Crossing 或 Line Jump。

### 11.1 推荐顺序

执行顺序为：

```text
E-1A → E-1B → E-1C → E-1D → E-1E
```

E-1A 是后续全部图元的共同依赖。E-1B 先冻结环网柜公共骨架，E-1C 再在同一骨架上完成复杂间隔；E-1D 校准独立的杆塔组合；E-1E 最后统一正式线路和 Preview 线型。正交 Routing、Snap、Alignment、避让、Crossing Detection 和 Line Jump 不进入上述任何子阶段。

## 12. Windows 截图验收清单

每个子阶段在 Windows/WPF 实机至少保留以下对照截图：

1. 3、4、5、6 间隔普通环网柜，显示名称、母线、等宽分支和电缆终端。
2. 普通负荷开关三种组合：运行、拉开、接地。
3. 上刀上接地：运行、拉开、接地三张截图。
4. 下刀下接地：运行、拉开、接地三张截图。
5. PT 间隔与相邻普通／融合间隔组合截图。
6. 普通杆、隔离开关杆、断路器杆、负荷开关杆、跌落式熔断器杆。
7. 电缆终端杆，清楚显示圆圈 + 三角形以及两侧连线。
8. 同一画面中的架空实线和电缆虚线。
9. 选中态、Switch 操作后状态、Cable preview、OverheadLine preview。
10. 100%、放大、缩小三个 Zoom 级别下的线宽、虚线节奏和文字可读性。
11. Pan、Fit、窗口缩放后图元仍受 E-0B viewport clipping 约束。
12. Save/Open、Undo/Redo 后相同对象的几何、状态和 Terminal endpoint 不漂移。

截图验收只确认视觉和交互回归，不用截图替代 Domain、Persistence 和 Stable ID 自动测试。

## 13. 本审计结论

1. 当前 RingCabinet、SwitchAssembly、PoleAttachment、CableTermination 和 TerminalAnchor 架构可以继续复用，不需要为真实图元建立第二套模型。
2. 已有 MVP 对象的主要差异属于 Rendering Geometry、Layout 和 Scene primitive，不是 Domain 重建问题。
3. 普通／三工位开关状态事实基本充足；最大问题是 Rendering 没有表达已有 SwitchKind、SwitchState、GroundingStructureKind 和组合状态。
4. Transformer、运行／检修位置、在运／拆除和独立站内设备是明确的 Domain／产品范围缺口，但不应被 E-1 图元校准顺手实现。
5. E-1 必须先完成 Metrics 和 primitive 基础，再按普通环网柜、复杂间隔、杆塔组合、线路线型逐步实施。
6. Phase E-1 审计已完成；任何真实图元实现仍未开始，等待本审计确认后进入 E-1A。
