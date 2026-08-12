# P0-6-D Implementation Plan

> 状态：规划稿，不包含生产代码或测试实现。  
> 基线：P0-6-C 已完成代码实现，并已在 MacBook / .NET SDK 10.0.400 环境通过解决方案编译和 55/55 Domain Tests。

## 1. P0-6-D 目标

P0-6-D 的目标是完善现有 CableTermination / PoleAttachment 的编辑体验，使已创建的杆塔附属电缆终端能够通过受控命令调整布局、保持合理的选择状态，并编辑已确认安全的属性。

本阶段不新增新的专业设备模型，不扩展 CableTermination 的电气语义，也不为其他柱上设备预先定义未经确认的专业规则。

目标用户链路为：

```text
Select PoleAttachment
→ move or edit an allowed property
→ execute Command
→ update RuntimeLayout or Domain
→ rebuild Scene
→ refresh Selection and Inspector
→ Undo / Redo
→ Dirty
→ Save / Reload
```

## 2. 当前基础状态

P0-6-C 已完成以下能力：

- CableTermination、owned ElectricalNode、CableSide/OverheadSide Terminal 和 PoleAttachment 的完整 Domain 聚合；
- 类型化 Add/Remove Command；
- Add/Remove 的 Undo/Redo；
- CableTerminationId、ElectricalNodeId、TerminalId、AttachmentId 等 Stable ID 保持；
- 以 AttachmentId 为键、相对 Pole 保存几何的 AttachmentLayout；
- CableSide/OverheadSide TerminalAnchor；
- PoleAttachment → AttachedDevice → CableTermination 的 Selection Resolver；
- CableTermination Attachment 的只读 Property Inspector；
- Desktop 创建入口和统一删除入口；
- CommandStack Dirty、Save/Reload 的既有集成。

P0-6-D 必须在这些能力之上增量实现，不重新设计 CableTermination 聚合、PoleAttachment 关系或 Persistence 格式。

## 3. P0-6-D 计划范围

### A. Attachment 移动能力

目标是允许用户选中 PoleAttachment 后调整其相对所属 Pole 的位置。

正式编辑链路：

```text
SelectionTargetKind.PoleAttachment
→ resolve AttachmentLayout
→ calculate new Offset in document millimeters
→ MoveAttachmentCommand
→ CommandStack.ExecuteCommand
→ replace RuntimeLayout value
→ RebuildScene
→ refresh Selection / Inspector
```

计划增加 `MoveAttachmentCommand`，或采用与现有命名风格一致的 `MoveAttachmentLayoutCommand`。Command 应持有：

- AttachmentId；
- 修改前 AttachmentLayout 或 Offset；
- 修改后 AttachmentLayout 或 Offset；
- 当前 RuntimeLayout/DrawingLayout 引用。

要求：

- AttachmentId、PoleId、AttachedDeviceId 和全部拓扑 ID 不变；
- 移动只改变 AttachmentLayout.Offset；
- 正式 RuntimeLayout 修改只能发生在 Command Execute/Undo/Redo 内；
- UI 或 Inspector 不得先修改权威 Layout，再补记 Command；
- 若提供拖动预览，预览必须是短生命周期的交互/Rendering 状态，取消时直接丢弃，不能作为未入栈的权威 RuntimeLayout 修改；
- 一次完整拖动只产生一个 CommandStack 历史项；
- Execute 成功后 Dirty，Undo 回到保存点时恢复 clean，Redo 再次 Dirty；
- Undo/Redo 使用原 AttachmentId，不删除重建 Domain 或 Layout 对象来模拟移动。

### B. Layout 编辑能力

P0-6-D 计划支持以下 AttachmentLayout 几何字段的受控编辑：

- Offset；
- WidthMillimeters / HeightMillimeters；
- LabelOffset。

建议使用不可变值替换：从当前 AttachmentLayout 构造修改后的新值，由类型化 Command 调用 DrawingLayout 的受控 Replace API。是否由一个通用 `ChangeAttachmentLayoutCommand` 管理全部字段，或由 Move/Resize/LabelOffset 分成多个命令，应在 D-1 实现审查时依据现有 Command 风格选择最小方案。

所有布局编辑必须满足：

- AttachmentId 不变；
- Width/Height 保持 AttachmentLayout 现有正数约束；
- 数值必须是有限的毫米文档坐标；
- Execute 前验证目标 Layout 存在且 ID 一致；
- Execute、Undo、Redo 都经过 Command；
- Command 失败不进入历史、不改变 Dirty、不留下部分 Layout 状态；
- Save/Reload 继续使用现有 FormatVersion 2 AttachmentLayout DTO，不新增持久化事实。

禁止：

- Inspector 直接修改 Layout 对象；
- WPF 控件直接写 RuntimeLayout；
- 绕过 CommandStack；
- 根据画面坐标改变 PoleAttachment 的 Domain 归属；
- 将 Offset、Width、Height 或 LabelOffset 写入 Domain。

