# P0-7-D-1 Template Runtime Model Design

> 状态：Template Runtime Model 设计稿；不包含 Template class、Builder、JSON parser、Runtime code 或 UI 实现。<br>
> 基线：checkpoint commit `e07681dd1df71c9c433a1197121872c48ffdf9a4`。<br>
> 上游设计：`docs/template-system-design.md`、`docs/template-builder-design.md`、`docs/p0-7-c-builder-readiness-review.md`。<br>
> 当前能力边界：生产 Domain 只支持 LoadSwitchInterval 与 IntegratedFeederInterval；PTInterval 和 DTU 生成尚不受支持。

## 1. Template Runtime Model 总体边界

Template Runtime Model 是 Builder 的不可变输入描述，位于模板来源与现有工程模型之间：

```text
Template source
      |
      v
Template Runtime Model
      |
      v
Builder Input
      |
      v
Domain + RuntimeLayout
```

它负责描述：

- 柜型分类；
- 有序 Bay；
- Bay 电气功能；
- 受控设备配置；
- 二次配置需求；
- LayoutRule；
- Builder 必须具备的能力。

它不负责：

- 创建 DrawingScene、SceneElement、Symbol 或 WPF 类型；
- Rendering；
- Selection 或 SelectionTransition；
- Command、CommandStack、Undo/Redo 或 Dirty；
- Inspector；
- ProjectRuntimeSession；
- Persistence DTO、FormatVersion 或工程保存；
- 生成后的 Stable ID；
- 具体模板实例的文档坐标。

Template Runtime Model 是描述，不是项目状态。Builder 消费它并返回新的 Domain 聚合和 RuntimeLayout；只有现有 Add Command 成功执行后，项目状态才发生变化。

## 2. RingCabinetTemplate 类型模型

建议将 `RingCabinetTemplate` 设计为不可变的、来源无关的模板定义：

```text
RingCabinetTemplate
├── TemplateId
├── Name
├── CabinetType
├── Bays[]
├── SecondaryConfiguration
├── LayoutRule
└── RequiredCapabilities (derived)
```

### 2.1 字段职责

| 字段 | 职责 | 是否进入生成后的 Domain |
| --- | --- | --- |
| `TemplateId` | 模板定义的逻辑身份，用于模板目录、诊断和来源区分 | 否 |
| `Name` | 面向用户的模板名称 | 否；不能自动成为 RingCabinet.DisplayName |
| `CabinetType` | 模板分类和默认策略提示 | 否；不能覆盖 Bays 的实际配置 |
| `Bays` | 按物理排列顺序保存 BayTemplate | 通过 Builder 映射为实际 Interval 定义 |
| `SecondaryConfiguration` | 描述 DTU 等二次配置需求 | 当前不生成；由能力检查拒绝 |
| `LayoutRule` | 引用受控布局生成规则 | 生成后的具体几何进入 RuntimeLayout |
| `RequiredCapabilities` | 根据模板结构派生的 Builder 能力需求集合 | 否 |

### 2.2 TemplateId

`TemplateId` 是模板定义的逻辑标识，不是 RingCabinetId，也不是 Template 实例 ID。建议使用稳定、可读、带命名域的字符串值，例如：

```text
builtin:ring-cabinet/load-switch-4
organization-x:ring-cabinet/integrated-6
```

第一版不需要把 TemplateId 写入工程文件，也不使用它派生 Domain GUID。两个用户操作即使选择同一 TemplateId，也必须生成彼此独立的 RingCabinet 聚合和 Stable ID 图。

若未来模板内容需要版本化，应增加独立 `TemplateVersion`，不要把版本隐含在 Domain Object ID 中。本阶段不冻结版本格式。

### 2.3 Name 与 CabinetType

`Name` 只用于模板选择界面或诊断。Builder 仍应从单独 CreationContext 接收本次实例的 RingCabinet DisplayName，避免模板名与项目设备名耦合。

