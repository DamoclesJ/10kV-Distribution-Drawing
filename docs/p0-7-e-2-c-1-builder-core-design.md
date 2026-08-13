# P0-7-E-2-C-1 Builder Core Design

> 状态：Template Builder Core 生产实现前设计；本阶段不修改任何生产代码、项目文件或测试。<br>
> 基线：commit `698d9ad1cbe37edcfd40265607f04e9fb658ebf0`。<br>
> 上游依据：P0-7-E-1 Builder Runtime Design、P0-7-E-2-A/B-1 计划以及已实现的 Application Template Runtime Model。

## 1. Builder 所属 Layer

### 1.1 当前项目事实

当前依赖与类型归属为：

```text
DistributionDrawing.Application (net10.0)
├── Template Runtime Model
└── references Domain

DistributionDrawing.Rendering.Wpf (net10.0-windows)
├── RingCabinetLayout
├── RingCabinetLayoutFactory
├── DocumentPoint
├── existing creation factories
└── references Domain
```

Application 已正确拥有平台无关 Template Runtime Model。Domain 不引用 Application。

### 1.2 方案比较

#### 方案 A：完整 Builder 放入 Application

优点是模板用例逻辑集中在 Application。

但当前 BuildResult 必须包含 RingCabinetLayout，而 RingCabinetLayout、DocumentPoint 和 RingCabinetLayoutFactory 都属于 `DistributionDrawing.Rendering.Wpf`。若完整 Builder 放入 Application，会迫使：

```text
Application → Rendering.Wpf
```

这会让 `net10.0` Application 依赖 `net10.0-windows` WPF 外层，破坏平台无关边界，因此本阶段不采用。

#### 方案 B：完整 Builder 放入 Rendering.Wpf 外层创建边界

Builder 同时引用：

- Application 的 Template Runtime Model；
- Domain 的 RingCabinetDefinition/RingCabinet.Create；
- Rendering.Wpf 自己的 RingCabinetLayoutFactory。

依赖方向为：

```text
Rendering.Wpf
    ├── Application
    └── Domain

Application
    └── Domain
```

Domain 不反向引用 Application 或 Builder。该方案与现有 `RingCabinetCreationFactory`、`DeviceCommandFactory` 所在创建基础设施一致，改动最小，但会把本可平台无关的 Template → Domain 映射也放入 WPF 项目。

### 1.3 推荐结论

第一版采用职责拆分，而不是把全部 Builder 逻辑放在单一项目：

```text
Application: Template → Domain Builder
Rendering.Wpf: Domain → RuntimeLayout Builder
Rendering.Wpf: thin workflow coordinator
```

外层协调入口放置于：

```text
src/DistributionDrawing.Rendering.Wpf/Interaction/Templates/RingCabinets/
```

namespace：

```text
DistributionDrawing.Rendering.Wpf.Interaction.Templates.RingCabinets
```

并在 C-2 实现时新增：

```text
DistributionDrawing.Rendering.Wpf → DistributionDrawing.Application
```

这只是对当前 RuntimeLayout 实际归属的适配，不表示 Template → Domain Builder 属于 Rendering。

### 1.4 Builder Layer Responsibility Split

#### Template → Domain Builder

归属：`DistributionDrawing.Application`。

职责：

- 校验 Template 和实例 DisplayName；
- 检查与 Domain 创建有关的 RequiredCapabilities；
- 拒绝 PT、DTU 和未知 EquipmentConfiguration；
- 按 Bays 顺序生成 RingCabinetIntervalDefinition；
- 生成 RingCabinetDefinition；
- 调用 RingCabinet.Create；
- 返回固定 Definition、RingCabinet 和已检查 Capability。

它不引用 DocumentPoint、RingCabinetLayout、RingCabinetLayoutFactory 或 WPF 类型。

建议命名：

```text
RingCabinetTemplateDomainBuilder
```

#### Domain → RuntimeLayout Builder

