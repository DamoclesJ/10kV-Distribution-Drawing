# P0-7-F-1 Template Library Design

> 状态：Template Library 生产实现前设计；本阶段不修改生产代码、测试、项目文件或工程格式。
> 稳定基线：commit `292e28b`，tag `v0.7-e`（`stable: template runtime and command integration`）。
> 事实源：当前已提交代码优先于 P0-7-E 历史计划；未冻结的专业规则不得由 Library 或实现自行推断。

## 1. Context

P0-7-E 已形成完整的基础 Template Runtime 创建链路：

```text
RingCabinetTemplate
        |
        v
Application Template -> Domain Builder
        |
        v
RingCabinetDefinition / RingCabinet
        |
        v
Rendering.Wpf Domain -> RuntimeLayout Builder
        |
        v
Thin Build Coordinator / Full BuildResult
        |
        v
AddRingCabinetCommand / CommandStack
        |
        v
Project Runtime / Selection / Inspector / Undo / Redo / Dirty
```

当前缺少的不是创建能力，而是模板来源：调用方仍需临时构造 `RingCabinetTemplate`。P0-7-F 首先建立一个稳定、集中、可枚举和可按 `TemplateId` 查找的内置模板目录，随后再接最小模板选择 UI。

当前真实 Runtime Model 已包含：

- `TemplateId`：trim 后的非空字符串 record；
- `RingCabinetTemplate`：`TemplateId`、`Name`、`CabinetType`、有序 `Bays`、`LayoutRule`、`SecondaryConfiguration` 和派生的 `RequiredCapabilities`；
- `BayTemplate`：`Index`、Domain `BayFunction`、强类型 `BayEquipmentConfiguration`；
- `LoadSwitchConfiguration` 与带 `GroundingStructureKind` 的 `IntegratedFeederConfiguration`；
- `NoSecondaryConfiguration` 与 `DtuSecondaryConfiguration`；
- `RingCabinetLayoutRule.Default`；
- `TemplateCapability`。

`RingCabinetTemplate` 已对 Bays 做防御性复制，RequiredCapabilities 使用 frozen set；`BayTemplate` 已拒绝 `Unknown` 和未定义 Function。Domain Builder 当前支持 LoadSwitch、IntegratedFeeder 和 mixed，明确拒绝 PT、DTU，并让 Domain 拒绝纯 LoadSwitch 两间隔。

## 2. Goals

第一版 Template Library 目标是：

1. 集中构造和保存经确认的内置 RingCabinet templates；
2. 使用稳定 `TemplateId` 精确查找模板；
3. 以稳定顺序枚举当前可用模板；
4. 保持 Library、集合和模板对象不可变；
5. 成为后续 Desktop Template Selection UI 的唯一内置模板来源；
6. 让 Builder 和 Coordinator 继续只消费 `RingCabinetTemplate`；
7. 不依赖 WPF、Desktop、Infrastructure、Persistence 或 Project Runtime；
8. 为未来多 Template Source 演进保留清晰边界，但不提前实现抽象体系。

第一版 Library 的目录语义冻结为：

> 枚举并返回当前版本能够通过现有 Domain Builder 和 RuntimeLayout Builder 成功创建的、专业内容已经确认的内置模板。

它不是“Runtime Model 可以表达的所有模板”目录。

## 3. Non-Goals

本阶段和第一版 Library 不负责：

- Template Build、Domain Create 或 Stable ID 生成；
- RuntimeLayout、DrawingScene、Rendering 或 HitTest；
- Project mutation、Command、Selection、Inspector、Undo/Redo 或 Dirty；
- Template Picker、ViewModel、Window 或 UI selection state；
- Project Persistence、DTO、FormatVersion 或 Migration；
- JSON parsing、文件 I/O、Template schema version 或热加载；
- 用户模板编辑、厂家模板、远程模板或插件发现；
- Capability 支持判断或 Builder failure 预测；
- 根据数量、名称、设备类型或顺序推断 BayIndex、Function 或 GroundingStructureKind；
- PT/DTU/两间隔的伪支持；
- 将 `TemplateId` 变成 Domain ID 或工程恢复依赖。

## 4. Current Architecture

当前依赖方向为：

```text
Desktop
   |
   +--> Rendering.Wpf Build Coordinator
   |
   +--> Application Template Runtime Model / Domain Builder
              |
              v
            Domain

Rendering.Wpf
   |
   +--> Application
   +--> Domain
```

新增 Library 后的读取链路为：

```text
Desktop composition / future template UI
        |
        v
Application RingCabinetTemplateLibrary
        |
        v
RingCabinetTemplate
        |
        v
existing Build Request / Coordinator
```

