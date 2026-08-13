# P0-7-E-2-A Template Runtime Model Implementation Plan

> 状态：生产实现前计划；本文件不包含 Runtime Model、Builder、Command、UI 或 Persistence 的代码修改。<br>
> 基线：commit `def7f28e9786e4dde6c8ac108c80987ed25271cf`。<br>
> 上游设计：`p0-7-e-1-template-builder-runtime-design.md` 以及 P0-7-D 已冻结的 Bay Metadata、Persistence Version 3 和 Migration 边界。

## 1. Runtime Model 所属边界

### 1.1 当前项目依赖事实

当前生产项目依赖方向为：

```text
DistributionDrawing.Domain
        ^
        |
DistributionDrawing.Application

DistributionDrawing.Domain
        ^
        |
DistributionDrawing.Rendering.Wpf

Application + Infrastructure + Rendering.Wpf
        ^
        |
DistributionDrawing.Desktop
```

`DistributionDrawing.Application` 当前是平台无关的 `net10.0` 项目，并且只引用 Domain。`DistributionDrawing.Rendering.Wpf` 是 `net10.0-windows` 项目，拥有现有 `RingCabinetLayout`、`RingCabinetLayoutFactory`、设备创建 Factory 和 Command。

### 1.2 方案比较

| 位置 | 结论 | 原因 |
| --- | --- | --- |
| Domain | 不采用 | Template 是创建描述，不是生成后长期存在的专业事实；Domain 不应引用 Template |
| Infrastructure | 不采用 | Infrastructure 适合未来 JSON、文件或数据库模板来源适配，不应拥有核心 Runtime Model |
| Rendering.Wpf | 不采用 | 会让平台无关模板描述被 Windows/WPF 目标框架污染 |
| 独立 Template 项目 | 暂不采用 | 语义清晰，但第一版模型规模小；立即增加项目、解决方案和引用成本超出最小实现需要 |
| Application | 推荐 | 适合作为用例输入模型；平台无关；已有指向 Domain 的合法依赖，可复用 BayFunction 与 GroundingStructureKind |

第一版建议把 Template Runtime Model 放入：

```text
src/DistributionDrawing.Application/Templates/RingCabinets/
```

建议 namespace：

```text
DistributionDrawing.Application.Templates.RingCabinets
```

依赖方向冻结为：

```text
Application Template Runtime Model
        |
        v
Builder implementation
        |
        v
RingCabinetDefinition / RingCabinet.Create
```

代码引用方向实际为 Builder 同时依赖 Application Runtime Model 和 Domain API。Domain 绝不引用 Application 或 Template 类型。

若未来模板运行时发展为跨多个应用复用的独立子系统，或需要脱离 Application 单独发布，再评估提取 `DistributionDrawing.Templates` 项目；E-2-A 不预先建立该层。

## 2. RingCabinetTemplate 设计

建议实现为不可变 sealed class，而不是带可写属性的 DTO：

```text
RingCabinetTemplate
├── TemplateId: TemplateId
├── Name: string
├── CabinetType: RingCabinetTemplateType
├── Bays: IReadOnlyList<BayTemplate>
├── LayoutRule: RingCabinetLayoutRule
├── SecondaryConfiguration: SecondaryConfiguration
└── RequiredCapabilities: IReadOnlySet<TemplateCapability> (derived)
```

### 2.1 构造与不可变性

构造函数负责：

- 拒绝 null；
- 规范化并校验 TemplateId 和 Name；
- 校验 CabinetType 是已定义值；
- 对 Bays 做一次物化和防御性复制；
- 拒绝空 Bays 和 null Bay；
- 校验 Bay Index 的正数及模板内唯一性；
- 保存不可变 LayoutRule 与 SecondaryConfiguration；
- 从模板内容派生 RequiredCapabilities，避免调用方维护第二份能力事实。

所有属性只读，不提供 public setter，不暴露调用方传入的可变集合。

### 2.2 TemplateId

建议新增轻量不可变 value type `TemplateId`，内部保存规范化的非空字符串，例如：