归属：当前暂由 `DistributionDrawing.Rendering.Wpf` 承担。

职责：

- 解析 RingCabinetLayoutRule；
- 在 Domain 创建前提供 LayoutRule 支持预检；
- 接收已创建的 RingCabinet 和文档 Position；
- 调用 RingCabinetLayoutFactory；
- 返回与实际 IntervalId/SwitchId 匹配的 RingCabinetLayout。

建议命名：

```text
RingCabinetTemplateLayoutBuilder
```

Rendering.Wpf 中可保留一个薄 `RingCabinetTemplateBuilder` 协调两段流程，但它不得重新实现 Template 映射或 Layout 几何。

未来若 RuntimeLayout、DocumentPoint 和 LayoutFactory 被抽离为平台无关项目，则 Domain → RuntimeLayout Builder 与外层协调器可以迁出 Rendering.Wpf。Template → Domain Builder 无需变化，也不需要 Domain 反向引用 Application。

## 2. Builder 输入

### 2.1 Build Request

仅传入 RingCabinetTemplate 不足以创建生产聚合与布局，因为 Domain 需要实例 DisplayName，LayoutFactory 需要文档位置。建议外层协调请求为：

```text
RingCabinetTemplateBuildRequest
├── Template: RingCabinetTemplate
├── DisplayName: string
└── Position: DocumentPoint
```

字段职责：

- Template 提供 Bays、BayIndex、BayFunction、EquipmentConfiguration、LayoutRule 与 RequiredCapabilities；
- DisplayName 是本次 RingCabinet 实例名称，不从 Template.Name 自动推断；
- Position 是本次初始 RingCabinetLayout 的文档毫米坐标，不写回 Template。

协调器按职责拆成两个内部输入：

```text
TemplateDomainBuildRequest
├── Template
└── DisplayName

TemplateLayoutBuildRequest
├── Cabinet
├── LayoutRule
└── Position
```

Application 的 Template → Domain Builder 永远不接收 Position。Rendering.Wpf 的 Domain → RuntimeLayout Builder 不接收 BayTemplate 映射参数。

### 2.2 明确不接收

Builder 不接收：

- ProjectRuntimeSession；
- DrawingDocument 或 RuntimeLayoutDocument；
- DrawingScene、SceneBuilder 或 Symbol；
- CommandStack、ICommand 或 SelectionTransition；
- SelectionManager、SelectionReference 或 Inspector；
- MainWindow、Dialog、ViewModel 或 WPF Control；
- Persistence DTO 或 FormatVersion；
- 当前工程 Undo/Redo 历史。

Builder 是无工程副作用的候选对象创建服务。它不检查当前工程是否已有相同 ID；工程注册冲突仍由 Add Command 在 Execute 时校验。

## 3. Builder 输出

### 3.1 成功结果

建议不可变成功结果：

```text
RingCabinetTemplateBuildResult
├── Definition: RingCabinetDefinition
├── Cabinet: RingCabinet
├── Layout: RingCabinetLayout
└── RequiredCapabilities: IReadOnlySet<TemplateCapability>
```

最小必需项是 Cabinet 与 Layout。Definition 和 RequiredCapabilities 建议保留：

- Definition 是模板映射后的完整 Domain 创建输入，便于测试与诊断；
- RequiredCapabilities 是构建时使用的只读快照，便于诊断能力决策；
- Cabinet 是完整、已验证的聚合；
- Layout 与实际 Cabinet/Interval/Switch Stable ID 对齐。

### 3.2 不增加 GeneratedMetadata

第一版不增加无约束 `GeneratedMetadata` 字典或对象袋。它容易重复保存：

- BayIndex/Function；
- Stable ID；
- TemplateId；
- Layout 坐标。

这些信息已经分别存在于 Template、Definition、Domain 和 Layout 中。若后续有明确诊断需求，应增加类型化字段，而不是自由 Metadata。

