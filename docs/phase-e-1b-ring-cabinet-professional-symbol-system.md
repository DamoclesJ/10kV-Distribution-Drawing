# Phase E-1B — Ring Cabinet Professional Symbol System

> 状态：**Implemented / Pending Windows Validation**  
> 实施日期：2026-08-19  
> 基线：`ee5fb6a`  
> 依据：`docs/phase-e-1-real-symbol-baseline-audit.md`、`配电专业附图图元.docx`

## 1. 目标与范围

本阶段合并原计划 E-1B 与 E-1C，在不修改 Domain、Persistence、RingCabinet Template、Switch 操作链和 Cable/OverheadLine 业务线型的前提下，把已有环网柜占位视觉替换为专业电气图元系统。

本阶段实现：

- 普通负荷开关环网柜专业视觉；
- 一二次融合环网柜专业视觉；
- LoadSwitch、IsolationSwitch、CircuitBreaker、GroundSwitch 的状态几何；
- 上刀／下刀及接地支路相对位置映射；
- PT 双圆线圈与 `PT` 标签；
- 连续母线、等宽间隔和居中柜名；
- 电缆终端向下三角形；
- 无可见柜体／间隔外框情况下的逻辑边界、选择和 Fit 范围；
- TerminalAnchor 与新视觉终端位置同步。

本阶段没有实现 Pole、PoleAttachment、独立 CableTermination 专项重画、Cable 虚线、OverheadLine 改造、Routing、Snap、Alignment、Avoidance、Crossing Detection、Line Jump、DTU、JPG 或 Printing。

## 2. 原问题

实施前的环网柜 Rendering 存在以下问题：

1. `RingCabinetSymbol` 使用可见大矩形表达整个柜体；
2. 每个 Interval 再使用可见大矩形表达柜格；
3. 不同 `SwitchKind` 全部复用“矩形 + 斜线／横线 + 分／合文字”的占位符；
4. 普通负荷开关间隔没有专业接地支路和电缆终端三角形；
5. 一二次融合间隔已有三台开关和结构类型，但只显示三个通用矩形；
6. PT 使用矩形加 `PT` 文字，而不是双圆线圈；
7. Interval 分支起点与 Cabinet 母线 Y 坐标没有统一投影合同；
8. IntegratedFeeder 的外部 TerminalAnchor 使用旧矩形端子上边缘，和目标三角形端点不一致；
9. 柜名 Label 的 Center alignment 计算结果没有按左上绘制原点转换，视觉上不能可靠居中。

## 3. Domain state → professional visual state

### 3.1 普通负荷开关间隔

Domain 继续使用两个独立 `SwitchDevice`：

| Domain 事实 | 专业视觉 |
| --- | --- |
| `LoadSwitch + Open` | 垂直主回路两接点，刀片偏离另一接点 |
| `LoadSwitch + Closed` | 垂直主回路两接点，刀片接通两接点 |
| `GroundSwitch + Open` | 水平接地支路两接点，刀片偏离接点 |
| `GroundSwitch + Closed` | 水平接地支路两接点，刀片接通至接地符号 |

`SwitchAssembly` 的既有互锁仍决定可否操作：

- LoadSwitch Closed + GroundSwitch Open：运行；
- LoadSwitch Open + GroundSwitch Open：拉开；
- LoadSwitch Open + GroundSwitch Closed：接地。

Rendering 不重新判断互锁，也不建立第二套操作状态。

### 3.2 一二次融合间隔

Domain 继续使用：

- `IsolationSwitch`；
- `CircuitBreaker`；
- `GroundSwitch`；
- 每台开关自己的 `SwitchState.Open/Closed`；
- `GroundingStructureKind`。

专业视觉映射如下：

| Domain 结构 | 视觉排列 |
| --- | --- |
| `UpperIsolationGrounding` | IsolationSwitch 位于 CircuitBreaker 上方，接地支路从上部连接段引出 |
| `UpperLowerGrounding` | IsolationSwitch 位于 CircuitBreaker 上方，接地支路从下部连接段引出 |
| `LowerLowerGrounding` | CircuitBreaker 位于 IsolationSwitch 上方，接地支路从下部连接段引出 |

IsolationSwitch 使用刀闸接点和刀片；CircuitBreaker 使用上下断口横线和状态连接线；GroundSwitch 使用水平刀闸、接地引线和三级接地横线。三台设备各自的 Open/Closed 直接决定刀片或断路器连接线位置。

Word 中的运行、拉开和接地组合可以由已有三台开关状态表达，Rendering 只投影这些状态。当前 `IntegratedFeeder` 的 `SwitchAssembly.Evaluate().OperationalState` 没有完整运行方式映射规则，通常返回 `Unclassified`；这是派生语义完整度问题，但不阻塞真实开关组合几何。本阶段没有向 Domain 添加伪三值状态。

