# P0-7-D-2-A Template Builder 业务规则冻结

## 1. 文档目的与适用范围

本文冻结 Template Builder 进入生产实现前必须统一的最小业务规则，重点解决 Bay 的物理顺序、现场编号、电气功能以及旧工程迁移之间的边界。

本文承接：

- `template-system-design.md`；
- `template-builder-design.md`；
- `template-runtime-model-design.md`；
- `p0-7-d-2-domain-compatibility-decision.md`。

本文只形成设计决策，不增加 Domain 字段，不修改 DTO 或 `FormatVersion`，也不实现 Template Runtime Model、Builder、Command 或 UI。

本文冻结的规则只覆盖当前已明确的最小范围。未在本文确认的 Function 与设备组合兼容矩阵、厂家编号规则、自动命名规则和 PT/DTU 专业模型不得自行补充。

## 2. Bay Index 与 Sequence 分离

### 2.1 Sequence 的定义

`Sequence` 表示 Bay 在同一个 RingCabinet 内的物理排列顺序。

它用于：

- Layout 的从左到右排列；
- Rendering 顺序；
- 聚合内部遍历；
- Persistence 恢复有序 Interval 集合。

第一版冻结规则：

- Sequence 从 1 开始；
- 同一 RingCabinet 内连续且唯一；
- Sequence 由 RingCabinet 内实际排列顺序决定；
- 调整 Bay 的物理顺序时，Sequence 随位置变化；
- Sequence 不是现场业务编号，不用于显示“负 N 间隔”。

当前 `RingCabinetInterval.Sequence` 已经承担这一职责，后续 Domain 补充应保留该语义。

### 2.2 Index 的定义

`Index` 表示现场业务间隔编号，即“负 N 间隔”中的正整数 N。

它用于：

- Inspector 展示；
- 工作票引用；
- 设备台账；
- 后续专业分析和跨界面识别。

第一版冻结规则：

- `Index = 5` 表示现场“负5间隔”；
- 禁止用 `Index = -5` 表示“负5间隔”；
- “负”是显示前缀，不是 Index 的数值符号；
- Index 是稳定业务事实，不因物理排序变化而改变；
- DisplayName 不是 Index 的事实源，不能通过名称反向解析 Index。

### 2.3 Domain 决策

推荐并冻结：`RingCabinetInterval` 最终同时保存 Sequence 与 Index。

二者的关系为：

| 概念 | 表达内容 | 排序后是否变化 | 是否允许不连续 |
| --- | --- | --- | --- |
| Sequence | 物理排列位置 | 是 | 否 |
| Index | 现场业务编号 | 否 | 是 |

例如，一个柜体按物理顺序排列的 Bay 可以依次具有 Index `1、2、5、7`。此时 Sequence 为 `1、2、3、4`，Index 仍为 `1、2、5、7`。

不得用 Sequence 代替 Index，也不得把 Index 当作 Layout 排序键。

## 3. Bay Index 规则冻结

第一版 Index 不变量冻结如下：

- 必须是正整数；
- 必须在同一个 RingCabinet 内唯一；
- 允许缺号；
- 不允许重复；
- 不要求连续；
- 不要求与 Sequence 相等；
- 创建完成后保持稳定，不因重排自动重编号。

普通模板可以默认产生 `1、2、3……N`，但这是模板创建便利策略，不是 Domain 连续性约束。

特殊模板可以明确产生 `1、2、5、7` 等非连续编号。Builder 必须按模板提供的 Index 创建，不得补齐缺号，不得排序，也不得在创建后重新编号。

本阶段不定义：

- 柜体合并或拆分时的自动重编号；
- 删除 Bay 后是否复用空缺编号；
- 厂家特定的字母或复合编号；
- Index 的批量编辑流程。

## 4. Template 中的 Index 配置策略

### 4.1 方案比较

方案 A 只提供 Bay 数量，由 Builder 直接生成 `1..N`。该方案适合普通实例，但无法无损描述非连续编号，也会把编号策略错误地放入 Builder。

方案 B 让最终的 `BayTemplate` 明确保存 Index，同时以 Bays 集合顺序表达 Sequence。该方案可以表达普通和特殊实例，并保持 Template 是完整、无歧义的 Builder 输入。

### 4.2 冻结结论

第一版采用方案 B：