`CabinetType` 是模板分类，不是所有 Bay 结构的事实来源。第一版可使用 P0-7-A 已定义的概念分类，例如 `Conventional` 与 `PrimarySecondaryIntegrated`。混合组成仍从 Bays 的 EquipmentConfiguration 派生，不新增“柜型决定全部间隔”的规则。

### 2.4 不应存在的字段

RingCabinetTemplate 不应保存：

- RingCabinetId、IntervalId、SwitchId、TerminalId 或 ElectricalNodeId；
- DrawingDocument、RuntimeLayoutDocument 或任何 Domain Object 引用；
- 实例 PlacementOrigin；
- Bay 或 Switch 的最终 DocumentPoint；
- SelectionReference；
- Command；
- Scene、Symbol 或 WPF 类型；
- Persistence DTO；
- 当前运行状态或用户已确认的现场 SwitchState。

模板集合和 Bays 集合必须在构造后不可变，并对输入集合进行防御性复制，避免 Builder 执行期间被 UI 或模板来源修改。

## 3. BayTemplate 设计

P0-7-A 已冻结 BayTemplate 的核心三字段模型：

```text
BayTemplate
├── Index
├── Function
└── EquipmentConfiguration
```

### 3.1 Index

`Index` 表示现场“负 N 间隔”中的正整数 N，不是 Bays 集合的数组下标，也不是物理排序键。

```text
Index = 5
Display projection = 负5间隔
```

禁止：

```text
Index = -5
```

规则：

- Index 必须大于 0；
- 同一个 RingCabinetTemplate 内 Index 必须唯一；
- Bays 集合顺序表示物理排列顺序；
- Builder 不根据 Index 自动重新排序；
- Index 不推断 BayFunction 或 EquipmentConfiguration；
- Index 不要求连续，除非未来经专业确认另有规则。

### 3.2 Optional Name

第一版不在 BayTemplate 增加 Optional Name。

原因：

- P0-7-A 已冻结三字段模型；
- 标准显示可由 Index 确定性投影为“负 N 间隔”；
- Optional Name 会引入“现场编号”和“显示名称”谁是事实源的问题；
- 当前 Domain 只有 Interval.DisplayName，没有独立现场 Index 字段。

Builder 第一版可以把 `Index = N` 映射为 Domain Interval.DisplayName 的默认值“负N间隔”。这只是创建期映射，不代表当前 Domain 已能独立查询或持久化 Index。

如果未来必须允许自定义 Bay 名称，应单独设计 `DisplayNameOverride`，并明确它不能替代 Index；在 Domain 能表达二者之前不加入第一版 Runtime Model。

### 3.3 Index 的现有 Domain 映射风险

当前 RingCabinetInterval 只有：

- `Sequence`：物理顺序；
- `DisplayName`：显示文本。

没有独立的现场 Index。因此模板生成后完全脱离模板时，Index 只能通过初始 DisplayName 间接表现；用户重命名后无法再可靠恢复 Index。

这不是 Runtime Model 的字段设计问题，而是现有 Domain 表达边界。进入实现前必须确认：

- 若 Index 只是创建时的标准显示输入，第一版映射到 DisplayName 可接受；
- 若 Index 是后续查询、专业规则或工作票必须使用的持久事实，则需要独立 Domain/Persistence 设计，不能依赖名称解析。

本阶段不替专业业务做该决定。

## 4. BayFunction 设计

`BayFunction` 表示电气功能，不表示设备类型。

第一版目标值：

| 值 | 语义 |
| --- | --- |
| `Incoming` | 进线功能 |
| `Outgoing` | 出线功能 |
| `Tie` | 联络功能 |
| `PT` | 电压互感器 Bay 功能 |
| `Metering` | 计量功能 |
| `Reserve` | 备用功能 |

同一个 Function 可以对应不同 EquipmentConfiguration。例如 Outgoing 可以使用负荷开关配置，也可以使用一二次融合配置。反向也不能从设备组合推断唯一 Function。

### 4.1 扩展机制

