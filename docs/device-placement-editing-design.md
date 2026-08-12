# Drawing Core P0-2：设备新增/删除真实绘图闭环设计

> 文档状态：实现前设计，不修改生产代码、工程格式或既有设计文档。
> 依据：`drawing-core-capability-review.md`、`desktop-project-session-integration-design.md`、当前 `main@2dbeb90` 代码。

## 1. 目标与当前缺口

P0-2 的目标是让用户在已经可以新建、打开和保存的空工程中，真正放置第一个设备，并完成选择、查看、撤销、重做、保存和恢复。第一阶段只覆盖 `Pole` 与 `RingCabinet`，不引入 Connection Editor。

当前代码已经有 `DrawingDocument.AddDevice`、`RingCabinet` 工厂、Layout/Scene/Symbol 和 `CommandStack`，但这些是底层能力或演示入口，不是用户闭环：

- 没有 Placement 工具状态机和画布放置入口；
- 没有设备生命周期 Command；
- `DrawingDocument` 没有安全的通用删除 API；
- `DrawingLayout` 和 `RuntimeLayoutDocument` 只有 Add/读取能力，没有对称删除；
- MainWindow 仍容易继续承载设备业务流程；
- 多个 RingCabinet 的属性解析和场景刷新需要在动态创建时重新验证。

因此 P0-2-B 的重点不是扩展设备种类，而是建立一个原子、可撤销、可保存的设备生命周期闭环。

## 2. 分层边界

```text
MainWindow 工具事件
    → PlacementController（只收集放置意图）
    → Add/Remove Device Command
    → DrawingDocument + RuntimeLayoutDocument
    → ProjectRuntimeSession.RebuildScene()
    → HitTest / Selection / Overlay / PropertyInspector
```

- Domain：设备、端子、内部节点和引用校验；不保存坐标。
- Runtime Layout：编辑期间唯一布局事实源，保存毫米工程坐标。
- Rendering：根据 Domain + Runtime Layout 构建 Scene，不执行创建/删除业务。
- Editor：Placement、Command、Selection、Undo/Redo 和 Dirty。
- ProjectWorkspaceController：继续负责工程文件生命周期，不负责设备放置细节。

## 3. 最小 Placement 模型

新增一个轻量 `PlacementController`（名称可调整，但职责不变），不把放置逻辑写入 `MainWindow.xaml.cs`。状态最小化为：

```text
Idle
 ├─ SelectPoleTool       → PlacingPole
 └─ SelectRingCabinetTool→ PlacingRingCabinet

画布点击 → 产生毫米文档坐标 → 创建请求 → 回到 Idle
取消/切换工具 → Idle
```

MainWindow 只负责工具菜单和鼠标事件转发。控制器负责将屏幕 DIP 坐标通过现有 `DocumentCoordinateSystem` 转为毫米坐标，并调用 CommandFactory；它不直接修改 Domain 或 WPF Visual。

第一版不需要通用工具注册表、吸附、预览图元或复杂手柄。可选的预览只允许是临时显示，提交/取消必须明确，且不能进入保存快照。

## 4. Add Command 的原子性

建议新增：

- `AddPoleCommand`；
- `AddRingCabinetCommand`。

两者都实现现有 `ICommand`，并保存稳定 ID、Domain 快照所需数据和 Runtime Layout 快照。Command 不保存 WPF 对象或 Selection 引用。

### Execute

1. 先在临时、未挂入工程的对象/快照上完成 Domain 构造；
2. 校验默认 Layout 参数；
3. 在一个 Domain 聚合操作中加入设备及其 Terminal/内部对象；
4. 加入对应 Runtime Layout；
5. 任一步失败，回滚已经加入的 Domain/Layout，Command 不进入历史；
6. 成功后由统一刷新协调器重建 Scene，并选择新对象。

实现上优先提供 `DrawingDocument.AddDeviceWithLayout(...)` 等应用层事务包装，或由 Command 使用明确的补偿回滚；不要先调用 `AddDevice` 再假定 Layout 一定成功。

### Undo / Redo

- Undo：通过 Domain 安全删除 API 删除设备及其内部对象，同时移除 Runtime Layout；
- Redo：用同一个稳定 ID 和原始快照重新构造，不调用 `Guid.NewGuid()`；
- 失败时恢复 Execute 前状态，不留下半个设备或孤立 Layout。

