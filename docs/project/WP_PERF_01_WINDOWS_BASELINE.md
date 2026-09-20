# WP-PERF-01 Windows Diagnostic Baseline

## 状态与边界

- WP-PERF-01A Characterization & Audit：Accepted。
- WP-PERF-01B Windows Diagnostic Baseline Preparation：In Progress，等待 Windows 测试与采集。
- WP-PERF-01C 性能优化：未授权。
- `FormatVersion = V7`；诊断数据不写入 `.kvdrawing`。
- 接近 60 FPS 是设计目标，不是冻结门槛。本文件不冻结 FPS、延迟、内存、PNG DPI、可读性或安全预算。

本诊断只观察现有行为，不改变 MouseMove 频率、候选合法性、`LastValid`、expected-invalid rollback、group all-or-none、`RouteFamilyKey` / hysteresis、`CableRouteGuide` 优先级、route continuity、MouseUp 最终位置、CommandStack 或 Undo / Redo。`VisualCollectionPublish` 仅表示 UI 线程完成视觉对象发布，不表示该画面已经由合成器显示。

## 诊断启用与关闭

诊断提供者为 `.NET EventSource`：`DistributionDrawing-Performance`。没有 EventListener 时采集默认关闭，普通启动即为诊断关闭。诊断记录进入外部 `.nettrace`，不会进入 V7 工程。

Windows PowerShell（仓库根目录）：

```powershell
git rev-parse HEAD
git status --short
dotnet --info
dotnet build .\src\DistributionDrawing.sln -c Release
dotnet test .\tests\DistributionDrawing.Domain.Tests\DistributionDrawing.Domain.Tests.csproj -c Release
dotnet test .\tests\DistributionDrawing.Application.Tests\DistributionDrawing.Application.Tests.csproj -c Release
dotnet test .\tests\DistributionDrawing.Infrastructure.Tests\DistributionDrawing.Infrastructure.Tests.csproj -c Release
dotnet test .\tests\DistributionDrawing.Rendering.Wpf.Tests\DistributionDrawing.Rendering.Wpf.Tests.csproj -c Release
dotnet test .\tests\DistributionDrawing.Desktop.Tests\DistributionDrawing.Desktop.Tests.csproj -c Release
dotnet tool install --global dotnet-trace
```

启动应用：

```powershell
.\src\DistributionDrawing.Desktop\bin\Release\net10.0-windows\DistributionDrawing.Desktop.exe
```

在另一 PowerShell 窗口中查找 PID 并开始采集：

```powershell
New-Item -ItemType Directory -Force .\artifacts\perf
dotnet-trace ps
dotnet-trace collect --process-id <PID> --providers DistributionDrawing-Performance:0xFFFFFFFFFFFFFFFF:4 --output .\artifacts\perf\<sample>-<case>-<run>.nettrace
```

执行一个规定手势后在采集窗口按 `Enter` 停止。诊断关闭对照组直接启动应用而不附加 `dotnet-trace`。若工具版本不接受上述 provider 三段式语法，先运行 `dotnet-trace collect --help`，保持 provider 名、全关键字与 Informational level 不变，并在回传记录中注明工具版本和实际命令。

## 事件与关联字段

| 事件 | 关键字段 | 含义 |
| --- | --- | --- |
| `GestureStart` | `gestureId`, `gestureKind`, `timestamp`, `stopwatchFrequency` | 一次 DeviceDrag、GroupDrag、CableRouteGuide 或 GroundingPoint 手势开始 |
| `Update` | `gestureId`, `updateId`, `outcome`, `timestamp`, `durationTicks` | 一次实际进入拖动处理的 MouseMove；结果为 Accepted、Rejected、Unchanged 或 UnexpectedFailure |
| `Phase` | `phaseName`, `gestureId`, `updateId`, `buildAttemptId`, `sceneBuildId`, `connectionId`, `attemptKind`, `timestamp`, `durationTicks`, `itemCount`, `secondaryCount`, `outcome` | 分阶段耗时与数量 |
| `GestureStop` | `gestureId`, `gestureKind`, `outcome`, `durationTicks`, accepted/rejected/unchanged counts | Commit、Cancel、LostCapture、SessionChanged 或失败结束 |