### 3.3 不修改 Project

BuildResult 是未提交候选结果。Builder 不调用：

- DrawingDocument.AddDevice；
- RuntimeLayoutDocument.AddRingCabinet；
- CommandStack.ExecuteCommand；
- Scene rebuild；
- Selection。

Build 失败时，任何临时对象都未进入工程，不产生 Dirty 或 Command History。

## 4. Template 到 Domain 映射

完整流程：

```text
RingCabinetTemplate
        |
        v
TemplateCapability preflight
        |
        v
BayTemplate[] in collection order
        |
        v
RingCabinetIntervalDefinition[]
        |
        v
RingCabinetDefinition
        |
        v
RingCabinet.Create
        |
        v
RingCabinet aggregate
```

### 4.1 Sequence

BayTemplate 不保存 Sequence。Builder 必须按 `Template.Bays` 的现有集合顺序创建 Interval Definitions，不排序、不重排。

现有 RingCabinet.Create 按 Definition 顺序生成：

```text
Sequence = position in Bays + 1
```

Sequence 由 Domain 创建边界生成，Builder 不显式写入，也不根据 BayIndex 排序。

### 4.2 BayIndex 与 BayFunction

Builder 原样传递：

```text
BayTemplate.Index    → RingCabinetIntervalDefinition.BayIndex
BayTemplate.Function → RingCabinetIntervalDefinition.Function
```

禁止：

- `BayIndex = Sequence` 自动补值；
- 根据 DisplayName 解析 BayIndex；
- 根据 EquipmentConfiguration 猜 Function；
- 把 Unknown 当作 Outgoing 或 Reserve；
- 自动补齐 BayIndex 缺号。

### 4.3 DisplayName

RingCabinet DisplayName 来自 BuildRequest.DisplayName。

BayTemplate 当前没有 Bay DisplayName 字段，而 Domain Interval DisplayName 是可选值。第一版 Builder应传入 `null`，不自动生成“负 N 间隔”等专业名称。BayIndex 仍作为独立 Domain 事实存在，Inspector 可按后续明确规则投影。

## 5. EquipmentConfiguration 映射

### 5.1 LoadSwitchConfiguration

映射为：

```text
RingCabinetIntervalDefinition.CreateLoadSwitch(
    bay.Index,
    bay.Function,
    SwitchState.Open,
    SwitchState.Open,
    displayName: null)
```

`Open` 只是现有 Domain 创建所需的技术初始化值，不是用户确认的现场运行状态。Builder 不把 SwitchState 加入 Template Runtime Model。

### 5.2 IntegratedFeederConfiguration

映射为：

```text
RingCabinetIntervalDefinition.CreateIntegratedFeeder(
    bay.Index,
    bay.Function,
    configuration.GroundingStructureKind,
    SwitchState.Open,
    SwitchState.Open,
    SwitchState.Open,
    displayName: null)
```

GroundingStructureKind 从强类型配置原样传递，不根据 CabinetType、Function 或位置推断。

### 5.3 Domain 不知道 EquipmentConfiguration

EquipmentConfiguration 只存在于 Application Template Runtime Model。Builder 把它转换为现有 RingCabinetIntervalDefinition；生成后：

- IntervalKind；
- GroundingStructureKind；
- Switch；
- Terminal；
- ElectricalNode；
- SwitchAssembly

构成真实 Domain 事实。Domain 不引用 EquipmentConfiguration，也不持久化 Template 配置副本。

## 6. Capability 检查

### 6.1 检查顺序

Capability 检查按职责拆分，并在生成 CabinetId、Definition、Domain 聚合和 Layout 前执行：

1. 外层协调器先让 Domain → RuntimeLayout Builder 预检 LayoutRule.RuleId；
2. Template → Domain Builder 读取 Template.RequiredCapabilities；
3. 检查 BasicRingCabinet、LoadSwitchBay、IntegratedFeederBay、PTBay 和 DtuSecondary；
4. 若有缺失能力，返回 UnsupportedCapability；
5. 只有 Layout 与 Domain 能力都受支持后才生成 CabinetId 并进入 EquipmentConfiguration 映射。

