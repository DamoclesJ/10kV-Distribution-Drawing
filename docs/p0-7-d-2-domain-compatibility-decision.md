# P0-7-D-2 Domain Compatibility Decision

> 状态：Domain 兼容边界决策稿；不包含生产代码、DTO、FormatVersion 或 Builder 实现。<br>
> 基线：checkpoint commit `e3bb2421e7e50b8413e6c3c9cff9bf8fe7536615`。<br>
> 上游设计：`docs/template-system-design.md`、`docs/template-builder-design.md`、`docs/p0-7-c-builder-readiness-review.md`、`docs/template-runtime-model-design.md`。<br>
> 决策原则：生成后仍需查询、显示、编辑或参与专业流程的信息必须成为 Domain 事实；可从现有结构可靠派生的信息不重复保存；仅说明生成来源的信息留在 Template 层。

## 1. 执行摘要

本 Review 推荐方案 B：在实现完整业务语义的 Template Builder 前，先对 RingCabinetInterval 做最小 Domain 补充。

明确决策：

| 信息 | 是否进入 Domain | 结论 |
| --- | --- | --- |
| Bay Index | 是 | 现场间隔编号是长期业务身份的一部分，不能只存在于 Template 或 DisplayName |
| BayFunction | 是 | Incoming/Outgoing/Tie/PT/Metering/Reserve 是长期电气用途事实，不能从设备结构推断 |
| CabinetType | 否 | 当前只是模板分类/生成来源；实际柜体组成由 Intervals 派生 |
| EquipmentConfiguration | 不以模板配置对象进入 | 映射为现有 IntervalKind、GroundingStructureKind、Switch/Terminal/Node/Assembly 等 Domain 事实 |
| TemplateId / TemplateReference | 否 | 第一版生成后完全脱离模板，不进入工程 Persistence |
| PT | 当前不生成 | 缺少完整 Domain/Layout/Rendering/Persistence 前置能力 |
| DTU | 当前不生成 | 属于 SecondaryConfiguration；不能作为一次 Bay 或伪造 RingCabinet 字段 |

对“仅验证技术映射的实验性 Builder”，现有 Domain 足以创建 LoadSwitch/IntegratedFeeder 聚合。但对计划进入生产、支持 Inspector、工作票和后续分析的 P0-7 Builder，丢弃 Index 与 Function 会造成不可逆信息损失。因此不建议先上线丢字段 Builder，再进行迁移。

## 2. RingCabinet 当前 Domain 结构审查

### 2.1 当前对象模型

当前 Domain 已表达：

- `RingCabinet`：柜体聚合根；
- `RingCabinetInterval`：柜内有序间隔；
- `SwitchDevice`：负荷开关、隔离刀、断路器和接地刀等一次开关设备；
- `SwitchAssembly`：同一间隔内设备组合与规则边界；
- `Terminal`：内部设备端子和间隔外部端子；
- `ElectricalNode`：MainBus、Intermediate、Circuit、Earth 等电气节点；
- Terminal 与 ElectricalNode 的固定绑定；
- `GroundingStructureKind`：IntegratedFeeder 的拓扑结构事实；
- `CabinetCompositionKind`：根据实际 Intervals 派生的柜体组成分类。

当前创建链路为：

```text
RingCabinetIntervalDefinition[]
→ RingCabinetDefinition
→ RingCabinet.Create
→ complete validated RingCabinet aggregate
```

Domain 工厂一次创建 Interval、Switch、Terminal、ElectricalNode、SwitchAssembly 和固定内部拓扑。外部 Builder 不需要、也不应逐个拼装这些对象。

### 2.2 当前间隔字段

`RingCabinetInterval` 当前保存：

- IntervalId；
- ParentCabinetId；
- Sequence；
- DisplayName；
- IntervalKind；
- SwitchDevices；
- SwitchAssembly；
- GroundingStructureKind；
- Intermediate/Circuit/Earth NodeId；
- ExternalTerminalId。

其中：

- `Sequence` 表示柜内物理排列顺序；
- `DisplayName` 表示可显示名称；
- `IntervalKind` 表示设备结构类型。

当前没有独立 Bay Index，也没有 BayFunction。

### 2.3 是否足够承载 Template Builder

从“生成当前两类设备结构”的技术角度，当前模型足够：Template EquipmentConfiguration 可以映射为现有 IntervalDefinition，Domain 工厂能创建完整聚合。