```text
builtin:ring-cabinet/load-switch-5
organization-x:ring-cabinet/integrated-6
```

TemplateId：

- 只标识模板定义；
- 不是 Guid 型 Domain ID；
- 不映射 RingCabinetId；
- 不参与任何 Stable ID 生成；
- 第一版不进入工程 DTO 或 FormatVersion；
- 不从模板名自动生成，以避免重命名改变身份。

### 2.3 Name 与 CabinetType

Name 是模板目录显示名称，不自动写入 `RingCabinet.DisplayName`。实例 DisplayName 由后续 Build Request 明确提供。

建议第一版 Template 专用枚举为：

```text
RingCabinetTemplateType
├── Conventional
├── PrimarySecondaryIntegrated
└── Mixed
```

该枚举只用于目录分类、能力展示和布局策略选择，不进入 Domain，也不能替代 Bays 的真实 EquipmentConfiguration。Builder 不根据 CabinetType 自动决定间隔类型、数量或比例。

## 3. BayTemplate 设计

建议使用不可变 sealed class 或 readonly record：

```text
BayTemplate
├── Index: int
├── Function: BayFunction
└── EquipmentConfiguration: BayEquipmentConfiguration
```

### 3.1 Index

Index：

- 必须大于 0；
- 在同一 RingCabinetTemplate 内唯一；
- 可以不连续；
- 原样映射 `RingCabinetInterval.BayIndex`；
- 不允许负数表示“负 N 间隔”。

BayTemplate 自身校验 Index 为正数；模板集合级唯一性由 RingCabinetTemplate 校验。Domain 继续执行自己的 BayIndex 不变量校验，作为最终事实边界。

### 3.2 Function

Function 直接使用现有 Domain `BayFunction`，避免 Template 与 Domain 定义两套同名电气功能枚举。Template Model 对新模板拒绝：

- 未定义枚举值；
- `BayFunction.Unknown`。

`BayFunction.PT` 可以作为能力需求被识别，但不能进入现有普通间隔创建映射；Builder 必须先返回 `UnsupportedCapability(PTBay)`。

### 3.3 Sequence

Template 不保存 Sequence。`RingCabinetTemplate.Bays` 的集合顺序就是物理排列顺序，Builder 按此顺序建立 `RingCabinetIntervalDefinition[]`。现有 `RingCabinet.Create` 负责产生从 1 开始、连续且唯一的 Domain Sequence。

禁止：

- 在 BayTemplate 增加 Sequence；
- 根据 Index 排序 Bays；
- 把 Sequence 写回 Index；
- 自动补齐 Index 缺号。

## 4. EquipmentConfiguration 设计

### 4.1 推荐结构

建议使用封闭的抽象基类加 sealed 派生类型：

```text
BayEquipmentConfiguration (abstract)
├── LoadSwitchConfiguration
└── IntegratedFeederConfiguration
```

不使用：

- `string DeviceType`；
- 任意字典；
- 自由设备列表；
- Domain Object 列表；
- Switch、Terminal 或 Node Stable ID。

抽象基类使 Builder 可进行穷尽的类型分派，并让未知配置明确失败。第一版不需要接口注册、插件系统或反射发现。

### 4.2 LoadSwitchConfiguration

第一版无额外专业参数，可实现为无状态 sealed record/class。Builder 将其映射为：

```text
RingCabinetIntervalDefinition.CreateLoadSwitch(
    bay.Index,
    bay.Function,
    SwitchState.Open,
    SwitchState.Open,
    intervalDisplayName)
```

`Open` 是当前 Domain 创建所需的技术初始化值，不是用户确认的现场运行状态；它不进入 Template 字段。

### 4.3 IntegratedFeederConfiguration

建议包含唯一必需参数：

```text
GroundingStructureKind GroundingStructureKind
```

Builder 将其映射为：

```text
RingCabinetIntervalDefinition.CreateIntegratedFeeder(
    bay.Index,
    bay.Function,
    configuration.GroundingStructureKind,
    SwitchState.Open,
    SwitchState.Open,
    SwitchState.Open,
    intervalDisplayName)
```