第一版 SupportedCapabilities：

- BasicRingCabinet；
- LoadSwitchBay；
- IntegratedFeederBay；
- RingCabinetLayout，且仅支持默认 RuleId。

### 6.2 PT

含 `BayFunction.PT` 的模板会派生 `TemplateCapability.PTBay`。Builder 必须在映射 LoadSwitch/IntegratedFeeder 配置之前返回：

```text
UnsupportedCapability(PTBay)
```

不得创建伪 PT，不得把 PT 映射为普通间隔，也不得调用 RingCabinet.Create。

### 6.3 DTU

含 DtuSecondaryConfiguration 的模板会派生 `TemplateCapability.DtuSecondary`。Builder 返回：

```text
UnsupportedCapability(DtuSecondary)
```

DTU 不进入一次 Domain，不创建 Terminal、Node、Connection 或纯图形占位。

### 6.4 未知 LayoutRule

当前 Runtime Model 的 LayoutRule 以 RuleId 表达。第一版只支持：

```text
RingCabinetLayoutRule.DefaultRuleId
```

其他 RuleId 返回明确 `UnsupportedLayoutRule`，不能静默回退默认布局。

## 7. Domain 创建边界

Application 中的 Template → Domain Builder 只创建 Domain Definition，然后调用：

```text
RingCabinet.Create(
    RingCabinetDefinition.Create(
        cabinetId,
        displayName,
        intervalDefinitions))
```

Builder 禁止直接：

- `new RingCabinetInterval`；
- `new SwitchDevice`；
- `new Terminal`；
- `new ElectricalNode`；
- 创建 SwitchAssembly；
- 拼装内部拓扑；
- 修改 RingCabinet 私有集合。

RingCabinet.Create 继续负责：

- Sequence；
- Interval/Switch/Terminal/Node/Assembly Stable ID；
- Internal topology；
- IntervalKind 与 GroundingStructureKind；
- 纯柜数量等 Domain 不变量。

Builder 可以做友好前置检查，但不能复制或替代完整 Domain 校验。当前纯 LoadSwitch 两间隔仍由 Domain 拒绝；Builder 不绕过该规则。

## 8. Layout 生成边界

Domain 聚合成功后，Rendering.Wpf 中的 Domain → RuntimeLayout Builder 调用现有：

```text
RingCabinetLayoutFactory.Create(cabinet, request.Position)
```

LayoutFactory 根据真实：

- Interval 顺序；
- IntervalKind；
- GroundingStructureKind；
- Switch 集合；
- IntervalId/SwitchId

创建完整 RingCabinetLayout。

Builder 不生成：

- Symbol；
- DrawingScene；
- SceneElement；
- HitTest；
- AttachmentLayout；
- WPF Shape。

RingCabinet 是独立设备，不能错误生成 AttachmentLayout。

第一版默认 LayoutRule 直接路由到现有 RingCabinetLayoutFactory。Builder 不复制其 width、spacing 或 Switch 坐标公式。

## 9. Stable ID 策略

### 9.1 首次 Build

在 Capability 和请求校验全部通过后：

1. Builder 生成一次 CabinetId，例如 `Guid.NewGuid()`；
2. 创建 RingCabinetDefinition；
3. RingCabinet.Create 生成内部 IntervalId、SwitchId、TerminalId、ElectricalNodeId 和 SwitchAssemblyId；
4. LayoutFactory 读取这些 ID 创建匹配布局；
5. BuildResult 固定保存 Definition、Cabinet 和 Layout。

TemplateId 不参与任何 Stable ID，不作为 Guid seed，也不写入工程对象。

### 9.2 Builder 是否保存 BuildResult