Library 只解决 `TemplateId -> RingCabinetTemplate` 和稳定枚举。现有 Runtime Model、两段 Builder、Coordinator、Command 和 Desktop creation controller 的输入输出均不需要改变。

## 5. Library Layer

第一版 Built-in Template Library 应属于 `DistributionDrawing.Application`：

```text
src/DistributionDrawing.Application/Templates/RingCabinets/Library/
```

推荐 namespace：

```text
DistributionDrawing.Application.Templates.RingCabinets.Library
```

理由：

- Library 管理的是 Application 已拥有的不可变 Template Runtime Model；
- 它只需要现有 Application -> Domain 依赖，以使用模板内的 Domain 枚举；
- 它不需要 WPF、布局、工程会话、文件系统或 DTO；
- Desktop 可向内依赖 Application；
- Rendering.Wpf Builder 无需知道模板来自内置目录、未来 JSON 或其他来源。

不采用 Domain：模板是创建来源，不是生成后长期 Domain 事实。
不采用 Infrastructure：第一版没有外部存储或 I/O。
不采用 Rendering.Wpf：目录和模板定义不应受 Windows/WPF 绑定。
不采用 Desktop：否则内置模板会与 UI 生命周期和展示状态耦合。

## 6. Library Responsibility

`RingCabinetTemplateLibrary` 第一版只负责：

- 在构造时物化模板集合；
- 拒绝 null 模板和重复 `TemplateId`；
- 保留显式注册顺序；
- 暴露只读模板列表；
- 按 `TemplateId` 精确查找并返回同一个不可变模板对象。

Library 不重新校验或复制 `RingCabinetTemplate` 已拥有的模型不变量。Template 构造器继续负责 Name、Bays、Index、Function、配置和 Capability 派生；Domain Builder 与 Domain 继续负责可生成性和电气不变量。

Library 也不缓存 BuildResult，不维护“最近选择”，不保存实例 DisplayName 或 Position。

## 7. Library API

推荐第一版最小 API：

```csharp
public sealed class RingCabinetTemplateLibrary
{
    public RingCabinetTemplateLibrary(
        IEnumerable<RingCabinetTemplate> templates);

    public IReadOnlyList<RingCabinetTemplate> Templates { get; }

    public bool TryGet(
        TemplateId templateId,
        out RingCabinetTemplate? template);
}
```

合同：

- 构造器对输入做一次物化和防御性复制；
- `Templates` 按注册顺序返回只读视图；
- `TryGet` 使用 `TemplateId` 的既有值相等语义；
- `templateId == null` 是调用错误，应抛 `ArgumentNullException`，不把无效 key 混同为“未找到”；
- 未知合法 ID 返回 `false` 和 null；
- 成功返回 Library 内保存的同一个 `RingCabinetTemplate` 引用。

第一版不增加 `GetRequired`。当前 UI 和 orchestration 都能自然处理“未找到”，`TryGet` 已足够；强制取得可以由明确需要的调用方自行转换为其上下文错误。待出现多个确实需要 fail-fast 的生产调用点后再评估 `GetRequired`。

第一版也不增加 Add、Remove、Reload、SearchByName、FilterByCapability 或分页 API。

## 8. Interface Decision

第一版不引入 `IRingCabinetTemplateLibrary`。

当前只有一个进程内 Built-in 实现，没有第二个 Source、替换需求或 I/O 边界。为单个实现添加 interface 会提前冻结一套尚未经过 JSON/User/Manufacturer Source 验证的抽象。

采用普通、不可变 concrete Library 仍然具备良好测试性：测试可直接用测试模板构造独立实例，无全局状态、无外部依赖。

未来真正出现第二个 Source 时，再依据实际差异抽象：

- `TemplateSource`：负责异步/同步加载来源；
- `TemplateCatalog`：负责合并、冲突和来源优先级；
- 或 `IRingCabinetTemplateLibrary`：如果不同实现确实共享同一查询合同。

不能现在猜测未来抽象一定是哪一种。

## 9. TemplateId Contract

当前 `TemplateId` 是包含 trim 后字符串 `Value` 的 sealed record。Library 必须遵守而不是暗中改变其语义：

- 构造 `TemplateId` 时去除首尾空白；
- null、空字符串和纯空白由 `TemplateId` 构造器拒绝；
- 相等性使用当前 record/string 的 ordinal、区分大小写语义；
- Library 不进行第二次 trim、大小写折叠或 Unicode 归一化；
- 重复检测使用 `TemplateId` 的实际相等语义；
- Template 与 TemplateId 构造完成后不可改变；
- 只允许按 `TemplateId` 查找，不按 `Name`、数组位置或 Bay 数量充当 identity；
- Library 不生成 Guid，也不从 DisplayName、Name 或模板内容生成 TemplateId。

