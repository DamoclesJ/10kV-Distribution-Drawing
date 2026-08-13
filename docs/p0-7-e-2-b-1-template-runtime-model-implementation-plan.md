# P0-7-E-2-B-1 Template Runtime Model Implementation Plan

> 状态：Template Runtime Model 代码实施计划；本阶段不修改任何 `.cs`、项目文件或解决方案。<br>
> 基线：commit `f32a137dc91732adc407f4d5602eb9ac3e6d6e20`。<br>
> 上游依据：`p0-7-e-1-template-builder-runtime-design.md` 与 `p0-7-e-2-a-template-runtime-model-implementation-plan.md`。

## 1. 项目结构确认

当前 Solution 为：

```text
src/DistributionDrawing.sln
```

现有生产项目包括：

```text
DistributionDrawing.Domain
DistributionDrawing.Application
DistributionDrawing.Rendering.Wpf
DistributionDrawing.Infrastructure
DistributionDrawing.Desktop
```

`DistributionDrawing.Application` 已存在，实际路径为：

```text
src/DistributionDrawing.Application/DistributionDrawing.Application.csproj
```

项目事实：

- TargetFramework 为 `net10.0`；
- 当前只引用 `DistributionDrawing.Domain`；
- 当前项目内没有生产 `.cs` 文件；
- 不依赖 WPF、Infrastructure 或 Desktop。

因此不需要新增 Application Project。Template Runtime Model 第一版应进入现有 Application 项目，而不是 Domain。

建议根 namespace：

```text
DistributionDrawing.Application.Templates.RingCabinets
```

## 2. Layer 依赖设计

### 2.1 允许的依赖

```text
DistributionDrawing.Application
        |
        v
DistributionDrawing.Domain
```

Runtime Model 可以复用现有 Domain 的受控枚举：

- `BayFunction`；
- `GroundingStructureKind`。

这样避免 Template 与 Domain 出现两套同义电气枚举。复用枚举不代表 Template 成为 Domain 事实；生成后真正长期存在的事实仍保存在 RingCabinetInterval 中。

### 2.2 禁止的依赖

禁止形成：

```text
DistributionDrawing.Domain
        |
        v
DistributionDrawing.Application
```

Domain 不引用：

- RingCabinetTemplate；
- BayTemplate；
- EquipmentConfiguration；
- TemplateId；
- TemplateCapability；
- LayoutRule。

Template 是创建来源描述，不是 Domain 聚合的一部分，也不进入 Persistence Version 3。

### 2.3 Builder 所在层

完整 Builder 需要同时使用：

- Application 中的 Template Runtime Model；
- Domain 的 RingCabinetDefinition/RingCabinet.Create；
- Rendering.Wpf 中的 RingCabinetLayout/RingCabinetLayoutFactory。

因此 B-2 Builder Core 不应放在 Domain 或 Application。按当前仓库最小边界，建议放在：

```text
src/DistributionDrawing.Rendering.Wpf/Interaction/Templates/RingCabinets/
```

并让 Rendering.Wpf 在 B-2 阶段增加对 Application 的单向项目引用：

```text
Rendering.Wpf
    |
    +--> Application
    |
    +--> Domain
```

这是外层实现依赖内层创建描述，不形成反向依赖。B-1 只实现 Runtime Model，不新增 Builder 文件，也不修改 Rendering.Wpf.csproj。

## 3. 文件结构设计

### 3.1 B-1 Runtime Model

未来 B-1 实现建议新增：

```text
src/DistributionDrawing.Application/
└── Templates/
    └── RingCabinets/
        ├── TemplateId.cs
        ├── RingCabinetTemplateType.cs
        ├── TemplateCapability.cs
        ├── RingCabinetTemplate.cs
        ├── BayTemplate.cs
        ├── BayEquipmentConfiguration.cs
        ├── LoadSwitchConfiguration.cs
        ├── IntegratedFeederConfiguration.cs
        ├── RingCabinetLayoutRule.cs
        ├── SecondaryConfiguration.cs
        ├── NoSecondaryConfiguration.cs
        └── DtuSecondaryConfiguration.cs
```

所有类型使用：