Builder 本身不保存会话级 BuildResult，也不维护缓存。它返回一次不可变结果。未来 AddRingCabinetCommand 持有该结果中的 Cabinet 和 Layout，从而负责 Undo/Redo 生命周期。

Build 失败但未发布的 ID 可以丢弃；Stable ID 不要求连续或回收。

## 10. Undo/Redo 预留

未来接入链路：

```text
TemplateBuilder.Build(request)
        |
        v
BuildResult(Cabinet + Layout)
        |
        v
AddRingCabinetCommand
        |
        v
CommandStack.ExecuteCommand
```

AddRingCabinetCommand 保存首次生成的同一 Cabinet 与 Layout：

- Execute：加入完整聚合和布局；
- Undo：移除同一聚合和布局；
- Redo：恢复同一对象及 Stable ID。

禁止在 Redo 时重新调用 Builder。Builder 不依赖 Command，也不记录 SelectionTransition。Selection 恢复属于后续 Desktop/Command Integration。

## 11. 错误模型

### 11.1 结果模型

建议区分成功结果与预期失败：

```text
RingCabinetTemplateBuildOutcome
├── Success: RingCabinetTemplateBuildResult
└── Failure: TemplateBuildFailure
```

第一版失败类型至少包括：

```text
TemplateBuildFailure
├── InvalidTemplate
├── UnsupportedCapability
├── UnsupportedLayoutRule
└── DomainCreationFailure
```

### 11.2 各错误职责

| 错误 | 触发条件 |
| --- | --- |
| InvalidTemplate | BuildRequest 缺少模板、DisplayName 无效、Position 非有限值等调用期输入问题 |
| UnsupportedCapability | PT、DTU 或未来未知能力不受当前 Builder 支持 |
| UnsupportedLayoutRule | RuleId 没有对应受控 Layout 策略 |
| DomainCreationFailure | 完整映射后被 RingCabinetDefinition/RingCabinet.Create 的最终不变量拒绝 |

错误必须包含稳定的错误类别和简明消息；UnsupportedCapability 还应包含 MissingCapabilities。

不得：

- 用默认值隐藏缺失 Capability；
- 把未知 RuleId 当默认 Rule；
- 把 Unknown Function 改成 Outgoing；
- 返回可提交的半聚合或半布局。

编程错误、不可达配置类型和意外异常不应全部吞并为 DomainCreationFailure。只有预期的 Domain validation 异常才转换；未知异常继续暴露，避免隐藏缺陷。

## 12. 第一版支持范围

第一版支持：

- LoadSwitchConfiguration；
- IntegratedFeederConfiguration；
- 三种现有 GroundingStructureKind；
- 普通、一二次融合及 Domain 已允许的 mixed RingCabinet；
- 非连续但唯一 BayIndex；
- Bays 顺序到 Domain Sequence；
- 默认 RingCabinetLayoutRule；
- 完整 RingCabinet + RingCabinetLayout BuildResult。

第一版不支持：

- PT 成功生成；
- DTU 自动生成；
- JSON Template；
- 厂家模板或插件注册；
- 自动电气计算；
- 自动 Function、BayIndex 或专业名称推断；
- 参数化 LayoutRule；
- TemplateReference 持久化；
- Command、Undo/Redo 或 Selection 的执行接入。

## 13. 测试设计

### 13.1 Builder 测试位置

Template → Domain Builder 测试应扩展现有平台无关项目：

```text
tests/DistributionDrawing.Application.Tests/
└── RingCabinetTemplateDomainBuilderTests.cs
```

它覆盖 Capability、Definition 映射、RingCabinet.Create、BayIndex、Sequence 和 Domain failure，不引用 Rendering.Wpf。

Domain → RuntimeLayout Builder 与薄协调器位于 Rendering.Wpf，且当前仓库没有 Rendering.Wpf.Tests。C-2 实现建议新增：