内置 ID 命名规范要求全部使用小写 ASCII，因此内置集合不会有仅大小写不同的两个 ID。若未来决定所有来源都必须大小写不敏感或强制小写，应先修改 `TemplateId` 的统一合同和测试，而不是只在某个 Library 实现中改变比较器。

`TemplateId` 是模板机器身份，不参与 CabinetId、IntervalId、SwitchId、TerminalId、ElectricalNodeId 或 SwitchAssemblyId。

## 10. Naming Convention

内置模板推荐采用与现有 `builtin:` 规则身份一致的命名空间格式：

```text
builtin:ring-cabinet/<family>/<variant-key>
```

约束：

- 固定 `builtin:` source prefix；
- 固定 `ring-cabinet` target segment；
- `<family>` 使用稳定、非本地化的机器词，例如 `conventional`、`integrated`、`mixed`；
- `<variant-key>` 由已确认的专业模板变体命名，可包含 `3-bay`、`4-bay` 等稳定结构信息；
- 全部使用小写 ASCII、数字和连字符；
- 不包含中文 DisplayName、UI 排序序号、厂家、年份或临时发布版本；
- 不因显示名称修改而改变 ID；
- 模板专业含义发生不兼容变化时发布新 variant key，不在原 ID 下悄然改变事实。

示意而非已批准模板：

```text
builtin:ring-cabinet/conventional/<approved-3-bay-variant>
builtin:ring-cabinet/integrated/<approved-4-bay-variant>
```

在 Function 排列和接地结构尚未确认前，不应把占位 variant 名作为生产 ID。无明确生命周期需求时不添加 `v1`、日期或厂家名。

## 11. Display Metadata

当前 `RingCabinetTemplate.Name` 已是用户可读模板名称，并且明确不自动成为实例 `RingCabinet.DisplayName`。第一版直接把它作为模板列表显示文本。

第一版不新增 `DisplayName` 重复字段，也不新增自由 `TemplateMetadata` 字典。UI 所需的以下信息可从 Template 派生：

- `Name`；
- `CabinetType`；
- `Bays.Count`；
- `RequiredCapabilities`；
- 各 Bay 的 Function 和 EquipmentConfiguration。

若未来需要本地化名称、说明、图标、来源、弃用状态或标签，应在真实 UI/source 需求出现后评估独立 descriptor。它不能复制 Bays、Capabilities 或配置事实。

## 12. Library Entry Decision

第一版 Library 直接保存并返回 `RingCabinetTemplate`，不增加 `RingCabinetTemplateDescriptor`。

当前 Descriptor 可提供的 `TemplateId`、Name、CabinetType、Bay 数量和 Capability 都已存在或可派生。增加 Descriptor 会制造同步义务和第二份事实源。

未来若出现不能合理放入 Runtime Template 的来源元数据，例如：

- 来源类型与来源文件；
- 本地化资源 key；
- 发布者、签名或 trust state；
- 弃用/兼容性状态；

再引入 source-owned descriptor。第一版 UI 直接枚举 Template 即可。

## 13. Built-in Template Policy

第一版 Built-in Library 只包含同时满足以下条件的模板：

1. 当前 Domain Builder 支持其 EquipmentConfiguration 与 Capability；
2. 当前 RuntimeLayout Builder 支持其 LayoutRule；
3. 当前 Domain 数量与组合规则允许创建；
4. 每个 BayIndex、BayFunction 和 GroundingStructureKind 都是明确、经确认的模板事实；
5. 对应 Application Builder 测试证明可成功生成；
6. 不需要 UI 再补充缺失专业事实。

因此“Runtime Model 可以表达”不是入库条件。测试 fixture、Demo 组合和历史设计示例也不是内置模板的专业批准依据。

第一批应保持很小：优先发布一个或少量经确认的 conventional 模板；只有 IntegratedFeeder 的 Function 组合和 GroundingStructureKind 全部确认后才加入 integrated/mixed 模板。不要为了覆盖 3/4/5/6 数量而一次性发布大量未经确认内容。

## 14. Legacy LoadSwitch Policy

3、4、5、6 间隔纯 LoadSwitch 在当前 Domain 数量范围内，但数量合法不等于 Function 排列已确定。

每个 built-in 必须逐 Bay 明确：

| Template fact | Requirement |
| --- | --- |
| Collection position | 决定物理 Sequence |
| `BayTemplate.Index` | 明确 BayIndex，可非连续 |
| `BayTemplate.Function` | 明确 Incoming/Outgoing/Tie/Metering/Reserve 等用途 |
| Equipment | 明确 `LoadSwitchConfiguration` |

