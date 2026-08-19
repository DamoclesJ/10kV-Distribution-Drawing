# Phase E-0A — RingCabinet Template Creation & PT Closure

## 1. 阶段目标

本阶段在真实图元校准之前，收口 Desktop 环网柜创建合同、PTInterval 创建链路以及间隔类型变更时的 Domain/RuntimeLayout 一致性。

本阶段不修改真实图元样式，不实现 DTU、正交 Routing、Snap、Alignment、自动避让、Crossing Detection、Line Jump 或 viewport clipping。

## 2. 原问题

旧 Desktop 对话框要求用户逐行添加间隔，并由 `RingCabinetCreationFactory` 再次将 UI 配置映射为 Domain 定义。Domain 已支持 `RingCabinetIntervalDefinition.CreatePT(...)`，但该映射只处理负荷开关间隔和一二次融合间隔，因此选择 PT 后会抛出：

```text
Unsupported interval kind "PTinterval". (Parameter 'configuration')
```

此外，`ChangeIntervalTypeCommand` 原先只替换 RingCabinet Domain 状态，没有同步替换对应 `RingCabinetIntervalLayout`。普通间隔与 PT 之间变更后，旧 Switch Layout 或缺失的 `PTSymbolPosition` 会使 Domain 和 RuntimeLayout 不一致。

## 3. 新创建合同

Desktop 的主要创建流程调整为：

```text
输入环网柜名称
→ 选择环网柜类型
→ 选择业务间隔数量
→ 自动生成完整 Template
→ 一次性创建 Domain + RuntimeLayout
→ 通过一个 AddRingCabinetCommand 加入工程
```

当前支持：

- 普通负荷开关环网柜：3、4、5、6 个业务间隔；
- 一二次融合环网柜：4、6 个业务间隔；
- 业务间隔名称按 Template 顺序固定生成 `负1`～`负N`；
- 环网柜名称使用用户输入；
- 一二次融合柜可显式包含一个正式 PTInterval；该入口不创建 DTU。

PT 当前追加在业务间隔之后，仅作为尚未冻结 PT/DTU 最终组合规则前的确定性创建顺序。该顺序不是最终 PT/DTU 物理排列规范；用户不能通过本阶段 UI 配置 DTU 或 PT/DTU 位置。

创建后的间隔名称可通过现有 Property/CommandStack 入口修改，修改参与 Undo/Redo 和 Persistence。

## 4. Template 架构决定

本阶段复用现有 `RingCabinetTemplate`、`BayTemplate`、`RingCabinetTemplateDomainBuilder`、`RingCabinetLayoutFactory` 和 `AddRingCabinetCommand`，没有新增第二套 Desktop Domain 构造链路。

具体调整：

- `BayTemplate` 增加可选 `DisplayName`；
- 增加正式 `PTConfiguration` 和 `TemplateCapability.PTInterval`；
- `RingCabinetTemplateDomainBuilder` 将 `PTConfiguration` 映射为 `RingCabinetIntervalDefinition.CreatePT(...)`；
- `RingCabinetCreationTemplateFactory` 根据“柜型 + 业务间隔数量 + 可选 PT”生成完整 Template；
- `RingCabinetCreationConfiguration` 只携带用户柜名和完整 Template；
- `RingCabinetCreationFactory` 委托现有 Domain Builder，不再维护平行的间隔类型 switch 映射。

旧固定三间隔 Template 仍可作为既有内置模板使用，但不限制新 Desktop 创建入口。

## 5. PT Domain、Scene 与 Layout

PT 创建使用正式 Domain 定义，结构保持：

```text
IsolationSwitch → PT → GroundSwitch
```

PT 的 Switch Layout 与 `PTSymbolPosition` 继续由统一 `RingCabinetLayoutFactory` 生成。Scene 仍由 `DrawingSceneBuilder` / `RingCabinetRenderer` / `PTIntervalSymbol` 根据 Domain + RuntimeLayout 构建，Selection 使用现有 Stable ID 和 HitTestIndex。

## 6. Layout / Persistence 决定

`PTSymbolPosition` 当前没有 Desktop 编辑入口，也不属于用户可编辑 RuntimeLayout 状态。它由 IntervalKind 和标准 RingCabinet 布局规则完全确定，因此归类为可确定性重建数据。

本阶段不向 Persistence V6 DTO 增加冗余字段，也不升级格式版本。工程加载时，`ProjectLayoutRuntimeMapper` 根据恢复后的 PT IntervalKind 调用 `RingCabinetLayoutFactory` 的统一规则重建 `PTSymbolPosition`。

保存/打开回归测试必须证明：

- PT Domain 和 Stable ID 保持；
- PT RuntimeLayout 位置恢复为相同确定值；
- Scene 仍能产生 PT 图元。

## 7. Interval Type Change 一致性

`ChangeIntervalTypeCommand` 现在同时持有 Domain aggregate 与 `RuntimeLayoutDocument`：

- Execute：捕获原 Domain + Layout，改变 Domain，并只重建目标 Interval 的标准内部布局；
- 其他间隔布局及柜体位置、尺寸、标签偏移保持不变；
- 新 Switch Layout 必须完整覆盖新 Domain Switch Stable IDs；
- PT 目标获得标准 `PTSymbolPosition`，离开 PT 后该位置被移除；
- 任一步失败时同时恢复原 Domain + Layout；
- Undo 恢复原 Domain + Layout；
- Redo 恢复首次执行产生的相同 Domain + Layout Stable IDs。

间隔名称编辑使用独立原子 Command，不改变拓扑或 RuntimeLayout。

## 8. 测试覆盖

本阶段增加或调整的覆盖包括：

- 3/4/5/6 间隔普通负荷开关柜 Template；
- 4/6 间隔一二次融合柜 Template；
- `负1`～`负N` 自动名称及用户柜名；
- 正式 PT Template、Domain definition、Switch 结构；
- PT Layout、Scene、Selection；
- 创建 Command 的原子 Undo/Redo 与 Stable IDs；
- PT Save/Open round-trip 与确定性 `PTSymbolPosition`；
- 普通/融合间隔到 PT、PT 到负荷开关间隔的 Domain/Layout 同步；
- Type Change Undo/Redo；
- 自动名称通过 Property/CommandStack 修改；
- 既有 Switch operation 测试继续保留。

当前 macOS 环境的最终验证情况：

- Domain 与 Application 生产项目曾在本轮中间版本完成编译；
- 最终 solution/test/WPF 构建在当前 macOS 沙箱中均出现约 5 分钟无编译诊断等待，随后以“0 个错误”退出失败；
- 该现象未提供可归因到源码的编译错误，因此没有通过修改 Windows WPF TargetFramework 或生产行为绕过；
- `git diff --check` 通过；
- 最终 WPF 编译、测试与运行结论必须以 Windows 实机命令为准。

最终执行详情以本次变更交付报告为准。

## 9. 后续明确不包含内容

以下内容未在 Phase E-0A 实现：

- DTU Domain/Rendering/Persistence 或假对象；
- PT/DTU 最终组合与左右位置规则；
- Word 真实图元重绘；
- RingCabinet 最终视觉样式及外框移除；
- Cable 虚线与 OverheadLine 实线规范化；
- 正交 Routing；
- Snap / Alignment；
- 自动避让；
- Crossing Detection / Line Jump；
- viewport clipping。