```text
tests/DistributionDrawing.Rendering.Wpf.Tests/
├── DistributionDrawing.Rendering.Wpf.Tests.csproj
└── RingCabinetTemplateBuilderTests.cs
```

该项目使用 `net10.0-windows`、EnableWindowsTargeting，并引用：

- DistributionDrawing.Application；
- DistributionDrawing.Rendering.Wpf。

测试本身不创建 Window、Dispatcher 或 WPF Control，只测试 Layout Builder、协调器、Domain 聚合和 RuntimeLayout 数据。若当前非 Windows 环境不能执行该目标框架，至少要求 Windows CI/开发机执行；不能将未运行测试描述为通过。

### 13.2 必须覆盖

1. LoadSwitch 模板成功生成：
   - Cabinet/Definition/Layout ID 对齐；
   - 每个 Bay 生成真实 LoadSwitchInterval；
   - 不产生外部 Connection 或伪对象。
2. IntegratedFeeder 模板成功生成：
   - GroundingStructureKind 原样保持；
   - 三种 GroundingStructureKind 均覆盖；
   - Switch Layout 覆盖实际 SwitchId。
3. BayIndex：
   - 正数、唯一、非连续值原样进入 Domain。
4. Sequence：
   - 按 Bays 集合顺序生成 1..N；
   - 不按 BayIndex 排序。
5. PT：
   - 返回 UnsupportedCapability(PTBay)；
   - 不调用 Domain/Create/Layout。
6. DTU：
   - 返回 UnsupportedCapability(DtuSecondary)；
   - 不生成一次拓扑或布局占位。
7. LayoutRule：
   - 默认 Rule 成功；
   - 未知 RuleId 明确失败，不回退。
8. Domain failure：
   - 纯 LoadSwitch 两间隔由 Domain 拒绝；
   - 不返回半结果。
9. Stable ID：
   - BuildResult 内 Cabinet/Interval/Switch/Layout 引用一致；
   - 同一 BuildResult 多次读取 ID 不变；
   - TemplateId 不等于或派生任何 Domain ID。

Undo/Redo Stable ID 测试属于 C-4 Command Integration：Builder 测试只验证固定 BuildResult，不模拟 Redo。

## 14. 文件规划

### 14.1 C-2 新增生产文件

```text
src/DistributionDrawing.Application/Templates/RingCabinets/Building/
├── TemplateDomainBuildRequest.cs
├── TemplateDomainBuildResult.cs
├── TemplateDomainBuildFailure.cs
└── RingCabinetTemplateDomainBuilder.cs

src/DistributionDrawing.Rendering.Wpf/Interaction/Templates/RingCabinets/
├── RingCabinetTemplateBuildRequest.cs
├── RingCabinetTemplateBuildResult.cs
├── RingCabinetTemplateBuildOutcome.cs
├── TemplateBuildFailure.cs
├── RingCabinetTemplateLayoutBuilder.cs
└── RingCabinetTemplateBuilder.cs
```

职责：

| 文件 | 职责 |
| --- | --- |
| BuildRequest | Template、实例 DisplayName、Position |
| BuildResult | 固定 Definition、Cabinet、Layout、RequiredCapabilities |
| BuildOutcome | 明确成功/失败分支 |
| TemplateBuildFailure | 类型化错误类别、消息和缺失能力 |
| TemplateDomainBuilder | Application 中的 Template 校验、Capability、Definition 映射和 Domain/Create |
| TemplateLayoutBuilder | Rendering.Wpf 中的 LayoutRule 解析和 RingCabinetLayoutFactory 协调 |
| RingCabinetTemplateBuilder | 薄协调器；组合 Domain 与 Layout 两段结果 |

### 14.2 C-2 修改文件

```text
src/DistributionDrawing.Rendering.Wpf/DistributionDrawing.Rendering.Wpf.csproj
```

新增对 Application 的 ProjectReference。除此之外不修改 Domain、Persistence、Desktop、Command 或 Selection。

### 14.3 C-2 测试文件