## 5. Pole 创建方案

Pole 创建请求只使用当前 Domain 已有字段：

- `DeviceId`；
- 当前构造器支持的杆号/名称字段；
- 由 `Pole` 工厂创建的架空锚点 Terminal。

画布坐标流程：

```text
鼠标 DIP
→ DocumentCoordinateSystem.DipToMillimeters
→ PoleLayout(position = mm)
→ AddPoleCommand
```

默认 Layout 尺寸沿用当前 `PoleLayout` 默认值，不写入 Pole Domain。创建成功后自动选择 `SelectionTargetKind.Device + PoleId`，PropertyInspector 显示现有杆号等属性。智能编号、杆型扩展和多端子配置留到 P1。

## 6. RingCabinet 创建方案

当前 `RingCabinetDefinition` 明确要求至少一个间隔；普通负荷开关工厂要求 3–6 间隔，一二次融合工厂要求 4 或 6 间隔。因此第一版不能创建空柜，也不应发明新的间隔规则。

建议提供最小创建对话框：

- 柜体名称；
- 柜体模板/结构选择：普通负荷开关型或一二次融合基础型；
- 合法间隔数量：普通型 3/4/5/6，一二次融合型 4/6；
- 初始开关状态使用现有工厂参数的固定安全默认值。

P0-2-B 可先只实现“普通负荷开关型 3 间隔”作为最小可用入口；若产品要求立即验证融合柜，再复用现有 `CreatePrimarySecondaryIntegratedCabinetBase`，但不新增接地结构或状态规则。混合柜配置器、PT/DTU 和逐间隔编辑留后续阶段。

创建后必须一次生成完整 RingCabinet 聚合、内部 Interval/Switch/Node/Terminal，以及一个合法 `RingCabinetLayout` 和全部间隔 Layout。默认柜体位置为点击毫米坐标，尺寸和间隔布局复用现有 Symbol 设计默认值。

## 7. Remove Command 与引用保护

建议新增 `RemovePoleCommand`、`RemoveRingCabinetCommand`，或使用类型化的 `RemoveDeviceCommand`。Command 必须保存完整可恢复快照，而不是只保存设备 ID。

当前 `DrawingDocument` 只有 Add API。删除前必须由 Domain 提供最小安全入口（建议 P0-2-B 先补 `CanRemoveDevice`/`RemoveDevice`，或等价明确 API），统一检查：

- 设备自身 Terminal；
- Connection、OverheadLine 和其他拓扑引用；
- PoleAttachment；
- Professional 的 BoundaryPoint、WorkScope、GroundingPoint Terminal 引用；
- RingCabinet 的内部 Interval、SwitchAssembly、ElectricalNode、Terminal；
- 对应 Runtime Layout。

默认策略是**拒绝有外部引用的删除**，不做猜测式级联删除。特别是：

- 被 Connection 引用的 Pole 或柜体拒绝删除；
- 有 PoleAttachment 的 Pole 拒绝删除，除非未来提供显式的可逆聚合删除规则；
- BoundaryPoint 或 GroundingPoint 间接引用设备端子时拒绝删除；
- 删除 RingCabinet 不得只移除顶层 Device 而遗留内部对象。

删除成功后才移除 Layout、清理 Selection 并进入 CommandStack。删除失败不污染历史、不标记 Dirty。

## 8. Runtime Layout 与 Scene 刷新

建议为 `RuntimeLayoutDocument`/`DrawingLayout` 增加对称的：

- `AddPole` / `RemovePole`；
- `AddRingCabinetLayout` / `RemoveRingCabinetLayout`；
- 必要的 `Contains`/完整性校验。

设备 Command 不直接操作 `DrawingVisual`。成功 Execute、Undo、Redo 后统一执行：

```text
Domain/Layout 修改
→ ProjectRuntimeSession.RebuildScene()
→ HitTestIndex 重建
→ 校验 SelectionReference
→ Selection Overlay 重建
→ PropertyInspector 快照刷新
```