当前仓库测试常使用 Incoming/Outgoing/Tie，只证明映射和创建技术链可用，不构成 3/4/5/6 间隔标准专业排列。`docs/p0-7-e-1-template-builder-runtime-design.md` 中的五间隔表同样是架构示例，不应自动升级为生产 built-in。

建议策略：

- 不允许 `Unknown` 或“待用户补充 Function”的内置模板；
- 不从 Sequence、Index 或设备类型推断 Function；
- F-2 实现 Library 机制可使用 test-local templates；
- 生产 Built-in 定义必须等待明确的逐 Bay 规则表；
- 第一批无需同时覆盖 3/4/5/6，确认一个发布一个。

Runtime Model 当前不是参数化模板编辑器，不能用缺失字段占位后让 UI 补齐。

## 15. IntegratedFeeder Policy

IntegratedFeeder 模板除 Index 与 Function 外，还必须逐 Bay 固定 `GroundingStructureKind`。Library 只保存明确值，不根据 Function、位置、CabinetType 或相邻间隔推断。

当前 `IntegratedFeederConfiguration` 支持：

- `UpperIsolationGrounding`；
- `UpperLowerGrounding`；
- `LowerLowerGrounding`。

这表示模型能承载三种结构，不表示某个标准四间隔或六间隔柜应采用哪一种。测试中覆盖三种值也只是映射验证。

第一版建议：

- 机制支持 integrated 和 mixed 模板；
- 在标准 Function 组合及每个 Bay 的 GroundingStructureKind 未确认前，不发布 integrated built-in；
- 确认后把每种不同结构作为明确模板事实；
- 若同一业务模板需要用户选择接地结构，留给未来参数化模板/创建配置，不在第一版 Library 引入参数系统。

## 16. PT Policy

第一版 Library 不包含 PT template。

虽然 Runtime Model 可由 `BayFunction.PT` 派生 `TemplateCapability.PTBay`，Domain Builder 当前会返回 `UnsupportedCapability`，且 PT Domain、Layout、Rendering、Persistence 尚未完成。

第一版目录语义是“当前可成功创建”，因此不应向最小 UI 暴露必然失败的模板，也不应要求 UI 在第一版实现 disabled capability catalog。

未来 PT 全链路完成后，可添加新的 built-in。最终可创建性仍由 Builder Outcome 判断，不能只靠 UI capability 提示。

## 17. DTU Policy

第一版 Library 不包含带 `DtuSecondaryConfiguration` 的模板。

Runtime Model 能表达 `DtuSecondary` 能力需求，但 Builder 当前明确拒绝，且 DTU 的 Domain/Layout/Rendering/Persistence 边界尚未实现。把它放入当前 Library 会违反“当前可创建目录”的合同并增加第一版 UI 复杂度。

未来 DTU 能力完成后，再把经确认的 SecondaryConfiguration 加入模板；Library 本身无需理解 DTU 拓扑或布局。

## 18. Two-Bay Policy

第一版 Library 不包含纯 LoadSwitch 两间隔模板。

Runtime Model 可以表达 2-bay，但现有 Domain 只允许纯 LoadSwitch 3–6 间隔，Builder 会返回 `DomainCreationFailure`。Library 不是“所有可表达输入”目录，不应发布已知无法创建的模板。

如果后续专业规则确认两间隔合法，应先独立修改并验证 Domain，再添加 built-in；不能在 Library 或 Builder 中补第三间隔、转换柜型或增加后门。

## 19. BayIndex Policy

每个 Built-in Template 必须显式构造每个 `BayTemplate.Index`。Library 不提供：

- `BayIndex = Sequence` 的运行时默认；
- 自动连续化；
- 缺号补齐；
- 排序或重新编号；
- 从名称解析编号。

固定模板显式写 `Index = 1, 2, 3` 是合法的模板事实，不是迁移 fallback。两者区别在于：值在模板定义中逐项可审查，而不是 Library/Builder 面对缺失数据时自动产生。

Template Bays 注册顺序继续决定 Domain Sequence；Library 不按 Index 排序。

## 20. BayFunction Policy

每个 Built-in Template 必须显式提供合法、已知的 `BayFunction`。

禁止：

- `BayFunction.Unknown`；
- 从 EquipmentConfiguration 推断；
- 从 Display Name/Template Name 推断；
- 从 Index、Sequence 或 Bay count 推断；
- 把所有未确认 Bay 默认为 Outgoing 或 Reserve。

