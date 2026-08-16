# Phase D-1 Interactive Switch Operation

## 目标

让图面中的具体 `SwitchDevice` 可以通过双击或 Inspector 明确执行分闸/合闸，并沿用现有 Domain 状态和 interlock。

## 交互设计

- 单击开关：只选择具体 `SwitchDeviceId`，不改变状态。
- 双击具体开关图元：切换当前 `SwitchState`。
- 双击 Interval 或 RingCabinet：不触发开关操作。
- Inspector 选中 SwitchDevice 后显示当前状态及一个中文操作按钮：当前合时显示“分闸”，当前分时显示“合闸”。
- 双击和 Inspector 按钮共用同一个 Desktop Controller。

## Controller 与命令链

```text
Switch primitive HitTest
  -> SelectionReference(Device, SwitchDeviceId, IntervalId)
  -> SwitchOperationController
  -> Application ChangeSwitchStateCommand
  -> Rendering CommandStack adapter
  -> DrawingDocument.ChangeSwitchState
  -> SwitchAssembly interlock
  -> ProjectRuntimeSession.RebuildScene
  -> Selection / Inspector refresh
```

`ChangeSwitchStateCommand` 原本属于 Application 层，未实现 Rendering 层的 `ICommand`。本阶段只增加 Desktop 边界适配器，使其进入现有 `CommandStack`，没有复制状态或闭锁模型。

## Interlock 与中文反馈

Controller 不判断开关组合是否合法，全部交给 `DrawingDocument` / `SwitchAssembly`。操作失败时，命令尚未进入 CommandStack 历史，Domain 和 Scene 不改变，Selection 保持。

当前 Domain 已有稳定的规则代码，Desktop 对已确认规则做最小映射：

- `LS-GS-MUTUAL-EXCLUSION`：负荷开关与接地刀闸不能同时合闸。
- `IF-IS-GS-MUTUAL-EXCLUSION`：隔离开关与接地刀闸不能同时合闸。
- 其他闭锁失败：当前开关操作不符合设备闭锁条件。

未知技术异常不直接向用户展示英文 `exception.Message`，而返回通用中文失败提示。完整全局消息本地化不属于本阶段。

## Selection、Scene 与 Undo/Redo

成功操作后保留原 `SelectionReference`，按最新 Domain 状态重建 Scene，现有符号重新生成合/分表达。失败时不重建伪状态、不清空 Selection。

Switch 操作使用同一个 `CommandStack`，因此：

```text
Execute -> Undo -> Redo
```

分别恢复原状态、目标状态和首次 Execute 的目标状态，不建立独立的开关 Undo 系统。

## Windows 验收要求

Windows 环境需验证：

- LoadSwitch、GroundSwitch、IsolationSwitch、CircuitBreaker 的双击和 Inspector 操作；
- 合法操作后状态文字、图形、Inspector 和 Selection 同步；
- LoadSwitch/IntegratedFeeder 闭锁拒绝时状态、Scene、Selection、Dirty、CommandStack 均不变，且提示为中文；
- Undo/Redo 后状态和 Scene 正确；
- PT 中存在的开关复用同一操作链；
- win-x64 EXE 实际双击操作。

Mac 环境仅用于项目编译和可运行的非 WPF 测试；WPF TestHost 的 Windows Desktop Runtime 限制仍需在 Windows 验证。
