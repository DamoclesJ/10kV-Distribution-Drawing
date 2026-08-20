# Phase E-1D — Pole / PoleAttachment / CableTermination Professional Symbols

> 状态：**Implemented / Pending Windows Validation**
> 实施日期：2026-08-19
> 基线：`c43f8b4`
> 视觉依据：`配电专业附图图元.docx`

## 1. 范围

本阶段把水泥杆、四类柱上开关和杆上电缆终端的旧占位图元替换为 Word 参考中的专业视觉语义，并同步标准 Layout、TerminalAnchor、Selection/HitTest 和测试。

没有修改 Domain、Topology、Stable ID、CommandStack、Persistence、Cable/OverheadLine 线型或环网柜 E-1B 图元；没有开始 E-1E、Routing、Snap、Avoidance、Crossing Detection 或 Line Jump。

## 2. Word → Domain → Scene 映射

| Word 图元 | 当前 Domain | Scene 投影 |
| --- | --- | --- |
| 水泥杆 | `Pole(PoleType.Cement)` | 单个空心 `SceneEllipse` |
| 柱上断路器 | `SwitchDevice(CircuitBreaker, Pole)` | 外框、线路入口/出口、刀片和叉形触点 |
| 柱上负荷开关 | `SwitchDevice(LoadSwitch, Pole)` | 外框、圆形接点、刀片和固定触点 |
| 柱上隔离开关 | `SwitchDevice(IsolationSwitch, Pole)` | 无外框的水平刀闸断口／导通线 |
| 跌落式熔断器 | `SwitchDevice(DropoutFuse, Pole)` | 竖向熔管、跌落偏移和操作箭头 |
| 电缆终端杆 | `Pole + PoleAttachment + CableTermination` | 相互独立的空心圆与闭合三角形 |

四种柱上 Switch 使用独立柱上几何，不复用 E-1B 环网柜内部开关图元。Open/Closed 继续直接来自 `SwitchDevice.SwitchState`；几何本身随状态改变，既有“分／合”状态文字继续保留。

Word 中的架空变压器 R45304、用户架空变台、独立配电变压器、站用变、电抗器、电容器、低压分支箱、设备生命周期状态、出线断路器间隔运行/检修位置和自由站内电缆头没有当前 Domain 对象，本阶段不实现。

## 3. Drawing Metrics 与 Layout

`DrawingMetrics.Default` 增加本阶段实际使用的工程比例：

- Pole radius 和 Pole label offset；
- PoleAttachment 默认宽高、label offset、内部 inset、触点尺寸、刀片比例；
- DropoutFuse 熔管宽度、inset、跌落偏移和操作箭头长度；
- CableTermination triangle width/height 和 logical hit padding。

`PoleLayout` 默认尺寸由 Pole radius 派生为圆形直径，`AttachmentLayout` 默认宽高和 label offset 由 PoleAttachment metrics 派生。显式恢复的 RuntimeLayout 尺寸仍被保留；没有把新增图元细节写入 Persistence。

Word 没有规定绝对毫米、线宽或字号。上述值是项目第一版工程绘图比例基线，不是行业标准，仍需 Windows 截图校准。

## 4. Geometry 与 Renderer

- `PoleSymbolDefinition` 不再生成旧竖线和顶部横线，只生成一个专业圆和逻辑边界；
- `CircuitBreaker`、`LoadSwitch`、`IsolationSwitch`、`DropoutFuse` 分别投影独立几何；
- `CableTerminationSymbolDefinition` 不再生成矩形，改为三点闭合 `ScenePolyline`；
- `MixedPoleRenderer` 和 `SwitchAttachmentRenderer` 正式允许四类合法柱上开关，未知类型仍拒绝；
- `SymbolLibrary` 继续作为统一 Symbol 路径，并用专业端点连接 Pole 与 Attachment；
- `SceneLogicalBounds` 只参与 Scene extent，不绘制透明占位框。

## 5. TerminalAnchor

`TerminalAnchorIndex` 仍是唯一视觉端子坐标索引。`PoleProfessionalGeometry` 同时服务 Symbol 和 Anchor，避免两套坐标公式漂移：

- Pole overhead anchor 位于圆形杆塔线路中心；
- CircuitBreaker、LoadSwitch、IsolationSwitch 的两个 terminal 位于图元左右入口/出口；
- DropoutFuse 的两个 terminal 位于竖向图元上下入口/出口；
- CableTermination CableSide 位于三角形外侧 apex；
- CableTermination OverheadSide 位于靠 Pole 的三角形底边中心。