如果某个候选模板的专业 Function 排列未冻结，该模板不进入 Built-in Library。Library 不负责补值，Runtime Model 继续在构造时拒绝 Unknown。

## 21. GroundingStructure Policy

每个 IntegratedFeeder Bay 的 `GroundingStructureKind` 必须在 Built-in 定义中显式给出并经过专业确认。

Library 不进行默认选择或推断。不同接地结构如果代表不同固定模板，可使用不同稳定 variant ID；如果它是创建时参数，则应在未来参数化模板设计中处理。

第一版不为此增加可变参数、options dictionary 或“默认接地结构”。

## 22. Immutability

推荐内部使用两种互补结构：

```text
registration-order array/read-only list
    -> stable enumeration

FrozenDictionary<TemplateId, RingCabinetTemplate>
    -> immutable lookup index
```

具体策略：

- 构造时 `IEnumerable` 只枚举一次并物化为新数组；
- 检查 null 和 duplicate 后，用 `Array.AsReadOnly` 暴露稳定顺序；
- 用 `ToFrozenDictionary` 建立 lookup；
- 不返回原始数组、List、Dictionary 或 mutable set；
- 查找返回同一 immutable Template 引用，不复制整个 Template；
- Template 的 Bays 和 RequiredCapabilities 已由现有模型防御性复制/冻结。

当前项目为 `net10.0`，Application 已使用 `System.Collections.Frozen`，无需新增包。第一版不需要 `ImmutableArray` 依赖。

## 23. Ordering

`Templates` 必须保留 Built-in 注册顺序。该顺序由 `BuiltInRingCabinetTemplates.CreateAll()` 中的显式列表决定，并由测试冻结。

不依赖：

- Dictionary/FrozenDictionary enumeration；
- TemplateId 字典序；
- Name 的本地化排序；
- Bay count 自动排序。

第一版不在 Runtime Model 或 Descriptor 增加长期 `SortOrder`。注册顺序足以满足单一内置目录和最小 UI。未来多 Source 合并时，再由 Catalog/Presentation policy 决定跨来源顺序。

## 24. Duplicate Handling

Library 构造时发现重复 `TemplateId` 必须立即失败：

- 不允许后者覆盖前者；
- 不允许静默忽略；
- 不允许自动改名；
- 不允许根据 Name 合并。

推荐抛 `ArgumentException`，参数名为 `templates`，消息包含冲突的 `TemplateId`。重复是调用方提供了无效集合，属于构造输入错误；不使用 `InvalidOperationException` 隐藏输入来源。

Built-in Library 的静态构造/Composition Root 若因此失败，应 fail fast，避免带歧义目录启动。

## 25. Built-in Construction

推荐增加一个集中定义类：

```text
BuiltInRingCabinetTemplates
    -> CreateAll(): IReadOnlyList<RingCabinetTemplate>
```

或以更小入口直接提供：

```text
BuiltInRingCabinetTemplates.CreateLibrary()
    -> RingCabinetTemplateLibrary
```

第一版推荐保留 `CreateAll()` 与 Library 构造分离：

- Built-in class 负责明确的模板内容；
- Library 负责集合合同、重复检测、顺序和查找；
- 测试可分别验证内容与容器；
- Composition Root 用 `new RingCabinetTemplateLibrary(BuiltInRingCabinetTemplates.CreateAll())` 组合一次。

所有模板应在该 Application 边界集中构造。禁止在 Desktop ViewModel、Dialog 点击事件、Builder 或每次 Build 时临时重建同名 built-in。

如果逐个模板内容变多，可在同一 Library 文件夹下按 family 拆 private factory 方法或内部定义类；第一版不为每个模板建立插件/注册类型。

## 26. Lifetime / Composition

Built-in Library 应是普通不可变对象，由 Desktop Composition Root 创建一次并注入后续模板选择入口。

不推荐 static global singleton：

- 隐藏依赖；
- 测试难以替换目录内容；
- 未来多 Source/不同 catalog 生命周期难以演进；
- 全局访问会诱导 Builder 自己查 Library。

不可变对象可以安全复用，生命周期可以等同应用会话或应用进程；无需 per-request 重建，也无需 Dispose。

在 F-2 只实现 Application Library 时，不必提前修改 Desktop Composition Root。F-3 接 UI 时再在真实 composition boundary 创建并传入。

## 27. Builder Boundary

Builder 输入保持：

```text
RingCabinetTemplate
```

Builder 不接受 TemplateId，不引用 `RingCabinetTemplateLibrary`，也不执行查找。调用链明确为：

```text
TemplateId
    -> Library.TryGet
    -> RingCabinetTemplate
    -> RingCabinetTemplateDomainBuilder
```