Switch、Terminal、ElectricalNode 和 SwitchAssembly 不由配置对象创建。它们继续由 `RingCabinet.Create` 根据 Interval Definition 生成。

## 5. GroundingStructureKind 边界

`IntegratedFeederConfiguration` 直接复用 Domain `GroundingStructureKind`，并在构造时拒绝未定义枚举值。

该值同时存在于两种不同职责中：

- Template 中：作为生成所需的输入描述；
- 生成后的 Domain 中：作为 RingCabinetInterval 的长期专业事实。

这不是重复事实源。Builder 将模板值一次性传入 Domain，生成后 Domain 成为权威事实；Template 不与已创建对象保持运行时同步关系。

Template 不增加 `GroundingSwitch` 布尔字段。IntegratedFeeder 的接地开关结构已经由 GroundingStructureKind 和 Domain Factory 表达，再增加布尔值会产生互相冲突的输入。

## 6. Capability 检查设计

### 6.1 Capability 模型

建议新增 Template 层枚举：

```text
TemplateCapability
├── BasicRingCabinet
├── LoadSwitchBay
├── IntegratedFeederBay
├── PTBay
├── DtuSecondary
└── LayoutRule
```

`RingCabinetTemplate.RequiredCapabilities` 从 Bays、LayoutRule 与 SecondaryConfiguration 派生，不接受调用方直接传入。

第一版 Builder 的 SupportedCapabilities 为：

- BasicRingCabinet；
- LoadSwitchBay；
- IntegratedFeederBay；
- 当前默认 LayoutRule。

### 6.2 UnsupportedCapability

Builder 在生成 CabinetId、Definition、Domain 聚合或 Layout 前比较 RequiredCapabilities 与 SupportedCapabilities。缺失能力时返回类型化失败：

```text
UnsupportedCapability
├── TemplateId
└── MissingCapabilities[]
```

PT 触发 `PTBay`，DTU 触发 `DtuSecondary`。当前必须拒绝，因为缺少相应的 Domain、Layout、Persistence 和 Rendering 完整能力。不得用 LoadSwitch、IntegratedFeeder、CableTermination 或纯图形占位模拟。

E-2-A 只实现能力描述和派生所需的模型；能力比较及失败结果由 E-2-B Builder Core 实现。

## 7. LayoutRule 设计

### 7.1 第一版表示

建议第一版使用不可变、强类型 `RingCabinetLayoutRule`，至少包含非空 `RuleId`：

```text
RingCabinetLayoutRule
└── RuleId: string
```

内建默认值例如：

```text
builtin:ring-cabinet/default-v1
```

RuleId 用于 Builder/Layout 策略解析，不进入 Domain 或工程 Persistence。它不参与 Stable ID。

### 7.2 规则与坐标边界

LayoutRule 可以选择包含以下规则的布局策略：

- Cabinet width 计算规则；
- Bay width；
- Bay spacing；
- Cabinet padding；
- 不同 EquipmentConfiguration 的初始开关排列。

Template 不保存：

- 某个 Bay 的最终 X/Y；
- Placement Position；
- IntervalId 或 SwitchId；
- SceneElement 或 WPF 类型。

当前 `RingCabinetLayoutFactory` 使用固定、确定性的初始策略，尚未接收参数化 LayoutRule。E-2-A 只实现 RuleId 模型；E-2-B 默认 RuleId 映射现有 Factory。真正参数化 width/spacing 必须在单独 LayoutFactory 设计中实现，不能把几何公式复制到 Builder。

## 8. SecondaryConfiguration 预留

建议使用不可变封闭模型：

```text
SecondaryConfiguration (abstract)
├── NoSecondaryConfiguration
└── DtuSecondaryConfiguration
```

`NoSecondaryConfiguration` 是第一版可成功 Build 的唯一配置。