### C. Selection 恢复

当前 Add/Delete 操作会在成功后显式选择或清空对象；Undo/Redo 负责恢复数据并重建 Scene，但没有统一的 Selection 历史语义。

P0-6-D 需要明确并实现编辑器级策略：

- Add 的 Undo：若当前仍选择被撤销的 Attachment，应清除 Selection；
- Add 的 Redo：是否重新选择恢复的 Attachment，需要统一定义；
- Remove 的 Undo：是否恢复删除前的 PoleAttachment Selection，需要统一定义；
- Remove 的 Redo：若选择目标再次不存在，必须清除 Selection；
- Move/Layout Edit 的 Undo/Redo：保持对同一 AttachmentId 的 Selection；
- 每次 Scene rebuild 后，由 Selection Resolver 根据新 InspectionSource 重新解析 Selection；
- 无法解析的 Selection 必须清除，Inspector 同步回到“未选择对象”；
- 可解析的 PoleAttachment Selection 应刷新 AttachedDevice、CableTermination 和 AttachmentLayout 投影。

Selection 是编辑器状态，不进入 Domain、Persistence 或 CommandStack 的工程事实。若需要恢复 Selection，应由 Desktop/编辑会话在命令结果和场景刷新之间协调，不得把 SelectionReference 写入工程文件或 Domain Command 聚合。

D-2 实现前需选定统一 UX 规则。建议默认规则为：Undo Remove 时恢复原 Attachment Selection；Redo Remove 时清除；Move/Layout Edit 的 Undo/Redo 保持 Selection。Add 的 Undo/Redo 是否自动重选应与现有 Pole、RingCabinet 行为一并核对，避免只为 Attachment 建立例外。

### D. Inspector 编辑能力

当前 CableTermination Attachment Inspector 已提供只读投影，包括 Attachment、CableTermination、Terminal、InternalNode 和 Layout 信息。

P0-6-D 计划将已确认安全的字段接入受控编辑，第一项为：

- CableTermination.DisplayName。

可在 D-3 评审后增加 AttachmentLayout 的 Offset、Width/Height 和 LabelOffset 编辑入口，但它们必须复用 B 节的 Layout Command，不能由 Inspector 自行形成第二套修改路径。

Inspector 编辑链路：

```text
SelectionReference
→ SelectionObjectResolver
→ validate editable field/input
→ Property/Device Command Factory
→ CommandStack.ExecuteCommand
→ RebuildScene
→ resolve same Stable ID
→ refresh Inspector
```

明确禁止编辑：

- CableSideTerminalId；
- OverheadSideTerminalId；
- InternalNodeId；
- Terminal owner、role、ElectricalNodeId 或连接策略；
- PoleAttachment.PoleId；
- PoleAttachment.AttachedDeviceId；
- Connection、OverheadLine 或其他拓扑关系。

重新挂接到其他 Pole 属于结构编辑，不是属性编辑，本阶段不实现。

## 4. 不包含范围

P0-6-D 不实现：

- Cable 完整专业模型；
- 电缆型号、长度、路径或材料管理；
- 环网柜新模板或结构重配置；
- PT / DTU；
- 新的柱上专业设备模型；
- 工作票生成或 WorkTicketData 接入；
- 自动拓扑、潮流、停电范围或联锁分析；
- JPG、打印预览或打印导出；
- 大规模 Rendering 重构；
- PoleAttachment 重新挂接；
- Terminal、ElectricalNode 或连接关系编辑；
- Persistence schema 或 FormatVersion 升级。

## 5. 架构约束

### Domain

负责 CableTermination、Terminal、ElectricalNode、PoleAttachment 及其专业事实和结构不变量。Domain 不保存 Offset、尺寸、LabelOffset、Selection 或 WPF 状态。

### Command

负责正式状态变化、验证边界、Undo/Redo 和 Dirty 推进。失败 Command 不进入历史，不留下半状态。

### Layout

负责 Attachment 的几何状态。AttachmentLayout 以 Stable AttachmentId 为键，Offset 相对所属 Pole；布局变化采用不可变值替换。

### Rendering

根据 Domain + RuntimeLayout 构建 Scene、Symbol、Anchor 和 HitTest。Rendering 只显示事实，不创建 Domain，不提交编辑状态。

### Desktop

负责输入、拖动手势、Dialog/Inspector 桥接、Command 调用、Scene rebuild、Selection 和错误反馈。MainWindow 不直接修改 Domain 或 RuntimeLayout。

全阶段禁止：

- Rendering 创建或修复 Domain；
- MainWindow 直接修改 Domain；
- MainWindow、Dialog 或 Inspector 绕过 CommandStack；
- 从坐标推导 PoleAttachment 归属；
- 通过删除重建改变 Stable ID；
- 将 Selection、拖动 Preview 或 Undo 历史持久化。

## 6. 实施顺序建议

