# Phase D-2-B Cable Property Editing

## Scope

本阶段为已选中的 `CableSegment` 增加电缆型号和长度编辑。起点端子、终点端子、连接标识以及所有 Stable ID 均保持只读；电缆重连、分割和路径编辑不在范围内。

## Inspector fields

Inspector 继续使用统一的 PropertyProjector / PropertyEditor 体系：

- 可编辑：`CableSegment.CableType`、`CableSegment.Length`。
- 只读：CableSegment ID、ConnectionId、StartTerminalId、EndTerminalId。
- 长度沿用 Domain 的 `double` 和米单位；输入必须是有限且大于零的数值。

## Command chain

属性修改链路为：

`Cable Inspector → PropertyEditor → PropertyCommandFactory → EditPropertyCommand → CommandStack → CableSegment`

`EditPropertyCommand` 调用 Domain 已有的 `ChangeCableType` / `ChangeLength`，不向 Inspector 暴露 public setter，也不复制 Cable 业务模型。

## Scene and selection

成功修改后沿用现有 Scene rebuild 流程。CableRenderer 继续从 CableSegment 读取型号和长度，并通过既有 LabelRequest / LabelLayoutEngine 生成标签。CableSegment 的 SelectionReference 不变，因此选择保持；连接关系和端子身份不被修改。

失败输入在进入 CommandStack 前被拒绝，显示中文提示，不改变 Domain、Scene 或命令历史。

## Undo / Redo

型号和长度分别使用既有 CommandStack 历史。Undo / Redo 重新应用同一命令的前后值，不重建 CableSegment，也不改变 ConnectionId 或两端 TerminalId。

## Persistence

本阶段不修改 Persistence 格式。现有 V6 Cable 持久化保存 CableType 和 Length；Save/Open 验证应确认修改后的值以及 CableSegment、Connection、StartTerminalId、EndTerminalId 均保持。

## Windows validation checklist

- 选中 CableSegment，确认型号和长度编辑控件可见，端点和 ConnectionId 只读。
- 修改型号和长度，确认 Cable Label 更新且 Selection 保持。
- 分别执行 Undo / Redo，确认值和拓扑身份恢复。
- Save / Open 后确认修改后的型号、长度和端点连接保持。
- 验证空型号、非数值、零值和负值输入显示中文错误且不增加命令历史。

MacOS 环境下 WPF TestHost 可能无法启动；Windows 需要执行 Desktop.Tests 和 Rendering.Wpf.Tests 的实际运行验证。