`DtuSecondaryConfiguration` 只表达未来模板能力需求，可以预留强类型位置值，例如 Left/Right；它不创建一次 Terminal、ElectricalNode、Connection 或拓扑关系。Builder 看到该配置必须返回 `UnsupportedCapability(DtuSecondary)`。

SecondaryConfiguration 不进入当前 RingCabinet Domain，也不进入 Persistence Version 3。未来正式实现 DTU 前，需要独立冻结 DTU Domain、Layout、Rendering 和 Persistence 边界。

## 9. Validation 位置设计

### 9.1 Template Model 校验

Runtime Model 只负责结构完整性和无歧义输入：

- TemplateId、Name 非空；
- CabinetType、BayFunction、GroundingStructureKind 为已定义枚举值；
- Bays 非空且不含 null；
- Index 为正数且模板内唯一；
- 新模板拒绝 BayFunction.Unknown；
- EquipmentConfiguration、LayoutRule、SecondaryConfiguration 非 null；
- 所有集合防御性复制。

Template Model 不校验 Domain 的纯柜数量规则，不创建拓扑，也不判断工程 ID 冲突。

### 9.2 Builder 校验

Builder 负责创建能力和映射边界：

- RequiredCapabilities 是否受支持；
- PT、DTU 是否必须拒绝；
- EquipmentConfiguration 是否存在明确映射；
- LayoutRule 是否有已注册策略；
- Build Request 的实例 DisplayName 与 Placement Position 是否有效；
- Build 结果 CabinetId 与 Layout.CabinetId 是否一致；
- Layout 是否覆盖实际 Interval/Switch Stable ID。

Builder 不根据名称、数量、设备类型或 CabinetType 推断 BayFunction、BayIndex 或 GroundingStructureKind。

### 9.3 Domain 校验

Domain 继续拥有最终专业不变量：

- BayIndex、Function 与枚举合法性；
- 同柜 BayIndex 唯一；
- Unknown/PT 新建拒绝；
- IntervalKind 与设备结构；
- 纯 LoadSwitch/IntegratedFeeder 数量规则；
- SwitchState、GroundingStructureKind；
- Terminal、ElectricalNode、SwitchAssembly 和内部拓扑完整性。

Template/Builder 的友好前置校验不能替代 Domain 校验。当前纯 LoadSwitch 两间隔仍会被 Domain 拒绝；未经专业确认，E-2 不修改或绕过该规则。

## 10. Stable ID 边界

Template Runtime Model 不包含任何 Domain Stable ID，TemplateId 也不参与 ID 生成。

首次 Build 的顺序为：

1. 完成 Template 和 Capability 预检；
2. Builder 为 `RingCabinetDefinition` 生成新的 CabinetId；
3. 调用 `RingCabinet.Create`；
4. Domain Factory 首次生成 IntervalId、SwitchId、TerminalId、ElectricalNodeId 和 SwitchAssemblyId；
5. `RingCabinetLayoutFactory` 读取真实聚合 Stable ID 创建 Layout；
6. BuildResult 固定保存该聚合和 Layout。

第一版不为内部 ID 注入新的 Generator，因为当前 Domain Factory 自己创建这些 ID。若未来需要可预测 ID，应独立调整 Domain 创建 API，而不是让 Template 或 Builder 手工拼装内部对象。

Build 失败产生但未发布的 ID 可以丢弃。Redo 不再次 Build，因此不会生成新 ID。

## 11. BuildResult 设计

BuildResult 属于 E-2-B Builder 实现边界，不放入 E-2-A Runtime Model 文件组。建议不可变结构为：

```text
RingCabinetTemplateBuildResult
├── Definition: RingCabinetDefinition
├── Cabinet: RingCabinet
├── Layout: RingCabinetLayout
└── RequiredCapabilities: IReadOnlySet<TemplateCapability>
```

其中：

- Definition 是本次映射形成的完整创建输入，可用于诊断和映射测试；
- Cabinet 是完整、已验证的 Domain 聚合；
- Layout 与实际 Cabinet/Interval/Switch Stable ID 匹配；
- RequiredCapabilities 是本次实际消费能力的只读快照。