```text
DistributionDrawing.Application.Templates.RingCabinets
```

第一版不建立通用 `Templates/Models` namespace。RingCabinet 是当前唯一模板目标，先使用设备类型化模型可避免无实际用途的通用抽象。

### 3.2 B-2 Builder 预留

后续 Builder 文件预留为：

```text
src/DistributionDrawing.Rendering.Wpf/
└── Interaction/
    └── Templates/
        └── RingCabinets/
            ├── RingCabinetTemplateBuildRequest.cs
            ├── RingCabinetTemplateBuildResult.cs
            ├── RingCabinetTemplateBuilder.cs
            └── TemplateBuildFailure.cs
```

B-1 Runtime Model 不引用这些 Builder 类型，也不提供 `Build()` 方法。

## 4. RingCabinetTemplate 实现设计

建议使用 `sealed class`，通过构造函数建立不可变状态：

```text
RingCabinetTemplate
├── TemplateId: TemplateId
├── Name: string
├── CabinetType: RingCabinetTemplateType
├── Bays: IReadOnlyList<BayTemplate>
├── LayoutRule: RingCabinetLayoutRule
├── SecondaryConfiguration: SecondaryConfiguration
└── RequiredCapabilities: IReadOnlySet<TemplateCapability>
```

### 4.1 不可变性

要求：

- 所有属性仅 getter；
- 不提供 public setter；
- Bays 在构造时物化并防御性复制；
- RequiredCapabilities 在构造时从模板内容派生并冻结；
- 不返回调用方可修改的数组、List 或 HashSet；
- 配置子类型同样不可变。

### 4.2 构造校验

RingCabinetTemplate 构造时校验：

- TemplateId 有效；
- Name 非 null、非空白，并统一 Trim；
- CabinetType 是已定义枚举值；
- Bays 非 null、非空且不含 null；
- 每个 Bay.Index 大于 0；
- Bay Index 在模板内唯一；
- LayoutRule 非 null；
- SecondaryConfiguration 非 null。

Bay Index 允许不连续，也不要求等于集合位置。

### 4.3 TemplateId

建议实现为只读 value type，保存规范化的非空字符串。它只服务模板目录身份：

- 不是 RingCabinetId；
- 不生成或派生 Stable ID；
- 不保存到工程文件；
- 不传给 RingCabinetDefinition；
- 同一 TemplateId 每次 Build 都产生独立聚合。

### 4.4 CabinetType

建议 Template 专用枚举：

```text
RingCabinetTemplateType
├── Conventional
├── PrimarySecondaryIntegrated
└── Mixed
```

CabinetType 只用于模板分类和策略选择。它不决定 Bays 的 EquipmentConfiguration，不固定间隔数量，也不进入 Domain。

## 5. BayTemplate 实现设计

建议使用不可变 `sealed class` 或 `sealed record`：

```text
BayTemplate
├── Index: int
├── Function: BayFunction
└── EquipmentConfiguration: BayEquipmentConfiguration
```

构造校验：

- Index 必须大于 0；
- Function 必须是已定义 BayFunction；
- 新模板拒绝 `BayFunction.Unknown`；
- EquipmentConfiguration 非 null。

Index 唯一性属于 RingCabinetTemplate 集合级校验。

映射关系冻结为：

| Template | Domain |
| --- | --- |
| `BayTemplate.Index` | `RingCabinetInterval.BayIndex` |
| `BayTemplate.Function` | `RingCabinetInterval.Function` |
| Bays 集合位置 | `RingCabinetInterval.Sequence` |

BayTemplate 禁止保存 Sequence。Builder 按 Bays 顺序建立 Interval Definitions；Sequence 最终由 RingCabinet.Create 生成。

## 6. EquipmentConfiguration 设计

### 6.1 类型结构

建议使用封闭抽象基类：

```text
BayEquipmentConfiguration (abstract)
├── LoadSwitchConfiguration (sealed)
└── IntegratedFeederConfiguration (sealed)
```

第一版不使用接口注册、反射或插件机制。抽象基类可限制合法变体，并支持 Builder 进行明确的类型分派。

禁止：

- `string DeviceType`；
- 任意设备字典；
- 自由设备列表；
- Domain Object 引用；
- Stable ID；
- SwitchState 用户配置。