- `BayTemplate.Index` 必须显式存在；
- `RingCabinetTemplate.Bays` 的集合顺序表示物理 Sequence；
- `BayTemplate` 不再重复保存一个独立 Sequence 字段；
- 模板作者可以覆盖每个 Bay 的 Index；
- 仅数量驱动的模板创建工具可以默认生成 `1..N`，但必须在调用 Builder 前物化为完整、显式的 BayTemplate 集合。

因此，Builder 接收的输入始终明确包含每个 Bay 的 Index。Builder 不负责猜测、补全或重排 Index。

例如，Bays 集合中的第二项可以具有 `Index = 5`：它的 Sequence 是 2，现场编号是 5。

## 5. BayFunction 第一版冻结

### 5.1 第一版枚举集合

第一版 Domain/Template 共同需要表达的 `BayFunction` 集合冻结为：

- `Unknown`；
- `Incoming`；
- `Outgoing`；
- `Tie`；
- `PT`；
- `Metering`；
- `Reserve`。

其中：

- `Incoming` 表示进线功能；
- `Outgoing` 表示出线功能；
- `Tie` 表示联络功能；
- `PT` 表示 PT 专用一次 Bay；
- `Metering` 表示计量功能；
- `Reserve` 表示备用功能；
- `Unknown` 只表示旧数据迁移后尚未确认的功能。

`BayFunction` 表示电气用途，不表示设备类型。同一个 Function 可以对应不同 EquipmentConfiguration；也不能从某个设备组合反向推断唯一 Function。

### 5.2 第一版暂不支持

以下候选不进入第一版枚举：

- `BusSection`；
- `Auxiliary`；
- 厂家特定用途；
- 尚未确认语义的自由字符串功能。

它们在专业语义、拓扑要求和与现有设备组合的兼容性确认后，可以通过后续版本扩展。不得预先映射为 Tie、Reserve 或 Unknown 来模拟支持。

### 5.3 兼容性边界

本次只冻结枚举集合，不冻结 Incoming、Outgoing、Tie、Metering、Reserve 与 LoadSwitch/IntegratedFeeder 之间的完整兼容矩阵。

唯一延续既有设计的强约束是：

- `Function = PT` 必须使用 PT 专用 EquipmentConfiguration；
- PT EquipmentConfiguration 必须使用 `Function = PT`；
- 当前 Builder 因能力不足而拒绝实际生成 PT。

不得因为枚举已存在，就自行定义其余 Function 的设备数量、比例、接线或厂家规则。

## 6. Unknown 策略

### 6.1 新模板与新建对象

新模板不允许使用 `BayFunction.Unknown`。

生产 Builder 在输入中发现 Unknown 时必须返回明确校验失败，不创建 Stable ID，不创建 Domain 对象，也不创建 RuntimeLayout。

Unknown 不能作为“暂时随便创建”的默认值，也不能被当作 Outgoing、Reserve 或其他功能处理。

### 6.2 旧工程迁移

旧工程没有 BayFunction，且不能从 DisplayName、IntervalKind、Switch 组合或 Layout 可靠推断功能。

因此旧工程迁移时：

- BayFunction 使用 `Unknown`；
- 不拒绝仅因缺少历史 Function 而加载合法旧工程；
- 不根据名称或结构猜测 Function；
- Inspector 应在后续实现中明确显示 Unknown，而不是隐藏为某个正常值；
- 在依赖 Function 的专业功能执行前，必须要求该值已被用户或受控业务流程确认。

Unknown 是迁移兼容状态，不是正常新建状态。加载旧数据与创建新对象必须采用不同校验入口，避免迁移兼容值扩散到新模板。

## 7. CabinetType 边界确认

`CabinetType` 只属于 Template 分类，不进入当前 RingCabinet Domain。

它可以用于：

- 模板目录分类；
- 初始默认值；
- 能力声明；
- LayoutRule 选择。

生成后的柜体事实由实际 Intervals、IntervalKind、GroundingStructureKind、Switch、Terminal、ElectricalNode 和 SwitchAssembly 表达。普通、一二次融合及混合柜的实际组成应从这些事实派生。

禁止：

- 用 CabinetType 覆盖 Bays 的实际配置；
- 在 Domain 中重复保存可与实际组成冲突的 CabinetType；
- 因 CabinetType 固定 Bay 数量或设备比例。

