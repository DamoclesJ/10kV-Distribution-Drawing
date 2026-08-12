# P0-7-A 模板系统架构设计 Review

> Review 基线：checkpoint commit `126fc930050cb8de22f95f895df66a90b32f64be`。<br>
> Review 对象：`docs/template-system-design.md`。<br>
> 范围：只审查模板架构和既有系统边界，不实现 Template、Builder 或任何生产代码。

## 1. Review 结论

P0-7-A 模板系统架构设计通过 Review。

设计符合当前 DistributionDrawing 的核心分层：模板只描述生成输入，Builder 负责把输入转换为合法 Domain 对象和匹配的 RuntimeLayout，现有 Rendering 继续消费这两个事实源。模板没有直接生成 Scene、Symbol 或 WPF 图形，也没有改变 Domain、CommandStack、Selection、Persistence 或 Rendering 的职责。

本次 Review 发现两处文档表达问题，均已在 `docs/template-system-design.md` 中修正：

1. 模板示例原先出现 `Display`，容易被理解为 `BayTemplate` 的第四个存储字段。现已删除该伪字段，并明确“负 N 间隔”由正整数 `Index = N` 派生显示。
2. 原文已说明 Builder 使用现有 Command 和 Stable ID，但没有集中冻结“模板创建结果必须等价于手工创建”的完整编辑器契约。现已补充 Stable ID、原子 Add、Undo/Redo、SelectionTransition、Inspector、Rendering 和 Dirty 的兼容要求。

修正后无架构阻断问题。P0-7-A 可以作为后续 P0-7-B 规划的设计基线。

## 2. Template 边界检查

检查通过。

设计明确采用：

```text
Template
    ↓
Builder
    ↓
Domain Objects + RuntimeLayout
    ↓
Existing Rendering
```

Template 只保存生成参数、Bay 功能、受控设备配置、二次配置和 LayoutRule，不保存：

- 生成后的 Stable ID；
- 具体实例的绝对 DocumentPoint；
- 逐设备最终坐标；
- SceneElement、Symbol、WPF Shape 或其他 Rendering 类型。

Builder 根据已创建的真实 Domain 对象和 Stable ID 生成 RuntimeLayout。Rendering 不读取模板来创建或修复专业事实。

## 3. Bay 模型检查

检查通过。

`BayTemplate` 冻结为环网柜一次系统基本单元，核心字段为：

```text
BayTemplate
├── Index
├── Function
└── EquipmentConfiguration
```

`Index` 表示现场间隔编号，不是数组下标，也不使用负数编码显示前缀：

- `Index = 5` 显示为“负5间隔”；
- 禁止 `Index = -5`；
- `Bays[]` 顺序表示物理排列顺序；
- Index 不用于推断 Function 或设备配置。

Review 已移除示例中的伪 `Display` 字段，避免破坏已冻结的三字段模型。

## 4. BayFunction 检查

检查通过。

`BayFunction` 表示电气功能，不表示设备名称，目标值包括：

- `Incoming`；
- `Outgoing`；
- `Tie`；
- `PT`；
- `Metering`；
- `Reserve`。

Function 与 EquipmentConfiguration 已明确分离。同一个 Outgoing 可以使用不同设备配置，设备组合也不能反向推断 Function。

PT 没有被建模为普通设备附件。PT Bay 通过 `Function = PT` 表达其一次系统功能；`PTEquipmentConfiguration` 只描述该 Bay 内部采用的受控设备方案。

## 5. PT 模型检查

检查通过。

设计明确 PT：

- 是 Bay；
- 接入主母线并参与一次拓扑；
- 不是普通附件；
- 不是 Cabinet Module；
- 不是 CableTermination；
- 具有专用 PT Terminal；
- 不提供普通电缆出口。

两种目标结构均已表达：

```text
母线 → 隔离刀 → PT → PT端子 → 接地刀
```

```text
母线 → 断路器 → PT → PT端子 → 接地刀
```

设计同时明确当前生产 Domain 尚无 PTInterval。P0-7-B 或后续 Builder 在 PT Domain 完成前必须拒绝该分支，不得把 PT 降级为现有普通间隔、PoleAttachment 或 CableTermination。

## 6. DTU 边界检查

检查通过。

DTU 位于 `SecondaryConfiguration`，不属于一次 `Bays[]`，并且：

- 不参与一次拓扑；
- 不创建一次 ElectricalNode、Terminal 或 Connection；
- 不属于 PT Bay 内部设备；
- `Left` / `Right` 只影响布局排列。