### 6.2 LoadSwitchConfiguration

LoadSwitchConfiguration 第一版没有额外字段。Builder 后续把它映射为 `RingCabinetIntervalDefinition.CreateLoadSwitch`。

Domain 所需的初始 LoadSwitch/GroundSwitch 状态由 Builder 创建策略提供合法技术值 `Open`，不是模板字段，也不是用户确认的现场运行状态。

### 6.3 IntegratedFeederConfiguration

属性：

```text
GroundingStructureKind GroundingStructureKind
```

构造时拒绝未定义 GroundingStructureKind。Builder 后续原样传入 `CreateIntegratedFeeder`。

该配置不单独保存 GroundingSwitch 布尔值。接地结构和 GroundSwitch 的专业事实由 GroundingStructureKind 与 Domain Factory 共同表达。

### 6.4 Domain 对象生成边界

EquipmentConfiguration 不直接创建：

- IntervalKind；
- Switch；
- Terminal；
- ElectricalNode；
- SwitchAssembly。

B-2 Builder 只把配置映射为 RingCabinetIntervalDefinition；真实对象和内部拓扑继续由 RingCabinet.Create 创建。

## 7. LayoutRule 设计

LayoutRule 属于 Template Runtime Model，因为模板需要选择初始布局策略，但它只保存规则引用，不保存实例几何。

第一版建议：

```text
RingCabinetLayoutRule
└── RuleId: string
```

内建默认 RuleId 示例：

```text
builtin:ring-cabinet/default-v1
```

第一版不把 width/spacing 数值直接写入 Runtime Model。RuleId 由 B-2 Builder/Layout 策略解析为现有确定性 RingCabinetLayoutFactory。后续需要参数化 Cabinet width、Bay width 或 Bay spacing 时，再扩展受控 LayoutRule 和 LayoutFactory 输入。

禁止 LayoutRule 保存：

- Placement Position；
- 某个 Bay/Switch 的 X/Y；
- IntervalId 或 SwitchId；
- RingCabinetLayout 实例；
- Scene 或 WPF 类型。

Template 不直接生成 Layout；B-2 Builder 在 Domain 聚合创建完成后调用 LayoutFactory。

## 8. Capability 设计

### 8.1 RequiredCapabilities

建议 TemplateCapability 第一版包含：

```text
BasicRingCabinet
LoadSwitchBay
IntegratedFeederBay
PTBay
DtuSecondary
DefaultRingCabinetLayout
```

RingCabinetTemplate.RequiredCapabilities 从以下事实派生：

- EquipmentConfiguration 类型；
- BayFunction.PT；
- SecondaryConfiguration；
- LayoutRule。

RequiredCapabilities 不由调用方传入，避免声明能力与模板结构不一致。

### 8.2 第一版支持与拒绝

第一版 Builder 支持：

- BasicRingCabinet；
- LoadSwitchBay；
- IntegratedFeederBay；
- 默认 RingCabinet Layout。

第一版 Builder 拒绝：

- PTBay；
- DtuSecondary；
- 未识别 LayoutRule；
- 未识别 EquipmentConfiguration。

`UnsupportedCapability` 是 B-2 Builder 的返回结果，不属于 B-1 Runtime Model 的行为。B-1 只负责让模板暴露准确 RequiredCapabilities。

PT 当前没有可成功映射的 EquipmentConfiguration。若 BayFunction 为 PT，RequiredCapabilities 必须包含 PTBay，B-2 Builder 在任何 Domain/ID/Layout 创建前失败。不得把 PT 映射为 LoadSwitch 或 IntegratedFeeder。

DTU 通过 DtuSecondaryConfiguration 派生 DtuSecondary，并由 Builder 拒绝。

## 9. Validation 边界

### 9.1 Template Model

负责输入结构：

- 必需字符串和对象非空；
- 枚举值已定义；
- Bays 非空；
- BayIndex 正数且模板内唯一；
- 新模板拒绝 Unknown Function；
- GroundingStructureKind 合法；
- 集合不可变；
- RequiredCapabilities 派生一致。

### 9.2 Builder

