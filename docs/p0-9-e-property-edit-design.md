# P0-9-E-1 Property Edit Design

## 1. 目标与边界

属性编辑是 Inspector 驱动的业务属性修改。它必须通过 Domain 提供的受控操作和现有 CommandStack 完成，不允许 UI 或 Inspector 直接设置 Domain 属性。

本阶段只编辑显示和业务描述属性，不改变设备身份、电气拓扑或安装关系。

## 2. 可编辑对象

第一阶段的编辑目标包括：

- `RingCabinet`；
- `Pole`；
- `SwitchDevice`；
- `CableSegment`。

编辑入口通过 `SelectionTarget` 定位对象，Resolver/Inspector 提供可编辑字段，Command 再校验目标仍存在且 ID 一致。

## 3. 属性边界

允许修改的内容应限于业务显示属性，例如：

- RingCabinet 的显示名称；
- Pole 的杆号和显示名称；
- SwitchDevice 的显示名称、调度编号；
- CableSegment 的名称、型号、长度等业务描述字段。

具体字段必须以 Domain 当前合法 API 为准。当前 Domain 已有 `Rename`、`RenamePoleNumber`、`SetDispatchNumber` 等受控入口；对于尚未提供变更 API 的字段，应先补充明确的 Domain 操作设计，不能通过反射、可变 DTO 或替换对象绕过不变量。

以下内容禁止由属性编辑修改：

- Stable ID；
- Terminal 及 Terminal 所有者关系；
- ElectricalNode；
- Connection；
- SwitchKind、InstallationType、IntervalKind；
- SwitchState；
- CableSegment 的起止 Terminal 引用；
- PoleAttachment 关系；
- 任意拓扑或设备组合结构。

开关状态必须继续通过专用 `ChangeSwitchStateCommand`，而不是通用属性编辑器。

## 4. EditCommand

未来按对象和属性边界提供明确 Command，例如：

- `ChangeDeviceDisplayNameCommand`；
- `ChangePoleNumberCommand`；
- `ChangeSwitchDispatchNumberCommand`；
- `ChangeCableSegmentPropertyCommand`。

每个 Command 保存：

- 目标 Stable ID；
- 属性 Key；
- Before Value；
- After Value。

构造阶段不修改 Domain。`Execute` 应先解析并校验目标，然后调用 Domain 受控方法；`Undo` 应用 Before Value；`Redo` 再次应用 After Value。目标不存在、值非法或属性不可编辑时，Command 失败且不产生部分修改。

对于 CableSegment 当前只读属性，不能以“编辑器直接写入”为临时方案。应先决定是否增加 Domain 级变更方法，确保长度为有限正数、名称/型号满足现有不变量，并保持 ConnectionId、StartTerminalId、EndTerminalId 不变。

## 5. Inspector 集成

InspectorResolver 根据 `SelectionTarget` 生成 `InspectorModel`。每个属性应携带可编辑性和稳定 Property Key；只读字段仍可展示，但编辑器不得为其创建 Command。

流程为：

```text
SelectionTarget
    -> InspectorResolver
    -> InspectorModel
    -> 用户输入校验
    -> EditCommand
    -> CommandStack
```

Inspector 只负责展示、输入解析和 Command 请求，不直接修改对象。编辑成功后，Inspector 刷新读取 Domain 当前值；编辑失败时保留原值并报告校验结果。

## 6. Undo/Redo 与 Selection

每次用户确认的一次属性修改对应一个原子 Command。输入过程中的临时文本不进入 Undo 历史；只有提交成功的 Before/After 变化进入 CommandStack。

属性编辑不应改变 SelectionTarget。目标仍以同一个 Stable ID 被选中，Inspector 重新解析即可。若对象在编辑期间已被删除或替换，Command 应失败而不清除无关 Selection。

## 7. Persistence 影响

编辑后的业务显示属性属于工程当前状态，保存时应由现有 DTO 保存，重新加载后保持修改结果。Undo/Redo 历史、Before/After 快照和编辑器临时输入不持久化。

如果字段已经存在于 V6 DTO，属性编辑不需要升级格式版本。若某个新业务字段尚未持久化，应先进行独立 Persistence Gap Analysis；本阶段不能借属性编辑临时改变 V6 格式或引入新的拓扑字段。

保存/恢复必须保持：

- Stable ID 不变；
- Terminal/Connection/Node 引用不变；
- Graph 查询结果不因纯显示属性编辑改变；
- SwitchState 不被通用属性编辑污染。

## 8. 明确不实现

本阶段不实现：

- 拓扑修改；
- 新增对象；
- 删除对象；
- Terminal、ElectricalNode、Connection 编辑；
- SwitchState 通用编辑；
- SwitchKind/IntervalKind 变更；
- Cable Split/Reconnect；
- 自由 CAD 属性系统。

## 9. 后续实施建议

建议先实现已有 Domain 受控 API 对应的名称、杆号和调度编号编辑，再单独评估 CableSegment 可编辑字段。每种 EditCommand 都应覆盖合法修改、非法值、Undo、Redo、Stable ID、拓扑不变和 V6 round-trip 测试。