从“长期保存 Template 所表达的现场语义”的业务角度，当前模型不足：

- Sequence 不能替代现场间隔编号；
- DisplayName 不能作为可解析的编号存储；
- IntervalKind 不能替代 Incoming/Outgoing/Tie 等 Function；
- 生成后完全脱离 Template 会丢失 Index 与 Function。

因此当前 Domain 适合做 Builder 映射原型，但不适合在要求保留现场 Index/Function 的生产链路中无损承载 Template Runtime Model。

## 3. Bay Index Domain 决策

### 3.1 语义区分

必须区分三个概念：

| 概念 | 示例 | 职责 |
| --- | --- | --- |
| Sequence | 第 1 个物理位置 | 柜内排列顺序，由集合顺序决定 |
| Bay Index | `5` | 现场“负5间隔”的编号事实 |
| DisplayName | “负5间隔”或人工名称 | 图面和 Inspector 显示文本 |

它们不能互相替代。Bays 可以按物理顺序排列为 Index 5、1、7；Index 也可能因现场规则不连续。用户修改 DisplayName 后，Index 仍应保持。

### 3.2 方案 A：Index 只存在 Template

优点：

- 不修改当前 Domain；
- Builder 可立即映射现有结构。

缺点：

- 生成后无法可靠回答现场间隔编号；
- Save/Load 后只剩名称文本；
- 重命名会破坏编号信息；
- Inspector、工作票、报表只能解析 DisplayName；
- 无法区分 Sequence 与现场编号；
- 后续补字段需要从不可靠文本迁移。

### 3.3 方案 B：Index 成为 Domain 属性

优点：

- 现场编号与显示名称分离；
- 支持工作票、运维展示和报表；
- 后续拓扑或专业规则可以直接引用稳定值；
- Save/Load 可以无损恢复；
- Inspector 可显示明确的“现场间隔编号”。

代价：

- RingCabinetInterval、创建定义、Restore 定义和 DTO 需要最小扩展；
- 需要迁移旧 FormatVersion 2 项目；
- 必须在专业规则确认前避免过度校验。

### 3.4 决策

推荐方案 B：Bay Index 进入 RingCabinetInterval Domain，作为正整数长期事实。

第一版只冻结最小不变量：

- Index > 0；
- 不使用负数表达“负 N”；
- Index 与 Sequence 是两个独立字段；
- DisplayName 不作为 Index 的事实源。

暂不自行定义：

- 自动编号；
- Index 是否必须连续；
- 插入/删除后的自动重排；
- 不同柜体或 PT 是否共享编号空间；
- 重复 Index 的处理规则；
- “负”前缀之外的命名规则。

模板可提供 Index；Domain 保存 Index；显示层可以投影“负{Index}间隔”，但 DisplayName 是否自动生成仍由后续 Naming/UI 设计决定。

## 4. BayFunction Domain 决策

### 4.1 语义区分

BayFunction 表示间隔电气用途：

- Incoming；
- Outgoing；
- Tie；
- PT；
- Metering；
- Reserve。

`IntervalKind` 表示设备结构：

- LoadSwitchInterval；
- IntegratedFeederInterval；
- 未来 PTInterval。

一个 Outgoing 可以使用 LoadSwitch 或 IntegratedFeeder；同一设备结构也可以承担 Incoming、Outgoing、Tie 或 Reserve。Function 不能从 IntervalKind、SwitchKind 或 DisplayName 推断。

### 4.2 方案 A：只保留设备结构

优点：

- 不修改当前 Domain；
- 当前 Rendering 和拓扑创建不需要 Function。

缺点：

- 生成后丢失 Template 的电气用途；
- 工作票无法按进线、出线、联络或备用筛选；
- Inspector 无法展示用途；
- 报表和设备分类只能猜测；
- 未来分析需要重新人工标注；
- PT Function 与普通设备结构的语义关联无法长期保留。

### 4.3 方案 B：RingCabinetInterval 保存 Function

优点：

- Function 作为长期专业事实可被查询、持久化和展示；
- 工作票、运维报表和后续电气分析有稳定输入；
- Function 与设备结构保持正交；
- Builder 不需要把 Function 编码进名称。

代价：

- 需要新增 Domain 值及 DTO 字段；
- 旧项目迁移时没有可靠依据自动推断 Function；
- Function 与 EquipmentConfiguration 的完整兼容矩阵尚未确认。

### 4.4 决策

推荐方案 B：BayFunction 进入 RingCabinetInterval Domain，作为长期电气用途事实。