第一版建议使用封闭枚举或等价受控值集合，不使用任意字符串，也不提供插件动态注册 Function 的机制。

新增 Function 必须伴随：

- 明确的专业语义；
- 与 EquipmentConfiguration 的兼容性规则；
- 必要的 Builder capability；
- 若需生成后保留，则有对应 Domain/Persistence 设计。

通过新版本扩展枚举比使用 `Unknown` 或自由字符串更安全。Builder 对未知值必须失败，不能当作 Outgoing 或 Reserve 处理。

### 4.2 Function 的现有 Domain 映射风险

当前 RingCabinetInterval 不保存 Incoming、Outgoing、Tie、PT、Metering 或 Reserve。对现有 LoadSwitch/IntegratedFeeder Builder 来说，Function 只能用于模板校验和创建期描述，生成后不会成为 Domain 事实。

第一版必须明确这一限制：

- Builder 不得把 Function 编码进 DisplayName 后再解析；
- Rendering、Selection 和 Inspector 不得从设备组合猜 Function；
- Function 不得静默参与生成后专业计算；
- 如果后续工作票或拓扑规则需要 Function，必须先增加独立 Domain/Persistence 表达。

在该决定完成前，Function 是 Template Runtime 的创建期元数据，不是生成后工程事实。

## 5. EquipmentConfiguration 设计

EquipmentConfiguration 描述一个 Bay 采用的受控设备组合和创建所需参数。它不是 Domain Object，也不是任意设备清单。

### 5.1 推荐模型：受控变体

建议采用封闭的不可变配置变体：

```text
BayEquipmentConfiguration
├── LoadSwitchBayEquipmentConfiguration
├── IntegratedFeederBayEquipmentConfiguration
└── PTBayEquipmentConfiguration
```

不推荐：

```text
EquipmentKinds = [LoadSwitch, CircuitBreaker, PT, ...]
```

自由列表无法保证设备数量、顺序、GroundingStructureKind、Terminal 或拓扑完整性，并会迫使 Builder 复制 Domain 工厂规则。

### 5.2 LoadSwitchBayEquipmentConfiguration

结构语义：

```text
LoadSwitch
+ GroundSwitch
```

该配置本身不保存 SwitchDevice、Terminal、ElectricalNode 或 SwitchState。Builder 将其映射为当前 `RingCabinetIntervalDefinition.CreateLoadSwitch` 所需定义；内部对象由 `RingCabinet.Create` 创建。

当前 Domain 工厂需要合法技术初始化状态。该状态由 CreationContext 提供，第一版可继续使用 Open；它不是模板字段，也不是用户确认的现场运行状态。

### 5.3 IntegratedFeederBayEquipmentConfiguration

结构语义：

```text
CircuitBreaker
+ IsolationSwitch
+ GroundSwitch
```

必须包含当前 Domain 创建所需的 `GroundingStructureKind`：

- UpperIsolationGrounding；
- UpperLowerGrounding；
- LowerLowerGrounding。

该参数是 Domain 拓扑事实，不是 LayoutRule。Builder 将配置映射为 `CreateIntegratedFeeder` 定义，Domain 工厂根据 GroundingStructureKind 创建匹配节点、Terminal、SwitchAssembly 和固定拓扑。

### 5.4 PTBayEquipmentConfiguration

PT 只设计模板表达，当前不能生成。建议目标模型包含：

```text
PTBayEquipmentConfiguration
└── PrimaryControl
    ├── Isolator
    └── CircuitBreaker
```

PT、PT 专用端子和 GroundSwitch 是该受控配置的固定组成，不需要再作为自由列表重复保存。

PTBayEquipmentConfiguration 必须与 `BayFunction.PT` 配对。Builder 在 `PTBay` capability 不受支持时返回 UnsupportedCapability，不得映射为普通 IntegratedFeeder、Attachment 或 CableTermination。

PT 专用 Terminal、Node、接地路径和联锁尚未冻结，不在 Runtime Model 中伪造字段。