因此 Builder 对内置、未来 JSON、用户或厂家来源保持 source-agnostic。Library 不知道 Builder 的 SupportedCapabilities，也不生成 BuildResult。

## 28. Coordinator Boundary

`RingCabinetTemplateBuildRequest` 继续包含 `RingCabinetTemplate`，不改成 TemplateId。

Lookup 应发生在 Desktop/Application orchestration boundary，随后把模板对象放入现有 Request。Build Coordinator：

- 不依赖 Library；
- 不按 ID 查模板；
- 不保存 Catalog；
- 不解析来源；
- 不增加 fallback。

这避免 Build 请求在执行时依赖可变化的外部目录，并保持一次 Build 使用调用方已选择的明确 immutable Template。

## 29. Desktop Boundary

后续最小 UI 的推荐流程：

```text
Desktop composition creates Library once
        |
        v
ViewModel reads Library.Templates
        |
        v
User selects a Template/TemplateId
        |
        v
Library.TryGet (when lookup is needed)
        |
        v
RingCabinetTemplateBuildRequest(
    selectedTemplate,
    instanceDisplayName,
    position)
        |
        v
existing RingCabinetTemplateCreationController
```

Library 不知道 SelectedTemplate、Dialog、Button、Window、ViewModel、Position 或错误消息。Desktop 不复制 built-in 列表，也不在 UI 中以 switch/if 重新构造模板。

如果 UI 直接持有从 `Templates` 选择的 immutable Template，对创建路径不必再次按 ID 查找；TemplateId 仍用于稳定 selection value、测试和未来 source integration。

## 30. Project Persistence Boundary

第一版工程文件继续只保存生成后的 Domain 与 Layout 事实。打开工程不依赖 Template Library，也不根据 TemplateId 重新 Build。

禁止：

- 把 TemplateId 当作恢复 RingCabinet 的必要字段；
- 保存 Template 后在 Load 时重新生成 Domain/Layout；
- 因模板内容升级而改变已保存工程；
- 修改 DTO、FormatVersion 或 Migration。

生成完成后，Domain/Persistence 是工程事实源；Template 只是创建来源。

未来可评估可选、非权威的 `CreationMetadata.TemplateId`，用于审计、用户提示或“来源模板”展示。它必须：

- 不参与 Restore；
- 不参与 Stable ID；
- 不要求模板仍存在；
- 不覆盖已保存 Domain/Layout；
- 经过独立 Project metadata 与格式版本设计。

本轮不实现或承诺该 metadata。

## 31. Future Template Persistence

Project Persistence 与 Template Persistence 是两个不同边界：

```text
Project file
    -> generated Domain + Layout facts

Future template source
    -> RingCabinetTemplate definitions
```

未来 JSON Template 属于 Infrastructure 的 Template Source/loading adapter，并映射到 Application Runtime Model。它不应塞入 `ProjectDomainDto`。

第一版 Built-in Library 不引入：

- `JsonSerializer`；
- 文件路径或 watcher；
- Template schema/version；
- Template migration；
- 用户目录；
- 网络或数据库。

未来 JSON 格式必须独立设计字段完整性、TemplateId 冲突、安全验证、版本和错误隔离。

## 32. Future Multiple Sources

未来可能存在：

- Built-in Templates；
- User Templates；
- Manufacturer Templates；
- JSON/file Templates。

可能演进链路为：

```text
TemplateSource(s)
        |
        v
TemplateCatalog / conflict policy
        |
        v
ordered immutable RingCabinetTemplate view
```

届时必须真实决定：

- 不同 source 的 TemplateId namespace；
- duplicate 是拒绝、遮蔽还是显式版本选择；
- source priority；
- reload 和错误隔离；
- ordering 与 localization；
- capability/compatibility presentation。

第一版不定义 `TemplateCatalog`、`CompositeTemplateLibrary`、`TemplateSource` 或 interface。当前 concrete Library 的小 API 不妨碍未来由 Catalog 包装或替换。

## 33. Capability Boundary

Library 不计算或决定“当前可创建”。它只保存模板自带的派生 `RequiredCapabilities`，且第一版 Built-in 内容政策排除已知 unsupported 模板。

Capability 的最终事实边界仍是：

- Domain Builder：Domain 创建能力；
- RuntimeLayout Builder：Layout capability 与 Rule 支持；
- Coordinator：只传播类型化失败。

未来 UI 可以读取 RequiredCapabilities 做说明或预览，但不能把 UI 过滤当作最终校验。每次创建仍必须经过现有 Builder Outcome。

Library 不维护第二套 supported capability whitelist，也不调用 Builder 预构建全部模板来决定是否显示。

## 34. Testing Strategy