`attemptKind` 区分 `Candidate`、`Rollback`、`CommitBefore`、`CommitAfter` 和异常恢复。`connectionId` 仅对单 Connection 路由等适用阶段有值。`timestamp` 与 `durationTicks` 都使用 `Stopwatch` 计数器，毫秒值为 `durationTicks * 1000 / stopwatchFrequency`。

当前阶段名包括：`MouseMoveHandling`、`PreviewUpdate`、`ObstacleBuild`、`CableRouteRequestBuild`、`OverheadRouteRequestBuild`、`RoutingAll`、`ConnectionRoute`、`CrossingDetection`、`LineJumpProjection`、`ProfessionalSceneBuild`、`SceneBuild`、`InspectorSelectionRefresh`、`DrawingVisualBuild`、`VisualCollectionPublish`、`MouseUpCommit` 和 `GestureCancel`。

汇总时以 `(gestureId, updateId, buildAttemptId, sceneBuildId, connectionId)` 关联。`MouseMoveHandling`、`SceneBuild`、`RoutingAll` 是父计时；不得把父耗时与其子阶段相加。报告可以给出父计时总值，或在同一父范围内给出子阶段占比及未归因余量。失败 Candidate 与随后 Rollback 是不同 `buildAttemptId`，两者必须分别保留。

## 固定样本

优先样本是用户实际出现卡顿的合法 V7 工作票工程。它不得由合成数据冒充。若本次没有该文件，在 Windows 上通过“打开工程”选取实际文件，另存副本用于测量；原始业务文件不得提交到仓库，回传前按业务要求脱敏。记录文件 SHA-256：

```powershell
Get-FileHash .\path\real-sample.kvdrawing -Algorithm SHA256
```

另外在 Windows GUI 中建立、保存、关闭并重新打开以下两个受控 V7 工程。设备组合须服从真实工作票用途，不为凑类型创建无意义连接。正式采集前将最终实际数量、稳定 ID 和坐标填入同目录的采集记录；若合法性需要调整数量，须说明理由并冻结新指纹后再开始重复测试。

| 样本 | 固定构成目标 | 路由与专业对象目标 |
| --- | --- | --- |
| `perf-normal-v7.kvdrawing` | 4 个 RingCabinet、5 个 Pole、2 个 Transformer、2 个 CustomerStation | 8 条 Cable、4 条 OHL、4 个 GroundingAccessPoint、4 个 GroundingPoint、3 个 CableRouteGuide；至少一个 GAP 位于真实 OHL/Transformer 场景 |
| `perf-extended-v7.kvdrawing` | 6 个 RingCabinet、10 个 Pole、4 个 Transformer、3 个 CustomerStation | 16 条 Cable、8 条 OHL、6 个 GroundingAccessPoint、6 个 GroundingPoint、6 个 CableRouteGuide；包含多障碍绕行和多连接设备 |

每个受控工程必须通过 V7 Save → Close → Open，重新构建 Scene 后无错误，并记录：SHA-256、文件大小、FormatVersion、各 Device 子类数量、Connection/Cable/OHL/GAP/GroundingPoint/Guide 数量、被测对象稳定 ID、每条被测 ConnectionId、Guide 对应 CableId，以及测试前截图。当前仓库未包含用户真实业务文件，也未在非 Windows 环境伪造上述两个 GUI 工程；因此其最终文件、指纹和实际对象清单属于本 WP 的 Windows 待采集项。

## 固定测试操作

每个样本选择以下目标并记录起点、终点和中间折点（文档毫米坐标与屏幕像素坐标均记录）：

1. 单设备自由拖动：选择只有一条或少量连接的设备，在不触发改道的空白区域按固定水平轨迹拖动，MouseUp。
2. 多连接设备自由拖动：选择连接数最多的 RingCabinet / Station，在空白区域按相同长度轨迹拖动，MouseUp。
3. CableRouteGuide 自由拖动：拖动已有 Guide 的水平段，不穿过新障碍，MouseUp。
4. 单设备障碍改道：轨迹经过会使既有 Connection 必须换侧或换通道的位置，完成 valid → invalid → valid。
5. 多连接设备障碍改道：同上，但目标至少影响三条 Connection，用于观察全量路由与回滚额外成本。
6. CableRouteGuide 障碍改道：让 Guide candidate 依次经历合法、expected-invalid、再次合法位置，再 MouseUp。
7. invalid → MouseUp：从 `LastValid` 进入 expected-invalid 后直接松开，验证最终提交仍为 `LastValid` 且无异常线路跳变。
8. Cancel：在至少一次 Accepted 更新后按现有取消方式结束，验证恢复 `Before`、无历史残留且诊断状态结束。