Builder 返回结果，不修改 DrawingDocument、RuntimeLayoutDocument、ProjectRuntimeSession、CommandStack 或 Selection。

BuildResult 不返回 DrawingScene、SelectionReference、DTO 或可变内部对象列表。

## 12. Command 集成预留

后续集成流程为：

```text
RingCabinetTemplate
        |
        v
TemplateBuilder.Build
        |
        v
RingCabinetTemplateBuildResult
        |
        v
AddRingCabinetCommand
        |
        v
CommandStack.ExecuteCommand
```

Builder 不创建或执行 Command。外层创建协调逻辑用 BuildResult 中同一个 Cabinet 和 Layout 构造现有 `AddRingCabinetCommand`。

Command 保存首次 BuildResult 所含对象。Undo 删除完整聚合和 Layout，Redo 恢复相同对象；禁止 Redo 重新运行 Builder。SelectionTransition 由 Desktop 成功执行后登记，不进入 Template 或 Builder。

## 13. 第一版明确不支持

E-2 第一版明确不支持：

- PT Template 的成功生成；
- DTU 自动生成；
- JSON Template、Schema、热加载或模板数据库；
- 厂家差异模板；
- 自动电气计算和网络拓扑分析；
- 自动 BayFunction 推断；
- 自动 BayIndex、DisplayName 或专业编号推断；
- TemplateReference 持久化；
- 已存在 RingCabinet 的结构重配置；
- Template 直接生成 DrawingScene；
- 为支持两间隔而绕过现有 Domain 数量规则。

## 14. 实际代码文件规划

### 14.1 E-2-A Runtime Model 新增文件

建议在现有 Application 项目新增：

```text
src/DistributionDrawing.Application/Templates/RingCabinets/
├── TemplateId.cs
├── RingCabinetTemplate.cs
├── RingCabinetTemplateType.cs
├── BayTemplate.cs
├── BayEquipmentConfiguration.cs
├── RingCabinetLayoutRule.cs
├── SecondaryConfiguration.cs
└── TemplateCapability.cs
```

职责：

| 文件 | 职责 |
| --- | --- |
| `TemplateId.cs` | 模板目录身份 value type 与字符串规范化 |
| `RingCabinetTemplate.cs` | 模板根、Bays 防御性复制、Index 唯一性、能力派生 |
| `RingCabinetTemplateType.cs` | Conventional/PrimarySecondaryIntegrated/Mixed 分类 |
| `BayTemplate.cs` | Index、BayFunction、EquipmentConfiguration |
| `BayEquipmentConfiguration.cs` | 抽象基类以及 LoadSwitch/IntegratedFeeder 强类型配置 |
| `RingCabinetLayoutRule.cs` | RuleId 和默认规则引用 |
| `SecondaryConfiguration.cs` | None/DTU 预留配置与能力标记 |
| `TemplateCapability.cs` | Builder 能力枚举 |

不修改 Domain 文件，不新增 Template DTO，不修改 FormatVersion。

### 14.2 E-2-A 测试规划

当前没有 Application.Tests。实现时建议新增最小平台无关测试项目：

```text
tests/DistributionDrawing.Application.Tests/
├── DistributionDrawing.Application.Tests.csproj
└── RingCabinetTemplateTests.cs
```

测试覆盖：

- TemplateId/Name 校验；
- Bays 防御性复制；
- Index 正数、唯一和非连续支持；
- Unknown/非法 Function 拒绝；
- IntegratedFeeder GroundingStructureKind 校验；
- RequiredCapabilities 派生；
- PT/DTU 能力标记；
- TemplateId 与 Domain ID 完全无关；
- Template 不包含 Sequence 和实例坐标。

若团队不希望 E-2-A 新增测试项目，可把建立 Application.Tests 作为单独验证提交；不建议把 Runtime Model 测试塞入 Domain.Tests，因为 Domain.Tests 不应引用 Application。

### 14.3 E-2-B Builder Core 预期文件