但应分两层冻结：

1. 本决策冻结“Function 必须长期保存”；
2. 后续专业设计再冻结 Function 枚举完整性、旧项目默认值和 Function–IntervalKind 兼容规则。

在规则未确认前不得：

- 根据 IntervalKind 自动推断 Incoming/Outgoing/Tie/Reserve；
- 根据设备组合自动改写 Function；
- 规定每个柜必须有一个 Incoming；
- 限制 Outgoing 的数量；
- 把 Reserve 等同于无设备；
- 为旧项目静默填入 Outgoing。

PT 是明确例外方向：未来 PTInterval 应使用 Function = PT，但具体 PT Domain 完成前不实现该分支。

## 5. CabinetType Domain 决策

### 5.1 方案 A：仅模板信息

Template CabinetType 用于模板目录分类、默认能力和布局规则选择。生成后 RingCabinet 的实际组成由有序 Intervals、IntervalKind 和 GroundingStructureKind 表达，并可派生 `CabinetCompositionKind`。

优点：

- 不复制与 Intervals 可能冲突的柜型事实；
- 支持混合柜；
- 与现有 `CabinetCompositionKind` 派生模型一致；
- 模板来源变化不影响项目对象。

### 5.2 方案 B：Domain 保存 CabinetType

若保存 NormalRingCabinet、IntegratedRingCabinet，可能产生：

- CabinetType=Normal 但包含 IntegratedFeederInterval；
- 混合柜无法明确分类；
- CabinetType 与 CompositionKind 重复；
- 模板分类被误当作电气约束；
- 需要额外 DTO 和迁移，却没有新增专业事实。

只有未来经厂家资料确认，需要表达柜体系列、壳体能力或自动化硬件能力时，才应设计独立 `CabinetStructureKind`。它也不能决定 Intervals。

### 5.3 决策

推荐方案 A：CabinetType 不进入当前 Domain。

它属于模板分类和生成来源信息。生成后的柜体组成继续由现有 Intervals 派生，不持久化 Template CabinetType 或 TemplateId。

## 6. EquipmentConfiguration 与 Domain 映射

EquipmentConfiguration 是创建描述，不是生成后需要复制保存的 Domain 对象。

映射原则：

```text
Template EquipmentConfiguration
→ RingCabinetIntervalDefinition
→ RingCabinet.Create
→ actual Domain structure
```

### 6.1 LoadSwitch 配置

```text
LoadSwitchBayEquipmentConfiguration
→ IntervalKind.LoadSwitchInterval
→ LoadSwitch + GroundSwitch
→ LoadSwitchThreePosition SwitchAssembly
→ Circuit/Earth Nodes + Terminals
```

### 6.2 IntegratedFeeder 配置

```text
IntegratedFeederBayEquipmentConfiguration
→ IntervalKind.IntegratedFeederInterval
→ CircuitBreaker + IsolationSwitch + GroundSwitch
→ IntegratedFeeder SwitchAssembly
→ GroundingStructureKind
→ MainBus/Intermediate/Circuit/Earth Node topology
→ Terminals
```

### 6.3 PT 配置

```text
PTBayEquipmentConfiguration
→ future IntervalKind.PTInterval
→ future controlled PT aggregate branch
```

当前不存在该目标分支，因此 Builder 返回 UnsupportedCapability。

### 6.4 决策

EquipmentConfiguration 不以模板配置对象或枚举副本进入 Domain。

生成后，实际设备、IntervalKind、GroundingStructureKind、SwitchAssembly、Terminal 和 ElectricalNode 是权威事实。Builder 不保存“EquipmentConfiguration=IntegratedFeeder”作为第二来源，也不由 Layout 或 Rendering 重建该配置。

## 7. PT 和 DTU 边界确认

### 7.1 PT

PT 是一次系统 Bay，不是普通 Attachment、Cabinet Module 或 CableTermination。

当前要生成 PT 至少需要：

- PTInterval Domain；
- PT Device 或等价明确的一次设备表达；
- PT 专用 Terminal；
- ElectricalNode 与固定拓扑；
- 隔离刀/断路器两种受控方案；
- 接地刀语义与安全规则；
- PT Layout；
- Rendering；
- Selection/Inspector；
- Persistence DTO 与 FormatVersion 决策。

这些能力未完成，因此当前 Builder 不生成 PT。PTBay capability 缺失时必须在创建任何对象前失败，不得忽略 PT 后生成剩余 Bays。