### 5.5 Function 与 EquipmentConfiguration 的最小兼容性

第一版只冻结可以确定的结构规则：

- `Function = PT` 必须使用 PTBayEquipmentConfiguration；
- PTBayEquipmentConfiguration 必须使用 `Function = PT`；
- LoadSwitch 和 IntegratedFeeder 配置不得用于 PT Function；
- 未确认 Incoming、Outgoing、Tie、Metering、Reserve 与两种现有配置之间的进一步限制。

Builder 不得自行建立未经确认的完整兼容矩阵。

## 6. Capability 模型设计

Capability 用于在任何 Domain 对象创建前判断 Builder 是否能完整生成某个模板。

```text
Template.RequiredCapabilities
          |
          v
Builder.SupportedCapabilities
          |
     set inclusion
      /          \
supported      missing
   |              |
continue     UnsupportedCapability
```

### 6.1 Capability 粒度

建议使用面向可完整生成特性的 capability，而不是每个叶子设备一个 capability：

| Capability | 表示的完整能力 |
| --- | --- |
| `BasicRingCabinet` | 可创建 RingCabinet 根聚合和默认布局 |
| `LoadSwitchBay` | 可生成完整 LoadSwitchInterval Domain + Layout |
| `IntegratedFeederBay` | 可生成完整 IntegratedFeederInterval Domain + Layout |
| `PTBay` | 可生成 PT Domain、专用拓扑、Layout 及后续必要表现 |
| `DTUSecondary` | 可处理 DTU SecondaryConfiguration 与对应 RuntimeLayout |
| `LayoutRule:<RuleId>` | 可解析并执行指定 LayoutRule |

不建议仅用 `Breaker` 表示 IntegratedFeeder 能力，因为“能够创建 CircuitBreaker”不等于能够创建完整 IntegratedFeeder Bay。

Capability 标识建议采用受控、可扩展的值对象或规范化字符串，而不是封闭枚举。原因是 LayoutRule 和未来设备族会持续增加，但扩展 capability 不应迫使旧 Builder 把未知能力当作已支持。

未知 capability 一律视为不支持。

### 6.2 RequiredCapabilities 的来源

`RequiredCapabilities` 建议是 RingCabinetTemplate 的只读派生属性，不由模板作者独立维护第二份列表。

派生规则示例：

- 任意 RingCabinetTemplate → BasicRingCabinet；
- 存在 LoadSwitchBayEquipmentConfiguration → LoadSwitchBay；
- 存在 IntegratedFeederBayEquipmentConfiguration → IntegratedFeederBay；
- 存在 PTBayEquipmentConfiguration → PTBay；
- SecondaryConfiguration 包含 DTU → DTUSecondary；
- LayoutRule 指向某规则 → LayoutRule:<RuleId>。

选择派生而非手工列表可以避免以下矛盾：

- 模板包含 PT Bay 但忘记声明 PTBay；
- 模板删除 DTU 后仍错误要求 DTU；
- EquipmentConfiguration 与 RequiredCapabilities 漂移。

若未来确有不容易从结构推导的外部能力，可以增加 `AdditionalRequiredCapabilities`，最终 RequiredCapabilities 为“结构派生集合 + 额外集合”。第一版不需要该扩展。

### 6.3 SupportedCapabilities 与失败结果

SupportedCapabilities 属于 Builder 运行环境或明确的 Builder implementation profile，不属于 Template，也不进入工程 Persistence。

Builder 必须在生成任何 GUID 或 Domain 对象之前计算：

```text
MissingCapabilities = RequiredCapabilities - SupportedCapabilities
```

若集合非空，返回 `UnsupportedCapability`，并报告全部缺失 capability。不得：

- 部分生成支持的 Bay；
- 忽略 PT 或 DTU；
- 用相似设备替代；
- 返回缺少 Layout 的 Domain；
- 通过 Rendering 临时补齐事实。

当前第一版 Builder profile 可支持：