由于 BuildResult 需要 `RingCabinetLayout`，E-2-B 应放在能合法依赖 Application Runtime Model、Domain 和 Rendering Layout 的外层边界。按当前仓库最小改动，建议在 Rendering.Wpf 的现有创建基础设施附近新增：

```text
src/DistributionDrawing.Rendering.Wpf/Interaction/Templates/RingCabinets/
├── RingCabinetTemplateBuildRequest.cs
├── RingCabinetTemplateBuildResult.cs
├── RingCabinetTemplateBuilder.cs
└── TemplateBuildFailure.cs
```

并由 `DistributionDrawing.Rendering.Wpf.csproj` 引用 Application。依赖方向是外层 Rendering.Wpf → Application → Domain，不形成 Domain 反向依赖。

这是对当前项目把 RuntimeLayout、LayoutFactory 和编辑创建基础设施放在 Rendering.Wpf 的适配，不表示 Template Model 属于 Rendering。若后续建立平台无关 RuntimeLayout 项目，应再迁移 Builder，不在 E-2-A 预先重构。

## 15. 实施顺序

### P0-7-E-2-A：Runtime Model

1. 在 Application 增加 TemplateId 和 Template 分类枚举；
2. 增加封闭 EquipmentConfiguration；
3. 增加 LayoutRule 和 SecondaryConfiguration；
4. 增加 BayTemplate；
5. 增加 RingCabinetTemplate、集合冻结和能力派生；
6. 增加 Application.Tests 并验证结构校验与不可变性；
7. 构建整个 solution，确认 Domain、Infrastructure、Rendering.Wpf、Desktop 引用未被破坏。

### P0-7-E-2-B：Builder Core

1. 实现 Build Request、Build Result 和类型化失败；
2. 实现 Capability 预检；
3. 映射 BayTemplate 到 RingCabinetIntervalDefinition；
4. 调用 RingCabinetDefinition、RingCabinet.Create；
5. 调用现有 RingCabinetLayoutFactory；
6. 验证 Cabinet/Layout/Interval/Switch Stable ID 覆盖；
7. 测试普通、IntegratedFeeder、Mixed、PT/DTU 拒绝和两间隔 Domain 失败。

### P0-7-E-2-C：Command Integration

1. 扩展 DeviceCommandFactory 的模板创建入口；
2. 复用现有 AddRingCabinetCommand；
3. 验证一次 Build、原子 Execute、Undo/Redo Stable ID；
4. 在 Desktop 接入时登记 SelectionTransition；
5. Redo 不重新 Build。

### P0-7-E-2-D：Built-in Template Library

1. 以 C# 不可变对象定义已确认模板；
2. 模板显式提供 Bays、Index、Function 和 EquipmentConfiguration；
3. 不引入 JSON、厂家推断或自动 Function/Index；
4. 独立验证每个内建模板可由 Builder 成功生成。

## 16. 风险与进入实现条件

主要风险：

- 把 Runtime Model 放入 Domain，会让创建来源污染长期专业事实；
- 把 Runtime Model 放入 Rendering.Wpf，会使模板定义依赖 Windows；
- Builder 复制 RingCabinet.Create 或 RingCabinetLayoutFactory，会产生第二套专业/几何规则；
- CabinetType、Function 与 EquipmentConfiguration 互相推断，会隐藏模板输入；
- TemplateId 参与 Stable ID，会诱导 Redo 重新 Build；
- Runtime Model 宣称支持两间隔，但现有纯 LoadSwitch Domain 仍拒绝两间隔。

进入 E-2-A 实现无需修改 Domain、Persistence 或 FormatVersion。进入 E-2-B 前必须接受以下明确行为：在专业规则未另行确认前，两间隔 LoadSwitch 模板只能被描述，Build 必须失败；Builder 不得自行放宽 Domain。

本计划推荐先以 Application 中的平台无关不可变模型完成 E-2-A，再在现有外层创建基础设施中实现 E-2-B。该路径保持 Template → Builder → Domain Definition 的单向依赖，并控制第一笔生产实现规模。