### 7.2 DTU

DTU 属于 Template `SecondaryConfiguration`，不属于一次 Bay，也不应作为 RingCabinetInterval。

DTU 是否最终成为独立二次 Domain 对象，或仅成为专用 RuntimeLayout/设备配置事实，尚未完成生产设计。本阶段不把 DTU 字段加入 RingCabinet Domain。

当前 Builder 遇到 DTUSecondary requirement 时返回 UnsupportedCapability。DTU 不影响当前 RingCabinet 一次聚合，不能由 PT、Attachment 或布局坐标间接表达。

## 8. Selection 与 Inspector 影响

### 8.1 Selection

Bay Index 和 BayFunction 都属于已有 RingCabinetInterval 的属性，不改变对象身份或父子关系。

因此不需要新增 SelectionTargetKind，也不需要修改 SelectionReference 结构。现有：

```text
SelectionTargetKind.RingCabinetInterval
ObjectId = IntervalId
ParentId = RingCabinetId
```

仍然足够。

SelectionObjectResolver 只需继续解析同一 Interval 对象；若字段直接位于 RingCabinetInterval，Resolver 不需要新的查找路径。具体代码是否需要调整取决于 ResolvedSelection 是否已经暴露完整 Interval，目前架构已具备该对象引用。

### 8.2 Inspector

Inspector 应扩展 RingCabinetInterval 的只读投影：

- Bay Index；
- BayFunction。

第一笔 Domain 补充建议只读显示，不同时实现编辑。原因是 Index 重编号、Function 修改及其专业校验尚未设计。

若未来允许编辑，必须使用类型化 Command，不得由 Inspector 直接修改 Domain。

## 9. Persistence 影响评估

### 9.1 DTO

若 Bay Index 和 BayFunction 进入 Domain，`ProjectRingCabinetIntervalDto` 必须增加对应字段，保存和恢复路径必须覆盖它们。

`RingCabinetIntervalRestoreDefinition`、`RingCabinetIntervalDefinition` 和聚合校验也需要对称承载。

### 9.2 FormatVersion

这两个字段是新的长期专业事实，现有 FormatVersion 2 无法保存，因此需要工程格式升级。

推荐新增 FormatVersion，而不是在 Version 2 DTO 中静默增加必填字段。原因：

- 旧项目没有 Index 和 Function；
- 不能从 DisplayName 可靠解析 Index；
- 不能从 IntervalKind 推断 Function；
- 静默填默认值会制造未经确认的专业事实。

### 9.3 Migration

旧项目迁移必须诚实表达未知值。可选策略需要另行设计：

- 允许新字段在迁移后处于明确 `Unspecified` 状态；
- 加载旧项目后要求用户确认；
- 提供显式迁移向导。

不推荐：

- 从“1号间隔”“负5间隔”等 DisplayName 自动解析；
- 使用 Sequence 自动填 Index；
- 所有旧间隔默认 Outgoing；
- 根据 LoadSwitch/IntegratedFeeder 自动推断 Function。

BayFunction 目标枚举是否包含 `Unspecified`，以及新建对象是否允许该值，必须在 Domain 补充设计中明确。迁移兼容值不能被误当作正常新建业务输入。

### 9.4 第一版策略

推荐先完成独立的最小 Domain/Persistence 设计和迁移决策，再实现生产 Builder。

在该阶段完成前，可以实现不写工程的纯 Runtime Model 类型及校验测试，但不建议把会丢失 Index/Function 的 Builder 接入 Desktop 创建闭环。

## 10. 最终 Domain Compatibility Decision

### 10.1 Bay Index

结论：进入 Domain。

它是现场间隔编号，与 Sequence、DisplayName 分离。只冻结正整数，不自行定义连续、自动编号或重排规则。

### 10.2 BayFunction

结论：进入 Domain。

它是长期电气用途事实，不能从 IntervalKind 或设备结构推断。具体枚举兼容、修改规则和旧项目迁移值需后续设计。

### 10.3 CabinetType

结论：不进入 Domain。

它是模板分类/生成来源。实际组成由 Intervals 派生；未来厂家结构能力另行建模。

### 10.4 EquipmentConfiguration

结论：不以模板配置对象进入 Domain。

它映射成 IntervalKind、GroundingStructureKind、实际 Switch、Terminal、Node、SwitchAssembly 和拓扑事实。

### 10.5 PT / DTU