- BasicRingCabinet；
- LoadSwitchBay；
- IntegratedFeederBay；
- 现有默认 LayoutRule。

PTBay 与 DTUSecondary 当前不支持。

## 7. LayoutRule 模型设计

LayoutRule 描述可复用的布局生成规则，不保存模板实例坐标。

### 7.1 推荐边界

第一版建议 RingCabinetTemplate 保存 `LayoutRuleReference`：

```text
LayoutRuleReference
└── RuleId
```

Builder/布局策略注册表根据 RuleId 解析受控规则。Template 不直接持有 LayoutFactory、委托、WPF 对象或 RuntimeLayout 实例。

第一版只支持当前 RingCabinetLayoutFactory 对应的默认 RuleId，例如：

```text
builtin:ring-cabinet/default
```

该规则的权威实现仍在唯一的布局生成策略中，不能在 Template 与 Factory 各保存一套不同默认数值。

### 7.2 未来参数化规则

未来受控 LayoutRule 可以描述：

- BayWidth；
- BaySpacing；
- CabinetPadding；
- MainBus relative position；
- PT 特殊宽度；
- DTU 左右排列策略；
- 标签偏移策略。

仍然禁止保存：

```text
Bay1.X = 100
Bay2.X = 165
SwitchA.Y = 40
```

这些是具体实例布局，应由 LayoutFactory 计算后进入 RingCabinetLayout。

### 7.3 绑定与复用

推荐 LayoutRule 是独立、可复用、只读的规则定义，通过引用与多个 Template 关联。这样可以：

- 保证同一规则只有一个权威实现；
- 统一 capability 检查；
- 在模板之间复用；
- 避免复制尺寸参数后发生漂移。

规则升级必须使用新的 RuleId 或明确版本，不应静默改变已发布模板的初始生成结果。本阶段不设计规则仓库或版本解析器。

## 8. 模板实例与 Domain 对象关系

### 8.1 方案 A：生成后完全脱离模板

生成完成后只保留：

- RingCabinet Domain 聚合；
- RingCabinetLayout；
- 现有 Command 历史和 SelectionTransition 会话状态。

优点：

- 不修改 Domain 或 Persistence；
- 模板后续变化不影响已创建工程对象；
- Undo/Redo 只恢复固定对象；
- Rendering、Selection 和 Inspector 不依赖模板目录；
- 工程文件可独立打开。

缺点：

- 无法直接回答“该柜由哪个模板生成”；
- 无法自动重新套用模板更新；
- 只存在于模板的 Function/Index 语义不会自动成为持久 Domain 事实。

### 8.2 方案 B：Runtime 保存 TemplateReference

优点：

- 可以显示来源和模板版本；
- 可支持未来迁移、比较或重新应用模板。

缺点：

- TemplateReference 会成为新的工程事实；
- 需要 DTO、FormatVersion、缺失模板处理和版本兼容策略；
- 容易让 Rendering 或业务逻辑在运行时依赖模板；
- 模板更新可能改变既有工程语义；
- 超出 P0-7 第一版范围。

### 8.3 第一版推荐

推荐方案 A：生成后完全脱离模板。

BuildResult 可以在当前创建流程中短暂携带 TemplateId 作为日志或诊断信息，但现有 Add Command、Domain、RuntimeLayout 和 Persistence 不保存该引用。

任何必须在生成后长期使用的专业事实都应进入正式 Domain，而不是依靠 TemplateReference。Function 与 Index 的长期语义因此必须在 Builder 实现前按第 3.3 和 4.2 节确认。

## 9. 模板来源设计

### 9.1 方案 A：C# 对象定义

优点：

- 编译期类型安全；
- 容易先验证不可变模型和 Builder 映射；
- 不需要同时设计 schema、版本迁移和错误定位；
- 适合第一版内置模板。

缺点：

- 新模板需要发布应用版本；
- 非开发人员不能直接维护。

### 9.2 方案 B：JSON 文件

优点：

- 模板可独立分发和维护；
- 适合未来厂家或组织模板库。