## 8. EquipmentConfiguration 边界确认

`EquipmentConfiguration` 只存在于 Template Runtime Model，作为创建描述，不作为生成后的重复 Domain 事实保存。

Builder 将其映射为现有或未来正式 Domain 对象，包括：

- IntervalKind；
- GroundingStructureKind；
- Switch；
- Terminal；
- ElectricalNode；
- SwitchAssembly；
- 其他经确认的设备聚合事实。

生成成功后，上述真实 Domain 对象是权威事实。不得在 Domain 中额外保存一份 EquipmentConfiguration，并让两套事实发生漂移。

Builder 也不得根据 Layout 或 Rendering 反向构造 EquipmentConfiguration。

## 9. PT 与 DTU 规则确认

### 9.1 PT

PT 在 Template 中是 `Function = PT` 的一次 Bay，不是普通 Attachment、CableTermination 或 Cabinet Module。

当前代码尚不具备完整的 PT Domain、PT Layout、Rendering 和 Persistence 能力。因此第一版 Builder 可以识别 PT 模板需求，但必须在任何对象或 Stable ID 创建前返回 `UnsupportedCapability`。

不得：

- 用 IntegratedFeeder 模拟 PT；
- 用 CableTermination 代替 PT 专用端子；
- 生成缺少一次拓扑事实的伪 PT；
- 产生部分 Domain 或 Layout 状态。

### 9.2 DTU

DTU 属于 `SecondaryConfiguration`，不属于一次 Bay，不进入当前 RingCabinet 一次 Domain。

当前代码尚无 DTU 的正式 Domain/Layout/Rendering/Persistence 能力。因此模板可以声明 DTU 能力需求，但 Builder 必须返回 `UnsupportedCapability`，不得只绘制一个无 Domain 事实的 DTU 图形。

DTU 的左侧或右侧位置属于未来 Layout 生成规则，不改变一次拓扑。

## 10. Persistence 与 Migration 策略

### 10.1 FormatVersion 决策

Bay Index 与 BayFunction 进入 Domain 后属于新的长期持久化事实。未来实现必须同步：

- RingCabinetInterval Domain；
- RingCabinetDefinition/恢复输入；
- Domain DTO；
- 保存与恢复映射；
- `FormatVersion`；
- 旧版本迁移测试。

不能在不升级持久化合同的情况下把 Index 或 Function 仅存入 DisplayName、Layout 或 TemplateReference。

本文不决定具体的新 FormatVersion 数值，数值应由实际 Schema 实现提交统一确定。

### 10.2 旧工程 Index 迁移

旧工程只有稳定的物理 Sequence，没有独立现场 Index。第一版迁移规则冻结为：

- `Index = 原 Sequence`；
- 该值是确定性的兼容兜底，不是从现场业务语义推断出的编号；
- 不从 DisplayName 解析 Index；
- 不从 Layout 位置推断 Index；
- 不因 IntervalKind 或设备结构修改 Index。

该规则保证旧工程可以确定性加载，并满足正整数和柜内唯一约束。但对于历史上存在特殊编号的柜体，迁移值可能不等于真实现场 Index。后续 UI/验收流程必须允许用户识别并受控修正，依赖准确 Index 的专业功能不能把迁移兜底值视为已现场确认。

### 10.3 旧工程 Function 迁移

旧工程的 BayFunction 统一迁移为 `Unknown`。

禁止：

- 根据 DisplayName 猜 Incoming、Outgoing、Tie、PT 或 Reserve；
- 根据 IntervalKind 猜 Function；
- 根据间隔位置或数量猜 Function；
- 因 Function 未知拒绝加载其他方面合法的旧工程。

旧工程完成迁移并保存为新版本后，应显式持久化 Index 与 Function，避免每次加载重复推断。

## 11. Template Builder 校验顺序

生产 Builder 在创建任何 Stable ID 或对象前，至少完成以下与本文相关的预校验：

1. Bays 集合有明确顺序；
2. 每个 Index 为正整数；
3. 同一模板内 Index 唯一；
4. 新模板中 Function 不是 Unknown；
5. PT/DTU 等 RequiredCapabilities 均受当前 Builder 支持；
6. 已确认的 Function 与 EquipmentConfiguration 强约束成立。

任何一项失败都必须返回明确结果，不生成半成品 Domain 或 RuntimeLayout。