### P0-6-D-1：MoveAttachmentCommand 基础能力

目标：

- 建立 AttachmentLayout 受控 Replace 能力；
- 实现类型化 MoveAttachment Command；
- 接入 CommandStack、Dirty、Undo/Redo；
- 建立最小 Attachment 拖动或位置提交入口；
- 移动后重建 Scene，TerminalAnchor 随 Offset 更新。

预计修改范围：

- DrawingLayout / RuntimeLayout 的最小 Attachment Replace API；
- Rendering.Wpf Interaction 中的 MoveAttachment Command；
- DeviceDragController 或独立 Attachment drag controller；
- Desktop 最小手势接入和刷新；
- 可落在现有非 WPF 测试边界内的 Command/Layout 测试。

测试要求：

- Execute、Undo、Redo 的 Offset 正确；
- AttachmentId、PoleId、AttachedDeviceId、TerminalId 和 ElectricalNodeId 不变；
- Command 失败不改变 Layout/History/Dirty；
- CableSide/OverheadSide Anchor 随移动更新并仍绑定原 TerminalId；
- Save/Reload 保持修改后的 Offset；
- 拖动取消不产生 Command 或 Dirty。

### P0-6-D-2：Selection 恢复与编辑体验

目标：

- 冻结 Add/Remove/Move 的 Undo/Redo Selection 规则；
- 在 Scene rebuild 后恢复或清理 Selection；
- 保证 Inspector 和 Selection Overlay 同步；
- 避免不存在对象的悬空 SelectionReference。

预计修改范围：

- Desktop Undo/Redo 协调；
- SelectionManager 或独立 Selection restoration policy；
- ProjectRuntimeSession/MainWindow 的最小刷新接入；
- Selection Resolver 行为测试。

测试要求：

- Undo Remove 后按确认策略恢复 PoleAttachment Selection；
- Redo Remove 后清除 Selection；
- Move 的 Undo/Redo 保持同一 AttachmentId Selection；
- 无法解析的 Selection 被清理；
- Inspector 在每次状态变化后显示当前对象或“未选择对象”；
- Selection 不进入 Persistence 或 Dirty。

### P0-6-D-3：Inspector 安全属性编辑

目标：

- 为 CableTermination.DisplayName 增加受控编辑入口；
- 视 D-1 的 Command 边界复用 Attachment Layout 编辑命令；
- 保持 Terminal、Node、Topology 和 Attachment 归属只读。

预计修改范围：

- Property Command / Factory；
- PropertyEditor 和允许字段白名单；
- PropertyInspector 最小 Desktop 编辑控件；
- Scene/Inspector 刷新接入；
- Domain 属性和 Layout 属性命令测试。

测试要求：

- DisplayName 编辑、Undo、Redo 和 Stable ID；
- 空白名称沿用现有 Domain 归一化语义，不在 Desktop 复制 Domain 规则；
- 非允许字段拒绝编辑；
- Layout 输入校验和 Command 原子性；
- 保存/恢复 DisplayName 和 AttachmentLayout；
- 编辑成功推进 Dirty，失败不改变 Dirty。

## 7. 风险分析

### Layout 与 Domain 一致性

AttachmentLayout 只能引用已存在的 PoleAttachment，且 AttachmentId 必须一致。移动不能改变 PoleId 或 AttachedDeviceId。Replace API 若缺少目标或 ID 不匹配，必须在修改前拒绝。

### Undo/Redo

拖动预览和正式 Command 容易形成双重修改。必须保证权威 Layout 只由 Command 提交，单次手势只形成一个历史项；Undo/Redo 必须使用保存的 Before/After 值，不能重新计算或生成新 ID。

### Selection 生命周期

删除、撤销和重做会使对象短暂不存在或恢复。若刷新顺序不明确，可能出现悬空 Selection、旧 Inspector 或 Overlay。应统一采用“状态变化成功 → RebuildScene/InspectionSource → 恢复或清理 Selection → 刷新 Inspector”的顺序。

### WPF 交互复杂度

鼠标捕获、移动阈值、缩放坐标转换、Esc 取消、工程切换和窗口失焦都可能中断拖动。预览必须可丢弃，不能污染 CommandStack 或 Dirty；文档毫米增量必须通过 ViewTransform 计算，不能直接保存 DIP。

### Inspector 编辑分流

DisplayName 属于 Domain，Offset/尺寸/LabelOffset 属于 Layout。Inspector 必须根据字段来源调用不同的类型化 Command，不得把 Domain 和 Layout 合并成可任意写入的通用对象。

### 测试环境

当前 MacBook / .NET SDK 10.0.400 已能完成 solution build 和 Domain Tests，但 Windows/WPF 实机交互仍需验证。D-1 至 D-3 每阶段都应执行 solution build、现有测试和 `git diff --check`；拖动、Selection、Inspector、Save/Reload 还需 Windows 实机验收。