缺点：

- 需要 schema、版本、解析错误、未知字段和安全边界；
- 需要将外部数据规范化为同一 Runtime Model；
- 当前阶段容易把来源格式与核心模型耦合。

### 9.3 方案 C：混合方式

内置模板由 C# 定义，外部 JSON 通过适配器解析成同一个 Runtime Model。

长期最灵活，但第一版同时实现两种来源会扩大验证范围。

### 9.4 推荐

第一版采用方案 A：C# 内置不可变对象定义。

Runtime Model 必须保持来源无关，使未来 JSON parser 只负责：

```text
JSON DTO
→ parse and validate
→ Template Runtime Model
```

Builder 只消费 Runtime Model，不知道模板来自 C#、JSON 或其他来源。本阶段不实现任何解析。

## 10. 与 Builder 的接口关系

概念接口：

```text
TemplateDefinition
+ CreationContext
→ TemplateBuilder
→ BuildResult
```

其中：

### TemplateDefinition

提供：

- RingCabinetTemplate；
- Bays 与 EquipmentConfiguration；
- 派生 RequiredCapabilities；
- LayoutRuleReference。

### CreationContext

提供不属于模板的本次实例信息：

- RingCabinet DisplayName；
- PlacementOrigin；
- CabinetId 生成入口；
- 当前合法的技术初始化状态；
- Builder SupportedCapabilities / implementation profile；
- LayoutRule resolver。

CreationContext 不包含 ProjectRuntimeSession、CommandStack、Selection 或可写工程对象。

### BuildResult

至少包含：

- 创建完成的 RingCabinet 聚合；
- 匹配的 RingCabinetLayout；
- 从根对象派生的只读 RootIdentity。

BuildResult 不保存扁平内部对象副本，不创建 SelectionReference，也不进入 Persistence。

Builder 消费流程：

```text
validate Template Runtime Model
→ derive RequiredCapabilities
→ compare SupportedCapabilities
→ map supported Bay configurations
→ RingCabinetDefinition
→ RingCabinet.Create
→ RingCabinetLayoutFactory
→ BuildResult
```

Capability 检查和全部模板结构校验应发生在首次 ID 分配及 Domain 创建之前。Domain 工厂仍是最终聚合合法性边界。

## 11. 与现有 P0-6 架构兼容性

Template Runtime Model 不改变现有编辑架构。

生成后的流程仍为：

```text
BuildResult(RingCabinet + RingCabinetLayout)
→ AddRingCabinetCommand
→ CommandStack
→ Scene rebuild
→ SelectionTransition / Selection
→ Inspector
→ Existing Rendering
```

兼容原则：

- Add Command 保存首次 BuildResult 的真实对象；
- Undo/Redo 不重新解析 Template 或重新 Build；
- Selection 使用现有 RingCabinetId / IntervalId；
- Resolver 不读取 Template；
- Inspector 投影现有 Domain + Layout；
- Rendering 不读取 Template；
- FormatVersion 2 不保存 Template、TemplateId 或 RequiredCapabilities；
- Save/Load 继续保存当前已支持的 Domain + RuntimeLayout。

第一版不修改 SelectionReference、SelectionObjectResolver、CommandStack、Persistence、Rendering 或 Existing Commands。

## 12. 第一版支持范围

### 12.1 Runtime Model 可表达范围

设计模型可表达：

- 普通 RingCabinetTemplate；
- LoadSwitch Bay；
- IntegratedFeeder Bay；
- Mixed Bays；
- Incoming、Outgoing、Tie、PT、Metering、Reserve Function；
- PT 两种 PrimaryControl 目标方案；
- DTU SecondaryConfiguration 需求；
- 可复用 LayoutRuleReference；
- 派生 RequiredCapabilities。

“可表达”不等于“当前可生成”。

### 12.2 第一版 Builder 必须支持