```text
tests/DistributionDrawing.Rendering.Wpf.Tests/
├── DistributionDrawing.Rendering.Wpf.Tests.csproj
└── RingCabinetTemplateBuilderTests.cs
```

若新增测试项目，应同步登记 `src/DistributionDrawing.sln`。C-1 设计阶段不修改 solution。

### 14.4 C-3/C-4 预留

C-3 可在 Builder 评审后冻结 BuildResult/Failure API；若 C-2 已按本设计一次实现，则 C-3 只做接口审查和必要的最小调整。

C-4 才修改 DeviceCommandFactory 或 Desktop 创建协调逻辑，复用 AddRingCabinetCommand。Builder Core 不修改现有 Command。

## 15. 实施顺序

### P0-7-E-2-C-1：Design

冻结 Builder Layer、输入输出、映射、Capability、错误和测试边界。

### P0-7-E-2-C-2：Builder Runtime Implementation

1. 增加 Rendering.Wpf → Application 引用；
2. 在 Application 实现 TemplateDomainBuildRequest、Result 和错误模型；
3. 在 Application 实现 Domain Capability 预检；
4. 映射 BayTemplate 到 Interval Definitions 并调用 RingCabinetDefinition/RingCabinet.Create；
5. 在 Rendering.Wpf 实现 LayoutRule 预检和 RingCabinetTemplateLayoutBuilder；
6. 实现薄 RingCabinetTemplateBuilder 协调器；
7. 返回固定 Definition、Cabinet、Layout 和 Capability 结果；
8. 分别增加 Application Domain Builder 测试与 RuntimeLayout Builder 测试并执行 build/test。

### P0-7-E-2-C-3：BuildResult Contract Review

确认：

- BuildResult 无 Project/Command/Selection 依赖；
- Definition/Cabinet/Layout/Capabilities 不构成可漂移副本；
- Failure 不隐藏专业校验或异常；
- Stable ID 覆盖完整。

### P0-7-E-2-C-4：Command Integration

1. DeviceCommandFactory 接收 Template BuildRequest 或固定 BuildResult；
2. Build 只发生一次；
3. 复用 AddRingCabinetCommand；
4. Execute/Undo/Redo 保持同一聚合、布局和 Stable ID；
5. Desktop 后续登记 SelectionTransition；
6. Redo 禁止重新 Build。

## 16. 最终设计结论

P0-7-E-2-C Builder Core 采用以下边界：

- Template Runtime Model 继续属于 Application；
- Template → Domain Builder 属于 Application，负责 Template 校验、Capability、Definition 和 RingCabinet.Create；
- Domain → RuntimeLayout Builder 因当前 RingCabinetLayoutFactory 的归属，暂放 Rendering.Wpf；
- Rendering.Wpf 只保留组合两段结果的薄协调器，不重复 Domain 映射或 Layout 几何；
- Builder 输入为 Template + 实例 DisplayName + Position，不接收 Session/Project/UI；
- Builder 原样传递 BayIndex/BayFunction，Bays 顺序由 Domain 生成 Sequence；
- Builder 只建立 Definition，不直接 new 内部 Domain Entity；
- Capability 检查先于 Stable ID、Domain 和 Layout 创建；
- PT/DTU 明确返回 UnsupportedCapability；
- Builder 返回固定 Cabinet + Layout，不修改工程；
- Builder 不缓存结果，未来 Command 保存首次 BuildResult；
- Redo 不重新执行 Builder；
- 第一版不自动命名、不推断 Function、不支持未知 LayoutRule 回退。

按当前代码结构，C-2 可以在不修改 Domain、Persistence、Desktop、Command 和 Selection 的前提下实现。唯一新增项目依赖是 Rendering.Wpf 单向引用 Application。未来 RuntimeLayout 独立后，Layout Builder 与薄协调器可以迁出 Rendering.Wpf，Application 中的 Domain Builder 保持不变。