第一版测试放入现有：

```text
tests/DistributionDrawing.Application.Tests/
```

### 34.1 Library container tests

覆盖：

1. 用合法模板正常构造 Library；
2. `TryGet` 按精确 TemplateId 返回同一模板对象；
3. 未知 TemplateId 返回 false/null；
4. null key 按输入合同失败；
5. duplicate TemplateId 构造失败且不覆盖；
6. `Templates` 保持显式注册顺序；
7. 调用方修改原输入 List 不影响 Library；
8. 调用方不能通过 `Templates` Add/Remove；
9. 查找结果保持 Runtime Template 的不可变性；
10. 大小写按当前 TemplateId ordinal 语义处理。

### 34.2 Built-in content tests

对每个最终批准的 Built-in 固定验证：

- TemplateId 与 Name；
- CabinetType；
- Bay 数量和集合顺序；
- 每个 BayIndex；
- 每个 BayFunction，且不存在 Unknown；
- EquipmentConfiguration 类型；
- 每个 IntegratedFeeder 的 GroundingStructureKind；
- SecondaryConfiguration；
- LayoutRule；
- RequiredCapabilities；
- Library 的稳定展示顺序；
- 不包含 PT、DTU 或 2-bay（按第一版政策）。

### 34.3 Builder compatibility tests

至少对每个 Built-in 或每类代表模板调用真实 `RingCabinetTemplateDomainBuilder`：

- Library 返回的 Template 可直接 Build；
- BayIndex/Function/Sequence 保持；
- 不需要 Library-specific adapter；
- 不产生 Unknown fallback。

RuntimeLayout 全链验证可继续由 Rendering.Wpf.Tests 在 F-4 完成。Application Library 不引用 Rendering.Wpf。

Library 自身不调用 `Guid.NewGuid`、Domain Create 或 Builder；“不生成 Stable ID”主要通过生产 API/静态边界审查验证，而不是伪造一个 Library ID 生成测试。

测试不涉及 UI。

## 35. Planned Files

F-2 推荐新增：

```text
src/DistributionDrawing.Application/Templates/RingCabinets/Library/
├── RingCabinetTemplateLibrary.cs
└── BuiltInRingCabinetTemplates.cs

tests/DistributionDrawing.Application.Tests/
├── RingCabinetTemplateLibraryTests.cs
└── BuiltInRingCabinetTemplatesTests.cs
```

原则上无需修改：

- `DistributionDrawing.Application.csproj`（`System.Collections.Frozen` 已可用）；
- Solution 或 Application.Tests project（测试项目已存在并引用 Application）；
- Runtime Model；
- Domain Builder；
- Rendering.Wpf Builder/Coordinator；
- Domain、Infrastructure、Persistence、Desktop、Command 或 Selection。

若 F-2-A 先只实现 container mechanism，可暂不新增 `BuiltInRingCabinetTemplates.cs`，直到专业模板矩阵确认。

## 36. F Phase Slices

推荐按以下小切片推进：

### P0-7-F-1：Template Library Design

冻结本文的 Layer、API、ID、内容准入、不变性和来源边界。

### P0-7-F-2-A：Immutable Library Runtime

- 实现 concrete `RingCabinetTemplateLibrary`；
- 实现 duplicate、ordering、lookup 和 immutability 测试；
- 不依赖具体专业模板内容。

### P0-7-F-2-B：Approved Built-in Templates

- 先取得逐 Bay 专业规则表；
- 集中实现最小批准模板集合；
- 冻结 TemplateId 和展示顺序；
- 用 Application Domain Builder 验证每个模板可创建；
- 不含 PT、DTU、2-bay。

### P0-7-F-3：Minimal Template Selection UI

- Composition Root 创建一次 Library；
- UI 枚举模板、输入实例 DisplayName/Position；
- 调用既有 TemplateCreationController；
- 使用结构化 Build Failure；
- 不实现编辑器、JSON 或复杂 capability engine。

### P0-7-F-4：End-to-End Verification

- TemplateId lookup -> Build -> Command -> Project；
- Selection/Inspector/Undo/Redo/Dirty；
- Stable ID 与 Scene；
- 失败不进入 History；
- Windows/WPF 实机最小验收。

JSON、User Template Editor、Manufacturer Templates、PT 和 DTU 不进入当前 F 阶段。

## 37. Professional Rules Requiring Confirmation

以下内容不能由代码、测试 fixture、Demo 或数组位置自行决定，必须在 F-2-B 前由用户/专业规则确认：