负责可生成性：

- SupportedCapabilities 检查；
- PT/DTU 拒绝；
- EquipmentConfiguration 到 Interval Definition 的映射；
- LayoutRule 是否可解析；
- 实例 DisplayName/Placement 输入；
- Cabinet/Layout Stable ID 覆盖一致性；
- 构建失败不返回半结果。

### 9.3 Domain

负责最终专业事实：

- BayIndex/Function 不变量；
- 同柜 BayIndex 唯一；
- Unknown/PT 创建拒绝；
- IntervalKind 与设备结构；
- 纯柜间隔数量；
- SwitchState/GroundingStructureKind；
- Terminal、ElectricalNode、SwitchAssembly 与内部拓扑。

友好前置校验可以与 Domain 有少量防御性重叠，但不能在 Template 或 Builder 复制完整 Domain 规则。尤其不能为两间隔模板绕过当前纯 LoadSwitch 3–6 间隔约束。

## 10. 测试设计

### 10.1 测试项目

当前没有 `DistributionDrawing.Application.Tests`。B-1 实现建议新增：

```text
tests/DistributionDrawing.Application.Tests/
├── DistributionDrawing.Application.Tests.csproj
├── RingCabinetTemplateTests.cs
├── BayTemplateTests.cs
└── EquipmentConfigurationTests.cs
```

测试项目：

- TargetFramework 为 `net10.0`；
- 引用 DistributionDrawing.Application；
- 不引用 Rendering.Wpf 或 Desktop；
- 采用仓库现有 xUnit 版本和测试配置；
- 加入 `src/DistributionDrawing.sln`。

### 10.2 测试覆盖

RingCabinetTemplate：

- 合法模板创建；
- TemplateId/Name 空值拒绝；
- Bays 空集合/null Bay 拒绝；
- BayIndex 重复拒绝；
- 非连续 BayIndex 保持；
- 输入集合修改不影响已创建模板；
- CabinetType 非法枚举拒绝；
- RequiredCapabilities 是不可变派生结果。

BayTemplate：

- Index 正数；
- Function 显式保持；
- Unknown/非法 Function 拒绝；
- 不存在 Sequence 属性；
- EquipmentConfiguration 必须存在。

EquipmentConfiguration：

- LoadSwitchConfiguration 是受控类型；
- IntegratedFeederConfiguration 保持三种 GroundingStructureKind；
- 非法 GroundingStructureKind 拒绝；
- 不接受字符串设备类型或自由设备列表。

Capability：

- LoadSwitch/IntegratedFeeder 能力正确派生；
- PT Function 标记 PTBay；
- DTU 配置标记 DtuSecondary；
- B-1 不测试 Builder 返回 UnsupportedCapability。

真正的 PT/DTU `UnsupportedCapability` 返回测试属于 B-2 Builder Tests。把它放入 B-1 会迫使 Runtime Model 调用 Builder，违反单向依赖。

## 11. 与 Builder Core 的接口预留

Runtime Model 只提供不可变描述：

```text
RingCabinetTemplate
        |
        v
RingCabinetTemplateBuilder.Build(request)
        |
        v
RingCabinetTemplateBuildResult
```

B-1 不实现：

- `Build()`；
- RingCabinetDefinition 映射；
- RingCabinet.Create 调用；
- RingCabinetLayoutFactory 调用；
- BuildResult；
- UnsupportedCapability 返回类型；
- Command 集成。

未来 Build Request 应单独提供实例 DisplayName 和 Placement Position；这些字段不加入 RingCabinetTemplate。

未来 BuildResult 至少包含：

- RingCabinetDefinition；
- RingCabinet；
- RingCabinetLayout；
- RequiredCapabilities 快照。

Builder 返回结果但不修改 Project、Document、RuntimeLayoutDocument、CommandStack 或 Selection。

## 12. 实际修改清单

### 12.1 B-1 生产新增文件