当前项目尚无已确认的 DTU RuntimeLayout 类型。设计正确要求在专用布局表达完成前不生成虚假的一次 Domain Device。

## 7. 间隔数量检查

检查通过，但 P0-7-B 必须保持“模板表达能力”和“当前 Domain 可执行能力”的区别。

设计中 Bay 数量由 `Bays.Count` 得出，不是 CabinetType。模板模型支持 2、3、4、5、6 及未来扩展；一二次融合的 4、6 只是快捷默认，不是模型上限。

当前生产 Domain 对现有纯 LoadSwitch 和纯 IntegratedFeeder 聚合仍有自身校验。P0-7-B 不得为了让模板实例立即可执行而放宽 Domain，也不得把模板的开放数量能力误报为当前 Builder 已全部支持。Builder 必须以当前 Domain 工厂的实际校验结果为最终边界。

## 8. LayoutRule 检查

检查通过。

LayoutRule 只描述或引用生成策略，包括：

- Bay 宽度；
- Bay 间距；
- PT 特殊宽度；
- 柜体边距与母线相对位置；
- DTU 左右排列策略；
- 默认标签偏移策略。

它不保存具体实例坐标。放置位置作为 Builder 的独立调用输入，生成后的几何进入 RuntimeLayout。

设计还正确区分了现有布局类型：Ring Cabinet Template 生成 RingCabinetLayout；AttachmentLayout 只用于 PoleAttachment，不能被提升为所有设备的通用布局容器。

## 9. 与 P0-6 架构兼容检查

检查通过。

对于当前 Domain 已支持的结构，模板生成结果必须等价于现有手工创建结果：

- Domain 保存完整专业事实和拓扑；
- RuntimeLayout 保存与 Stable ID 对应的几何；
- Add Command 原子提交两者；
- Undo/Redo 恢复同一对象和 Stable ID，不重新运行 Builder；
- Selection 与 SelectionTransition 继续使用 Stable ID；
- Inspector 继续经 Resolver / Projector 投影；
- Rendering 继续读取 Domain + RuntimeLayout；
- CommandStack 继续管理 Undo/Redo 和 Dirty。

Template 在创建完成后不成为工程事实的第二来源，也不参与后续 Selection、Inspector 或 Rendering 解析。

## 10. 发现的问题与修正状态

| 问题 | 风险 | 修正状态 |
| --- | --- | --- |
| 示例出现未定义的 `Display` 字段 | 可能把派生显示文本误认为模板持久字段 | 已修正 |
| P0-6 编辑器兼容要求分散 | 可能导致 Builder 重建 ID、绕过 Command 或遗漏 Inspector/Selection | 已集中补充 |

未发现 Template 保存具体坐标或 Rendering 对象、PT 层级错误、DTU 进入一次拓扑、Bay 数量写死、或模板绕过现有 Command 的问题。

## 11. P0-7-B 建议

P0-7-B 建议先做 Builder 实现前设计与能力矩阵，不直接同时实现 PT、DTU 和外部模板格式。

推荐顺序：

1. 冻结最小不可变 Template 输入模型，只覆盖当前 Domain 已支持的 LoadSwitchInterval 与 IntegratedFeederInterval。
2. 建立 `BayFunction + EquipmentConfiguration` 到现有 `RingCabinetIntervalDefinition` 的显式映射；不从名称、CabinetType 或布局推断结构。
3. 明确支持矩阵：区分当前可生成、受现有 Domain 数量校验限制、以及因 PT/DTU 模型缺失而不可生成的配置。
4. 设计 Builder 的完整创建结果，使 Domain 聚合、RingCabinetLayout 和全部 Stable ID 在首次创建时一次固定。
5. 复用现有 AddRingCabinetCommand 原子提交；Redo 复用首次结果，不重新调用 Builder。
6. 为非法 Function/EquipmentConfiguration 组合、当前 Domain 不支持的数量、PT/DTU 未实现分支和布局生成失败定义明确失败语义。
7. 验证模板生成结果与现有配置器手工创建结果在 Domain 拓扑、Layout、Selection、Undo/Redo、Inspector 和 Save/Reload 上等价。

P0-7-B 不应修改 Persistence 或 FormatVersion，也不应为了模板方便而放宽现有 Domain 规则。PT/DTU 的生产实现应作为独立前置能力设计和验收。

## 12. Review 范围确认

本次只修改：

- `docs/template-system-design.md`；
- `docs/p0-7-a-review.md`。

未修改 Domain、Persistence、FormatVersion、CommandStack、Selection、Rendering、Existing Commands、Builder 或任何 Template Runtime Code。