## 4. 统一视觉骨架

普通柜与一二次融合柜共享同一布局骨架：

```text
               环网柜名称
        ─────────────────  连续水平母线
           │       │       │
        等宽间隔  等宽间隔  等宽间隔
           │       │       │
        专业开关  专业开关   PT/接地
           │       │       │
           ▽       ▽       ▽  外部 Terminal
```

- Cabinet Name 的 Label anchor 位于逻辑柜体水平中心；
- Cabinet Name 的 Center 结果转换为 `SceneText` 左上绘制原点；既有 Interval／Switch Label 定位保持不变；
- Busbar 从第一个 Interval 左边界连续延伸到最后一个 Interval 右边界；
- 所有 Interval 使用统一宽度、高度与间距；
- 分支起点使用实际 Cabinet busbar Y，而不是 Interval 矩形上边界；
- 外部电缆终端使用向下闭合三角形；
- 3/4/5/6 间隔普通柜和 4/6 间隔融合柜使用同一尺寸体系；
- PT 仍是同一水平排列中的特殊 Interval。

## 5. PT 专业图元

PT Interval 继续使用 E-0A 已建立的正式 Domain 与 `PTSymbolPosition` Layout 输入。本阶段只修改视觉投影：

- IsolationSwitch 从连续母线向下连接；
- PT 使用两个相交的 `SceneEllipse`；
- 两个线圈使用同一 `CoilRadius` 和 `CoilSpacing`；
- `PT` 标签位于线圈侧方；
- GroundSwitch 从 IsolationSwitch 后的 circuit node 引出；
- 外部 Terminal 使用向下三角形；
- 不再绘制 PT 矩形占位框。

`PTSymbolPosition` 仍是标准 Layout 的确定性派生值，没有进入 Persistence 格式。新建布局把 PT 线圈水平中心对齐标准间隔主分支；旧工程若保存了旧 RuntimeLayout，则按其已有位置投影，不替换 Stable IDs。

## 6. 可见几何与逻辑边界

本阶段删除了 RingCabinet 和 Interval 的可见矩形，不使用透明矩形模拟专业图元。

新增 `SceneLogicalBounds`：

- 只保存 `DocumentRect`；
- `DrawingSceneRenderer` 不绘制它；
- `DrawingSceneBoundsCalculator` 把它纳入 Fit／Scene extent；
- Selection 仍由现有 `SelectionHitTestIndex` 负责；
- 不进入 Domain、RuntimeLayout 或 Persistence。

因此可见专业几何与逻辑交互范围明确分离。RingCabinet move、Fit 和 cabinet/interval/switch selection 不依赖外围矩形存在。

## 7. Drawing Metrics 与 Layout

本阶段继续使用 E-1A `DrawingMetrics.Default`，并补充：

- `RingCabinet.CabinetPadding`；
- `RingCabinet.DeviceVerticalSpacing`；
- `Switch.LogicalHitHeight`。

`RingCabinetLayoutFactory` 的以下默认值已改为从 Metrics 派生：

- Cabinet padding 和整体高度；
- Interval width、height、spacing；
- Busbar offset；
- 主开关与接地开关横向位置；
- 上部／下部设备纵向位置；
- Switch length 和 logical hit height；
- PT 水平中心。

专业 Symbol 共用 Metrics 中的线宽、接点半径、PT 线圈尺寸、CableTermination 三角形尺寸和字号。Word 没有给出绝对毫米尺寸，这些数值仍是第一版工程绘图比例基线，不是行业标准。

## 8. TerminalAnchor

`TerminalId = topology truth` 合同没有变化。本阶段只调整视觉投影：

- 普通间隔和融合间隔 ExternalTerminal anchor 位于间隔中心线的三角形下端 tip；
- PT ExternalTerminal anchor 位于 PT 线圈中心线的三角形下端 tip；
- Y 坐标统一为 Interval logical bottom；
- 不改变 TerminalId、Connection、ElectricalNode 或 Cable topology。

连接到旧 IntegratedFeeder 矩形端子上边缘的视觉锚点因此移动到新的三角形端点；Cable 仍由既有 TerminalId 自动跟随新投影。

## 9. Selection、HitTest 与 Switch Operation

`DrawingSceneBuilder` 现有 hit-test 架构保持不变：

- RingCabinet logical bounds：priority 10；
- Interval logical bounds：priority 20；
- 每个 Switch layout bounds：priority 40。

因此仍可分别选择 Cabinet、Interval 和具体 SwitchDevice。PT 至少保持 Interval selection，其 IsolationSwitch 和 GroundSwitch 也继续使用现有 Switch layout hit target。

Phase D-1 操作链没有变化：

```text
SwitchOperationController
→ ChangeSwitchStateCommand
→ CommandStack
→ DrawingDocument / Domain SwitchAssembly interlock
→ RebuildScene
→ professional geometry reflects current SwitchState
```