Builder 创建时：

- 按 Bays 集合顺序生成 Domain Sequence；
- 原样保存每个 BayTemplate.Index；
- 原样保存已验证的 BayFunction；
- 将 EquipmentConfiguration 映射为真实 Domain 结构；
- 不自动排序、补号、重编号或猜测用途。

## 12. 最终业务规则清单

### 12.1 Sequence 是否存在

是。Sequence 表示物理排列顺序，Domain 继续保存；Template 通过 Bays 集合顺序表达，不重复增加 Sequence 字段。

### 12.2 Index 是否进入 Domain

是。Index 是稳定的现场业务间隔编号，必须成为 RingCabinetInterval 的长期事实。

### 12.3 Index 是否要求连续

不要求。Index 必须为正整数且柜内唯一，允许缺号，不允许重复。

### 12.4 Template 是否允许覆盖 Index

允许。普通创建工具可默认生成 `1..N`，但最终 Template 必须显式保存每个 Index，Builder 不自行推断。

### 12.5 BayFunction 第一版枚举

第一版为 `Unknown、Incoming、Outgoing、Tie、PT、Metering、Reserve`。BusSection、Auxiliary 和厂家特定功能留待后续确认。

### 12.6 Unknown 是否允许

仅允许作为旧工程迁移兼容值。新模板和生产 Builder 新建流程禁止 Unknown。

### 12.7 CabinetType 是否进入 Domain

不进入。CabinetType 保持 Template 分类和默认策略信息。

### 12.8 EquipmentConfiguration 是否进入 Domain

不以配置对象或枚举副本进入。Builder 将其映射为实际 Interval、Switch、Terminal、Node 和 Assembly 等 Domain 事实。

### 12.9 PT/DTU 当前处理方式

Template 可以表达能力需求；当前 Builder 必须返回 UnsupportedCapability，不生成伪对象或部分状态。

### 12.10 下一步 Domain 补充范围

推荐下一步采用最小 Domain 补充，不实施更大重构：

- RingCabinetInterval 增加只读 Bay Index；
- RingCabinetInterval 增加只读 BayFunction；
- 创建定义与恢复路径承载两个值；
- 聚合校验 Index 为正且柜内唯一；
- 新建路径拒绝 Unknown，迁移恢复路径允许 Unknown；
- Persistence DTO 与 FormatVersion 同步升级；
- 旧工程采用 `Index = Sequence`、`Function = Unknown` 的确定性迁移；
- 增加 Domain、Persistence migration 和 Stable ID round-trip 测试；
- Inspector 第一阶段只读显示 Index 与 Function。

该补充不包含：

- Index/Function 编辑 Command；
- Bay 重排 UI；
- 自动命名或自动重编号；
- PT/DTU 生产模型；
- Function 与设备组合的完整专业矩阵；
- Template Builder 生产实现。

## 13. 未决问题

以下问题不阻断最小 Domain 补充，但在对应功能进入生产前仍需单独确认：

- 旧工程迁移后的 Index 如何在 UI 中标记为“待现场确认”；
- Index 修改、冲突处理和审计流程；
- Bay 重排后 Inspector 与工作票的交互规则；
- Incoming、Outgoing、Tie、Metering、Reserve 与具体 EquipmentConfiguration 的完整兼容矩阵；
- BusSection、Auxiliary 及厂家特定 Function 的正式语义；
- PT 与 DTU 的完整 Domain、Layout、Rendering 和 Persistence 设计。

在这些规则确认前，不得通过名称猜测、设备组合推断或 Rendering 补事实。

## 14. 下一步建议

下一阶段应先实施 Bay Index 与 BayFunction 的最小 Domain/Persistence 补充，再进入生产 Template Runtime Model 与 Builder：

1. 设计并评审 Domain 字段、枚举及创建/恢复校验；
2. 设计 FormatVersion 升级和旧工程迁移合同；
3. 实现 Domain/DTO/恢复路径并完成迁移测试；
4. 实现只读 Inspector 投影；
5. 在上述事实可以无损保存后，实现 Template Runtime Model；
6. 最后实现 Builder、原子 Command 和 Desktop 创建入口。

该顺序避免生产 Builder 先生成无法持久化的 Index/Function，也避免后续依赖 DisplayName 或 TemplateReference 反向恢复业务事实。