- 普通环网柜模板输入；
- LoadSwitchBayEquipmentConfiguration；
- IntegratedFeederBayEquipmentConfiguration；
- 三种现有 GroundingStructureKind；
- 当前 Domain 允许的纯类型和 Mixed Bay 数量；
- 由 Bays 集合决定并可在未来扩展的数量模型；
- 当前默认 RingCabinet LayoutRule；
- UnsupportedCapability 失败语义。

Bay 数量模型不把 4 或 6 写死为模板类型；实际 Build 仍必须通过当前 Domain 规则：纯 LoadSwitch 为 3 至 6，纯 IntegratedFeeder 为 4 或 6，Mixed 服从现有聚合校验。

### 12.3 第一版暂不支持生成

- PT Bay；
- DTU；
- 厂家差异模板；
- 自定义 Bay Name；
- 参数化 LayoutRule 数值编辑；
- TemplateReference 持久化；
- JSON 模板；
- 自动电气计算；
- 网络拓扑分析；
- 高级参数编辑；
- 已存在 RingCabinet 的模板重套。

PT/DTU 模板必须在 capability 检查阶段失败，不得部分生成其他 Bays。

## 13. 未解决问题与编码前决策

### 13.1 Bay Index 是否是持久专业事实

当前只能映射为 Interval.DisplayName。若 Index 需要在重命名后保持或参与工作票规则，则必须先设计独立 Domain 字段和 Persistence。

### 13.2 BayFunction 是否是持久专业事实

当前 Domain 不保存 Function。若 Function 只用于模板选择和创建期校验，可以在生成后丢弃；若后续业务需要查询 Incoming/Outgoing/Tie 等，必须先增加 Domain/Persistence 表达。

### 13.3 CabinetType 是否需要持久化

当前建议它只作为模板分类，实际组成由 Bays 决定。如果未来厂家柜体能力依赖 CabinetType，必须另行确认其 Domain 语义。

### 13.4 Capability 标识版本

第一版需冻结 capability 的规范化命名、大小写和比较规则。未知 capability 必须失败；不能静默忽略。

### 13.5 LayoutRule 版本

第一版仅支持一个内置默认 RuleId。未来参数化和版本策略尚未设计。

这些问题中，13.1 和 13.2 是进入 Builder 生产实现前最重要的业务边界确认；其余可通过第一版窄范围处理。

## 14. 推荐方案与下一步

推荐方案：

- RingCabinetTemplate 使用不可变、来源无关模型；
- TemplateId 是模板逻辑身份，不是 Domain ID；
- BayTemplate 保持 Index、Function、EquipmentConfiguration 三字段；
- 第一版不增加 Optional Name；
- EquipmentConfiguration 使用受控变体，不使用任意设备列表；
- RequiredCapabilities 从模板结构派生；
- Builder 先比较能力集合，缺失时返回 UnsupportedCapability；
- LayoutRule 通过可复用 RuleId 引用；
- 生成后不持久化 TemplateReference；
- 第一版模板来源使用 C# 内置对象，未来 JSON 适配到同一 Runtime Model；
- 当前只生成 LoadSwitch、IntegratedFeeder 和现有合法 Mixed 柜；
- PT/DTU 只表达需求，不生成伪模型。

下一步建议先进行 P0-7-D-1 Runtime Model Implementation Readiness Review，重点确认：

1. Index 和 Function 是创建期元数据还是必须进入 Domain 的专业事实；
2. 第一版 capability 标识集合；
3. Template Runtime Model 应放置的程序集及依赖方向；
4. 与现有 RingCabinetCreationConfiguration 的复用或隔离边界；
5. 第一版是否只提供一个默认 LayoutRuleReference。

上述决策确认后，再实现纯 Template Runtime 类型；不要同时实现 Builder、UI 或 JSON parser。

## 15. 范围确认

本次只新增：

- `docs/template-runtime-model-design.md`。

未修改 src、Domain、Persistence、FormatVersion、CommandStack、Selection、Rendering、UI 或 Existing Commands；未实现 Template class、Builder、JSON parser 或任何 Runtime code。