不要使用自动鼠标脚本替代首轮专业 GUI 操作。冻结轨迹后，后续优化前后必须使用同一工程指纹、对象 ID、起止坐标、折点顺序和大致持续时间。每项先预热 3 次，再采集至少 10 次；诊断关闭对照至少 5 次，诊断开启至少 10 次。每次只采一个手势，测试间执行 Undo 或重新打开工程以恢复相同前态，并核对 Scene 与连接结果。

## 环境与结果记录模板

```text
Application commit:
Working tree / build configuration:
Sample name / SHA-256 / bytes / FormatVersion:
Windows edition, version, build:
.NET SDK / WindowsDesktop runtime:
CPU / physical cores / logical cores:
RAM:
GPU / driver:
Storage model / connection:
Power mode / AC or battery:
Monitor model / resolution / refresh rate / HDR:
Windows scaling / app window size:
Remote desktop or local console:
dotnet-trace version and exact command:
Diagnostic on/off:
Device / Connection / Cable / OHL / GAP / GroundingPoint / Guide counts:
Target stable ID / connected ConnectionIds:
Start / waypoints / end / duration:
Warm-up count / measured run count:
```

逐次结果至少导出为下列列集合（CSV 或等价表格）：

```text
sample,case,run,diagnostic,gestureId,gestureKind,gestureOutcome,
updateCount,acceptedCount,rejectedCount,unchangedCount,
mouseMoveMeanMs,mouseMoveP50Ms,mouseMoveP95Ms,mouseMoveP99Ms,mouseMoveMaxMs,
obstacleTotalMs,routeRequestTotalMs,routingAllTotalMs,connectionRouteMaxMs,
crossingTotalMs,lineJumpTotalMs,sceneBuildTotalMs,drawingVisualTotalMs,
visualPublishTotalMs,inspectorTotalMs,mouseUpCommitMs,
gestureDurationMs,longestGapMs,workingSetStartMiB,workingSetPeakMiB,gcHeapPeakMiB,notes
```

平均值与 P50/P95/P99 应从所有单次 Update 样本计算，不能先按手势平均后再计算尾延迟。`longestGapMs` 使用相邻 Update 时间戳差值，仅代表处理/输入观察间隔，不能直接称为显示帧间隔。内存可并行使用 `dotnet-counters monitor --process-id <PID> System.Runtime` 或 Windows Performance Recorder，记录 Working Set、GC Heap、分配率和 Gen 2/LOH 变化；明确标注工具与采样间隔。

若要报告实际显示帧率、present 间隔或 compositor stall，应另行使用获批的 Windows ETW/WPR GPU/Present 轨迹或等价的显示呈现工具，并把它与 `gestureId` 时间窗对齐。`CompositionTarget.Rendering`、`Show()` 返回或 `VisualCollectionPublish` 单独都不足以证明画面已显示。

## 正确性对照与回传

每次测试后保存必要截图或录屏，并核对 Electrical Model、Cable/OHL 合法性、GroundingPoint/GAP、Guide 优先级、route family continuity、group all-or-none、expected-invalid rollback、MouseUp `LastValid`、Undo/Redo 与 Save/Open。优化前后比较必须同时比较：工程指纹/前态、最终布局坐标、Connection endpoints、Guide 值、Scene route points / family（可获得时）、MouseUp 画面、Undo 后 Before、Redo 后 After。Rendering 不得创建或修改 Domain 事实。

回传包应包含：环境模板、样本清单与 SHA-256、轨迹表、`.nettrace` 原始文件、从事件导出的表格、诊断开/关对照、WPR/Present 原始轨迹（若采集）、截图/录屏、完整测试输出和异常说明。不要回传未脱敏业务内容。Windows 采集通过之前，WP-PERF-01B 状态保持“待 Windows 采集/验证”，不得据此开始 WP-PERF-01C。