1. 普通 LoadSwitch 3、4、5、6 间隔分别允许发布哪些标准模板；
2. 每个标准模板的逐 Bay 物理顺序；
3. 每个 Bay 的明确 BayIndex，及是否允许非连续/负号显示规则；
4. 每个 Bay 的 BayFunction：Incoming、Outgoing、Tie、Metering、Reserve 等；
5. 是否存在多个同数量但不同 Function 组合的模板，以及稳定 variant 名称；
6. 一二次融合 4/6 间隔的标准 Function 与设备组合；
7. 每个 IntegratedFeeder Bay 的 `GroundingStructureKind`；
8. mixed cabinet 哪些组合可作为标准模板，而非仅 Domain 可表达组合；
9. 模板面向用户的中文 Name；
10. 首批模板的明确展示顺序；
11. PT Bay 的 Domain/Layout/Rendering/Persistence、标准位置和 Function 组合（未来阶段）；
12. DTU 的模型、左右位置及与柜体的关系（未来阶段）；
13. 若未来支持参数化模板，哪些字段由模板固定、哪些允许用户选择。

在这些规则确认前，Codex 不应把 Incoming/Outgoing/Tie 测试排列、Demo 或 E-1 五间隔示例发布成 Built-in。

## 38. Risks

### 38.1 未确认专业事实被固化

风险最高。TemplateId 一旦作为稳定身份发布，后续悄然修改 Function 或接地结构会改变同一 ID 的专业含义。

控制：生产 Built-in 进入 F-2-B 前必须有逐 Bay 审核表；不把测试 fixture 当规范。

### 38.2 Library 与 Builder 能力漂移

Library 不应复制 supported capabilities。即使第一版内容政策只收录可创建模板，最终仍以 Builder Outcome 为准，并以兼容测试及时发现漂移。

### 38.3 TemplateId 规范分裂

Library 若使用大小写不敏感 comparer，会与当前 TemplateId record equality 不一致。

控制：第一版遵守现有 ordinal/case-sensitive 语义，built-in convention 统一小写；未来统一变更必须从 TemplateId 合同开始。

### 38.4 Ordering 变成隐藏 UI 事实

注册顺序是第一版最小展示顺序，但未来多 Source 后不够。

控制：不把 SortOrder 塞入 Runtime Model；多 Source 时由 Catalog/presentation policy 重新设计。

### 38.5 Static global access

全局 singleton 会让 Builder、UI 和测试形成隐藏耦合。

控制：Composition Root 创建普通不可变对象并显式传递。

### 38.6 Project 对 Library 产生恢复依赖

若工程只保存 TemplateId 并在 Restore 重建，模板升级会破坏 Stable ID 和历史工程。

控制：Project 继续保存完整 Domain/Layout；TemplateId 最多是未来非权威 creation metadata。

### 38.7 过早多来源抽象

当前 interface/catalog/source hierarchy 没有第二个真实实现验证。

控制：先实现 concrete Library，未来按实际 I/O、冲突和 reload 需求提取。

## 39. Final Architecture Decision

P0-7-F-1 冻结以下设计：

```text
Desktop composition / future UI
        |
        v
RingCabinetTemplateLibrary (Application, immutable concrete object)
        |
        +--> ordered IReadOnlyList<RingCabinetTemplate>
        +--> FrozenDictionary<TemplateId, RingCabinetTemplate>
        |
        v
selected RingCabinetTemplate
        |
        v
existing BuildRequest / Domain Builder / Layout Builder / Coordinator
        |
        v
existing Add Command / Project Runtime
```

最终决策：

- Library 属于 Application；
- 第一版使用 concrete immutable Library，不引入 interface；
- API 只有稳定枚举和 `TryGet`，不增加 `GetRequired`；
- TemplateId 使用现有 trim + ordinal case-sensitive 合同，内置 ID 统一小写；
- `RingCabinetTemplate.Name` 直接承担第一版显示名称，不增加 Descriptor；
- 枚举保留显式注册顺序，查找使用 frozen dictionary；
- duplicate ID 构造时 fail fast；
- 普通对象由 Composition Root 创建一次，不使用 static global singleton；
- Builder、Coordinator 与 BuildRequest 不依赖 Library 或 TemplateId lookup；
- 第一版目录只含当前可成功创建且专业事实已确认的模板；
- PT、DTU、2-bay 和未确认 Function/GroundingStructure 模板不入库；
- Project Restore 不依赖 TemplateId，JSON Template 属于未来独立 source；
- F-2 分成 container runtime 与 approved built-ins，避免机制实现被未确认专业规则阻塞或诱导猜测。

在 Library 机制层面没有生产实现阻断，可以进入 P0-7-F-2-A。进入 P0-7-F-2-B 前必须先确认第 37 节的首批模板专业矩阵。