Undo/Redo 只恢复 Domain 状态；Scene 仍由相同 Stable IDs 和 RuntimeLayout 重建，不保存 Rendering 状态。

## 10. 测试覆盖

新增或调整的 Scene／结构测试覆盖：

1. 3/4/5/6 间隔普通柜统一宽度；
2. 4/6 间隔融合柜统一尺寸体系；
3. 连续母线起止位置；
4. Cabinet Name anchor 居中；
5. 无 Cabinet／Interval 可见矩形；
6. `SceneLogicalBounds` 进入 Scene bounds；
7. LoadSwitch Open/Closed 几何差异；
8. GroundSwitch Open/Closed 几何差异；
9. Interlock 拒绝操作后 Scene 几何保持不变；
10. CircuitBreaker Open/Closed 几何差异；
11. IsolationSwitch Open/Closed 几何差异；
12. 三种 `GroundingStructureKind` 的上下设备顺序；
13. 运行／拉开／接地组合几何互不相同；
14. PT 两个等尺寸线圈和 `PT` 标签；
15. PT 与 busbar 分支连接；
16. Cabinet、Interval 和全部 Switch 的 selection target；
17. 普通／融合／PT ExternalTerminal anchor 与三角形 tip 一致；
18. Switch Command execute/undo/redo 后几何恢复；
19. Switch Stable ID 不变；
20. 既有 RingCabinet Renderer 状态与业务编号测试适配专业 geometry；
21. Drawing Metrics 新字段稳定性。

既有测试继续承担以下回归：

- Domain interlock；
- Desktop `SwitchOperationController`、CommandStack、Undo/Redo；
- Cable topology 与场景构建；
- PT Domain／Application／Persistence V6 round-trip；
- Interval Type Change 的 Domain/Layout/Undo/Redo；
- Project Save/Open。

自动断言以 Scene structure、Document geometry、Stable ID 和 HitTest contract 为主，没有新增脆弱的像素截图断言。

## 11. 验证状态

macOS 当前验证结果：

- `DistributionDrawing.sln` build：成功，0 warning / 0 error；
- `DistributionDrawing.Rendering.Wpf` build：成功，0 error；独立构建显示 22 个既有 nullable warning；
- `DistributionDrawing.Rendering.Wpf.Tests` build：成功，0 warning / 0 error；
- `DistributionDrawing.Desktop` build：成功，0 warning / 0 error；
- `DistributionDrawing.Desktop.Tests` build：成功，0 error，3 个既有 xUnit analyzer warning；
- Domain Tests：55/55 通过；
- Infrastructure Tests：50/50 通过，包含 PT V6 round-trip 与工程持久化回归；
- Rendering.Wpf.Tests TestHost：因 macOS 缺少 `Microsoft.WindowsDesktop.App 10.0.0` 中止，没有进入测试断言；
- Application.Tests 额外构建检查：在未修改的 `RingCabinetTemplateDomainBuilderTests.cs` 中发现两个既有 `SwitchKind` 未解析错误；该测试项目不在 solution build 内，本阶段没有越界修改该历史问题。

WPF 测试程序集可以在 macOS 编译，但 TestHost 运行需要 Windows `Microsoft.WindowsDesktop.App 10.0.0`。上述 macOS 结果不是 Windows 视觉或交互验收。

本阶段最终状态为：**Implemented / Pending Windows Validation**。

## 12. Windows 截图与交互验收清单

Windows 必须人工检查：

1. 普通 3 间隔柜；
2. 普通 6 间隔柜；
3. 融合 4 间隔柜；
4. 融合 6 间隔柜；
5. 含 PT 的融合柜；
6. LoadSwitch 分／合几何；
7. GroundSwitch 分／合几何；
8. CircuitBreaker 分／合几何；
9. Isolation/Ground interlock；
10. 上刀上接地结构；
11. 下刀下接地结构；
12. 三工位运行组合；
13. 三工位拉开组合；
14. 三工位接地组合；
15. PT 双圆、标签、支路和终端；
16. Cabinet/Interval/Switch 独立选择；
17. Cable 接入后锚点和跟线；
18. Switch 双击与 Inspector 操作；
19. Undo/Redo 后几何与选择；
20. Save/Open 后 Stable ID、状态、布局和视觉；
21. Move 与 Fit 在无可见外框时正常；
22. 3/4/5/6 间隔比例、名称居中和 Label 可读性。

## 13. 后续明确不包含内容

原计划 E-1B 与 E-1C 已在本阶段合并。后续 E-1 只继续处理既定独立范围：

- Pole、PoleAttachment 和独立 CableTermination 图元；
- OverheadLine 实线与 Cable 虚线业务切换。

Orthogonal Routing、Snap、Alignment、Avoidance、Crossing Detection 和 Line Jump 不属于 E-1B，也不得从本阶段自动开始。