移动 Pole 后，所有 anchor 由最新 `PoleLayout + AttachmentLayout` 重新计算。TerminalId、Connection、CableSegment、OverheadLine 和 RingCabinet anchor 合同均未改变。

## 6. Selection / HitTest / Label

- Pole hit bounds 使用专业圆的 logical bounds，目标仍为 `Pole.Id`；
- PoleAttachment hit bounds 使用对应专业图元 logical bounds，目标仍为 `AttachmentId`；
- 柱上 Switch 在保留 PoleAttachment target 的同时，以更高优先级映射原 `SwitchDevice.Id`；
- CableTermination 继续保持既有 Attachment 身份合同；
- 没有透明矩形维持选择；
- `MixedPoleRenderer → LabelRequest → LabelLayoutEngine` 保持唯一业务标签路径；Pole、Switch Attachment、CableTermination 各生成一次标签；
- Switch “分／合”继续是状态文字，不作为业务名称标签重复布局。

## 7. Cable / OverheadLine 边界

本阶段只改变 terminal 的视觉坐标。Cable 和 OverheadLine 仍以稳定 TerminalId 和 Connection 建立拓扑，线路样式和路径不变。

现有 Cable create/delete/reconnect、OverheadLine create、Undo/Redo 和 Save/Open 的 Domain/Command/Persistence 逻辑没有修改。Windows 需验证移动 Pole 后 CableSide、OverheadSide 和 Pole overhead 线路端点跟随新的 anchor。

## 8. 自动测试

Scene structure 和 geometry 测试覆盖：

- Pole 单圆、无旧竖杆、标签和 logical bounds；
- 四类柱上 Switch 均可渲染且 geometry 可区分；
- 四类 Open/Closed geometry 与“分／合”状态文字；
- LoadSwitch、DropoutFuse 不再被 Renderer 拒绝；
- CableTermination 闭合三角形、无旧矩形、Pole + triangle 组合；
- Switch 两端 anchor 分离；
- CableTermination CableSide/OverheadSide anchor 分离；
- Pole move 后 anchor 更新；
- Pole、PoleAttachment、SwitchDevice Stable ID hit target；
- MixedPole label 确定性和无重复；
- DrawingMetrics 仍不依赖 Domain，也不进入 RuntimeLayout。

既有 Domain、Application、Infrastructure、Desktop 和 Rendering tests 继续承担 Switch Command undo/redo、Cable create/reconnect/delete、OverheadLine、Persistence 和完整场景回归。

## 9. Windows 验收清单

1. 水泥杆显示为空心圆，无旧竖杆；
2. 45300002 隔离开关杆与 Word 组合关系一致；
3. 45300003 断路器杆与 Word 组合关系一致；
4. 45300004 跌落式熔断器杆与 Word 组合关系一致；
5. 柱上负荷开关与断路器视觉明显不同；
6. 四类开关双击／Inspector 分合后几何更新；
7. Undo/Redo 后状态几何恢复；
8. 电缆终端杆显示为“圆圈 + 三角形”；
9. Pole、Attachment、具体 Switch 和 CableTermination 可按原身份选择；
10. Pole、Attachment、CableTermination 标签各一次；
11. Cable 创建、删除、重连使用 CableSide，OverheadSide 仍拒绝 Cable；
12. OverheadLine 使用 Pole overhead／CableTermination OverheadSide；
13. Move Pole 后 Cable 与 OverheadLine 端点跟随；
14. Save/Open 后 Stable IDs 和 topology 不变；
15. Fit/viewport 包含专业几何且不依赖透明矩形。

## 10. 当前验证结果

macOS 当前验证：

- `DistributionDrawing.sln` build：成功，0 errors；2 个 warning 均为离线环境无法读取 NuGet vulnerability source 的 `NU1900`，不是源码 warning；
- `DistributionDrawing.Rendering.Wpf.Tests` build：成功，0 errors / 0 warnings；
- `DistributionDrawing.Desktop.Tests` build：成功，0 errors；包含 3 个既有 xUnit analyzer warning 和 1 个 `NU1900`；
- Domain／Infrastructure 测试程序集成功生成，但本机受控环境禁止 TestHost 建立回环通信，测试运行在断言前中止；
- Rendering.Wpf.Tests／Desktop.Tests 在 macOS 仍不能启动 Windows Desktop TestHost；
- Application.Tests 仍由未修改的 `RingCabinetTemplateDomainBuilderTests.cs` 两个既有 `SwitchKind` 未解析错误阻断；
- `git diff --check`：通过。

上述限制没有通过修改 TargetFramework、Domain 或无关测试规避。自动测试实际执行和视觉验收必须在 Windows 完成。

本阶段最终状态：**Implemented / Pending Windows Validation**。