新增对象自动选中；删除对象若当前被选中则清除；Undo/Redo 选择恢复以稳定 ID 为准，不允许悬空引用。保存继续使用已有 `RuntimeLayout → ProjectLayoutSnapshot` 映射。

## 9. 属性与交互边界

P0-2 创建后只要求能查看必要属性：

- Pole：当前已有杆号/名称等投影；杆号可继续复用现有 PropertyCommandFactory；
- RingCabinet：柜体名称、间隔摘要和只读结构信息。

本阶段不扩张环网柜间隔编辑器，不新增开关状态编辑 UI，不允许 PropertyInspector 直接修改 Domain。新增/删除均必须经过 CommandStack；成功操作设置 Dirty，保存后 `MarkSaved()`。

## 10. 保存恢复验收

最小端到端场景：

1. 新建空工程，画布无硬编码对象；
2. 选择杆塔工具，点击画布，创建 Pole；
3. 选择环网柜工具，点击画布，完成最小合法 RingCabinet；
4. 验证两者自动选择、属性投影和 HitTest；
5. Undo/Redo，确认对象和 Layout 稳定 ID 不变；
6. 移动 Pole，保存工程；
7. 关闭并重新打开；
8. 验证 Domain 对象 ID、内部柜体结构、毫米坐标和 Scene 一致；
9. 新打开工程的 Selection 为空、CommandStack 是新历史、Dirty 为 false；
10. 删除存在外部引用的设备，确认 Domain 拒绝且工程状态不变。

## 11. 当前架构风险与最小处理

| 风险 | P0-2 最小处理 |
| --- | --- |
| MainWindow 膨胀 | 新增 PlacementController、DeviceCommandFactory；MainWindow 只转发事件 |
| CommandStack 是否支持生命周期命令 | 复用现有 `ICommand`，增加 Add/Remove Command，不创建第二套历史 |
| DrawingDocument 无安全删除 | 增加统一、可校验、可逆的最小删除 API；拒绝隐式级联 |
| RuntimeLayout 无删除 | 增加对称 Remove/Contains API |
| SceneBuilder 是否能动态构建 | 复用现有 `Build(document, RuntimeLayoutDocument)`，补齐多柜体解析验证 |
| Resolver 的多 RingCabinet 限制 | P0-2 至少改为按稳定 ID 查找全部柜体；不继续依赖“第一台柜体” |
| Runtime 与 Persistence 双状态 | Runtime 仍是编辑事实源，保存时单向生成 Snapshot |
| Stable ID 与重做 | Command 快照保存所有 ID，Redo 不重新生成 |

## 12. P0-2-B 范围建议

建议 **P0-2-B 同时实现 Pole + RingCabinet 的最小创建/删除闭环**，理由是两者共享 Placement、生命周期 Command、Scene 刷新和保存验收基础；只要 RingCabinet 限定为一个最小合法模板，不会引入完整柜体配置器。

P0-2-B 最小文件范围建议：

- `Desktop/Placement/PlacementController`；
- `Rendering.Wpf/Interaction/DeviceCommandFactory` 及 Add/Remove Commands；
- `Domain/Documents/DrawingDocument.cs`：最小安全删除 API；
- `Rendering.Wpf/Layout/DrawingLayout.cs`、`RuntimeLayoutDocument.cs`：删除/完整性入口；
- `Desktop/MainWindow.xaml(.cs)`：工具菜单和点击转发的最小接线；
- 必要的多柜体 `SelectionObjectResolver`/Scene 刷新修正。

验收标准是：普通用户不调用 API、不依赖演示场景，即可从空工程放置一个 Pole 和一个最小 RingCabinet，撤销/重做、删除保护、保存/重新打开全部通过。

如果 RingCabinet 最小对话框无法在一个小迭代内完成，应先拆为 P0-2-B Pole，再将 RingCabinet 创建与最小模板作为 P0-2-C；但不应把 Pole 的真实闭环继续推迟。

## 13. 明确不做

- Connection Editor、Cable/OverheadLine 创建和 Terminal 连线；
- Copy/Paste、多选、框选、Snap/Align；
- Zoom/Pan、Export/Print；
- 完整 RingCabinet 模板/逐间隔编辑；
- PTInterval、DTUCabinet、WorkTicketData；
- 自动布局、智能编号和自动级联删除。