结论：阻断对应能力分支，不阻断纯 Runtime Model 设计。

- PT 阻断 PT-capable Builder；
- DTU 阻断 DTU Secondary Builder；
- 两者不阻断只支持当前 LoadSwitch/IntegratedFeeder 的模型类型和纯校验；
- 如果生产 Builder 被要求无损保存所有模板语义，则 Index/Function Domain 补充是当前前置阻断。

### 10.6 第一版 Builder 是否需要 Domain 修改

结论分层：

- 纯技术原型、只验证当前设备结构生成：不需要 Domain 修改，但会丢弃 Index/Function，不能作为完整生产闭环；
- 生产级第一版、要求保留 Template Runtime Model 的长期专业事实：需要最小 Domain/Persistence 补充；
- 不需要大规模 Domain 重构。

## 11. 实施方案选择

### 11.1 方案 A：无需 Domain 修改，直接 Builder

适用于短生命周期原型或单元验证。风险是生成后丢失 Index/Function，并在未来引入迁移债务。

不推荐作为生产路线。

### 11.2 方案 B：最小 Domain 补充

内容：

- RingCabinetInterval 增加 Bay Index；
- RingCabinetInterval 增加 BayFunction；
- 创建定义和 Restore 定义对称增加字段；
- DTO 与格式版本升级；
- 旧项目迁移明确 Unknown/Unspecified 语义；
- Inspector 增加只读投影；
- Selection、CommandStack 和 Rendering 保持不变。

推荐。

### 11.3 方案 C：较大 Domain 重构

例如新增独立 Bay 聚合根、RingCabinetInstance、模板引用、厂家柜体层或自由设备配置对象。

当前没有必要，也会破坏现有聚合边界。

不推荐。

## 12. 推荐实施路线

### P0-7-D-2-A：专业字段最小设计确认

冻结：

- Domain 字段命名；
- Bay Index 最小不变量；
- BayFunction 枚举；
- 新建是否允许 Unspecified；
- 旧项目迁移策略；
- PT Function 的未来兼容方式。

不得在本阶段定义自动编号、比例、进出线数量或用途推断规则。

### P0-7-D-2-B：Domain 与 Persistence 最小补充

实施：

- RingCabinetInterval / Definition / RestoreDefinition；
- RingCabinet.Create / Restore 参数传递与校验；
- DTO 和 FormatVersion；
- 旧版本迁移；
- Domain/Persistence round-trip 测试。

保持：

- RingCabinet 聚合根不变；
- 内部拓扑工厂不变；
- Stable ID 不变；
- Existing Commands 的原子边界不变。

### P0-7-D-2-C：Inspector 只读投影

在现有 Interval Selection 上显示 Index 与 Function，不新增 SelectionTargetKind，不实现编辑。

### P0-7-D-3：Template Runtime Model 与 Builder

字段可无损映射后，再实现：

```text
BayTemplate.Index → RingCabinetInterval.BayIndex
BayTemplate.Function → RingCabinetInterval.BayFunction
EquipmentConfiguration → existing Domain structure
```

复用 AddRingCabinetCommand；Redo 不重新 Build。

### PT / DTU 独立路线

PT 与 DTU 继续作为独立专业模型阶段，不塞入最小 Domain 补充提交。

## 13. 非阻断问题

- CabinetType 的具体模板枚举命名尚可在 Runtime Model 实现审查中调整；
- Capability 标识格式尚未冻结；
- LayoutRule 第一版可继续只支持默认规则；
- Domain 内部 ID 仍由 RingCabinet.Create 生成；
- Template 来源暂定 C# 内置定义。

这些问题不要求修改现有 RingCabinet 聚合边界。

## 14. 下一步建议

下一步不要直接实现 Builder。先进行 P0-7-D-2-A 专业字段最小设计审查，重点由用户/专业人员确认：

1. Bay Index 是否确实需要在重命名后长期保持；
2. Index 是否允许重复、缺号和非连续；
3. BayFunction 第一版枚举是否完整；
4. 新建对象是否允许 Unspecified；
5. FormatVersion 2 旧项目如何迁移未知 Index/Function。

在这些规则确认前，不应自行添加默认值或推断逻辑。

## 15. 范围确认

本次只新增：

- `docs/p0-7-d-2-domain-compatibility-decision.md`。

未修改 src、Domain、Persistence、FormatVersion、CommandStack、Selection、Rendering、UI 或 Existing Commands；未实现 Builder。