```text
src/DistributionDrawing.Application/Templates/RingCabinets/TemplateId.cs
src/DistributionDrawing.Application/Templates/RingCabinets/RingCabinetTemplateType.cs
src/DistributionDrawing.Application/Templates/RingCabinets/TemplateCapability.cs
src/DistributionDrawing.Application/Templates/RingCabinets/RingCabinetTemplate.cs
src/DistributionDrawing.Application/Templates/RingCabinets/BayTemplate.cs
src/DistributionDrawing.Application/Templates/RingCabinets/BayEquipmentConfiguration.cs
src/DistributionDrawing.Application/Templates/RingCabinets/LoadSwitchConfiguration.cs
src/DistributionDrawing.Application/Templates/RingCabinets/IntegratedFeederConfiguration.cs
src/DistributionDrawing.Application/Templates/RingCabinets/RingCabinetLayoutRule.cs
src/DistributionDrawing.Application/Templates/RingCabinets/SecondaryConfiguration.cs
src/DistributionDrawing.Application/Templates/RingCabinets/NoSecondaryConfiguration.cs
src/DistributionDrawing.Application/Templates/RingCabinets/DtuSecondaryConfiguration.cs
```

### 12.2 B-1 测试新增文件

```text
tests/DistributionDrawing.Application.Tests/DistributionDrawing.Application.Tests.csproj
tests/DistributionDrawing.Application.Tests/RingCabinetTemplateTests.cs
tests/DistributionDrawing.Application.Tests/BayTemplateTests.cs
tests/DistributionDrawing.Application.Tests/EquipmentConfigurationTests.cs
```

### 12.3 B-1 修改文件

```text
src/DistributionDrawing.sln
```

只用于登记 Application.Tests。现有 Application.csproj 已引用 Domain，Runtime Model 无需增加项目引用，因此原则上不修改 Application.csproj。

B-1 不修改 Domain、Infrastructure、Rendering.Wpf、Desktop、Persistence、FormatVersion、Command 或 Selection。

### 12.4 B-2 预期修改

B-2 才新增 Builder/BuildResult 文件，并修改 Rendering.Wpf.csproj 以引用 Application。B-1 不提前进行这些改动。

## 13. 实施顺序

### P0-7-E-2-B-1：Runtime Model

1. 新增 TemplateId、TemplateType 和 Capability；
2. 新增封闭 EquipmentConfiguration；
3. 新增 LayoutRule 和 SecondaryConfiguration；
4. 新增 BayTemplate；
5. 新增 RingCabinetTemplate、集合冻结、Index 唯一校验和能力派生；
6. 新增 Application.Tests 并加入 solution；
7. 执行 build/test、`git diff --check` 和范围检查。

### P0-7-E-2-B-2：Builder Core

1. 增加 Rendering.Wpf → Application 项目引用；
2. 实现 BuildRequest、BuildResult 和 Failure；
3. 实现 Capability 预检；
4. 映射 RingCabinetDefinition；
5. 调用 RingCabinet.Create 和 RingCabinetLayoutFactory；
6. 测试普通、IntegratedFeeder、Mixed、PT/DTU 拒绝、两间隔 Domain 失败和 Stable ID。

### P0-7-E-2-B-3：Command Integration

1. 通过 DeviceCommandFactory 接收固定 BuildResult；
2. 复用 AddRingCabinetCommand；
3. CommandStack 原子 Execute；
4. Undo/Redo 恢复相同对象，不重新 Build；
5. Desktop 后续接入 SelectionTransition。

## 14. 风险与实施门槛

主要风险：

- Runtime Model 误入 Domain，导致生成来源成为长期专业事实；
- Runtime Model 依赖 Rendering.Wpf，导致平台无关模型被 Windows 绑定；
- RequiredCapabilities 由调用方传入，造成能力声明与模板结构漂移；
- BayTemplate 保存 Sequence，与 Bays 顺序形成两个事实源；
- EquipmentConfiguration 使用自由字符串或列表，迫使 Builder 复制 Domain 规则；
- B-1 提前实现 UnsupportedCapability 返回，从而把 Builder 职责塞入 Model；
- 为让两间隔模板成功而绕过现有 Domain 数量规则。

B-1 可以在不修改 Domain、Persistence、Rendering、Desktop、Command 和 Selection 的前提下直接实施。B-1 完成条件是平台无关模型及其测试可独立编译，且没有 Builder、Layout、Project 或 WPF 依赖。
