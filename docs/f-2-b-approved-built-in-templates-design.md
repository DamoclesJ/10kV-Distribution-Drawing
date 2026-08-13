# F-2-B Approved Built-in Templates Design

> 状态：设计阶段，不包含生产代码或测试实现。
> 稳定基线：`b5d7d3e docs: add p0-7-f-2-b-0-c integration verification report`。
> 前置条件：BayFunction 已从当前模型删除，Project FormatVersion 4 与 Windows 全链路验证已完成。

## 1. Background and Goals

P0-7-F-2-A 已提供不可变的 `RingCabinetTemplateLibrary` 容器，但当前 Library 仍没有正式批准的内置模板内容。F-2-B 的目标是建立第一批由系统维护、具有稳定身份、可被当前 Builder 完整创建的 Approved Built-in Templates。

Approved Built-in Template 解决的是：

- 系统正式提供哪些模板；
- 每个模板的稳定 `TemplateId` 和显示名称是什么；
- 模板明确包含哪些 Bay 结构、初始 BayIndex、LayoutRule 和 SecondaryConfiguration；
- 模板如何进入现有 Build、Command、Rendering 和 Persistence 链路；
- 模板专业内容变更时采用什么兼容策略。

F-2-B 不重新设计 Template Runtime、Domain Aggregate、Layout Builder 或 CommandStack。Built-in Template 是已有不可变 Runtime Model 的批准实例，不是新的建模体系。

## 2. Relationship with the Existing TemplateLibrary

现有 `RingCabinetTemplateLibrary` 继续是简单的不可变容器：

```text
ordered RingCabinetTemplate instances
        |
        +--> stable enumeration order
        +--> TemplateId uniqueness
        +--> TryGet(TemplateId)
```

Approved Built-in Templates 作为构造完成的 `RingCabinetTemplate` 集合注册进 Library。Library 不知道模板为何被批准，也不执行专业验证、Build、Capability 预测或版本迁移。

推荐后续实现新增一个 Application 层构造入口，例如 `BuiltInRingCabinetTemplates.CreateLibrary()` 或等价 factory。它负责按批准顺序构造模板并传给现有 Library；不修改 Library API，也不引入全局可变 singleton。

边界冻结如下：

- Library 保存和查询模板；
- Built-in factory 定义批准内容和显式顺序；
- Runtime Model 保证单个模板对象不变量；
- Builder/Domain 决定模板能否实际创建；
- Desktop 负责选择模板并发起创建；
- Infrastructure 不依赖 Library 恢复工程。

## 3. Built-in Template and User Template Boundary

### 3.1 Approved Built-in Template

Built-in Template：

- 随应用版本发布；
- 由代码仓库评审和测试批准；
- 使用 `builtin:` 命名空间下的稳定 `TemplateId`；
- 对调用方只读；
- 内容必须与当前 Domain Builder 和 RuntimeLayout Builder 能力一致；
- 不依赖外部文件、用户目录、厂家插件或网络；
- 不允许用户原地编辑并覆盖同一 Built-in ID。

### 3.2 User Template

User Template 是未来独立来源：

- 不使用 `builtin:` 身份命名空间；
- 可能来自 JSON、用户配置或其他 Template Source；
- 需要独立的 schema、验证、冲突和生命周期规则；
- 即使内容与某个 Built-in 相同，也不能冒用其 `TemplateId`；
- 不得修改 Built-in Library 内对象。

第一版不实现 User Template、Composite Catalog 或 Template Editor。未来出现第二种真实来源后，再依据实际加载和冲突需求抽象 `TemplateSource` 或 `TemplateCatalog`。

## 4. Approved Built-in Template Lifecycle

Approved Built-in Template 生命周期为：

1. 专业规则确认模板结构；
2. Application 层以明确代码构造 immutable `RingCabinetTemplate`；
3. 注册进 `RingCabinetTemplateLibrary`，重复 ID 立即失败；
4. Desktop 枚举并显示 Library 中的模板；
5. 用户选择后，调用方取得同一个 Template 实例；
6. Template 只在创建时参与 Build；
7. BuildResult 中的 Domain Aggregate 和 RuntimeLayout 进入 CommandStack；
8. Project Persistence 保存生成后的 Domain/Layout，不依赖 Template 再次 Build；
9. Undo/Redo 复用原 Command 保存的对象，不重新查询 Library；
10. 应用升级后，已有工程仍可独立恢复。

模板内容一旦在稳定 `TemplateId` 下发布，不得悄然进行改变专业语义的修改。纯显示名称修正可以保留 ID；Bay 数量、EquipmentConfiguration、结构类型或 LayoutRule 的不兼容变化应发布新的 `TemplateId`。旧 ID 的保留期限应在真正出现替换需求时单独决策。

## 5. First Target Template: 10kV Ring Cabinet Without Bay-Function Classification

### 5.1 Terminology Decision

本设计将“10kV Ring Cabinet（无间隔环网柜）”解释为：

> 具有真实物理 Bay/Interval，但不保存、不要求也不推断 Incoming、Outgoing、Tie、Reserve 或 Metering 等间隔功能分类的 10kV 环网柜。

它不表示零物理间隔。当前 `RingCabinetTemplate`、`RingCabinetDefinition` 和 `RingCabinet` 均要求至少一个 Bay/Interval；若业务真正要求“零间隔柜体”，那将是新的 Domain/Rendering 能力，不能作为当前 Built-in Template 偷渡实现。

### 5.2 Template Identity and Metadata

首个目标属于 Conventional Ring Cabinet family：

- `CabinetType`：`RingCabinetTemplateType.Conventional`；
- `SecondaryConfiguration`：`NoSecondaryConfiguration.Instance`；
- `LayoutRule`：`RingCabinetLayoutRule.Default`；
- Template name：使用经产品确认的中文显示名称；
- `TemplateId`：使用 `builtin:ring-cabinet/conventional/<approved-variant>`；
- instance DisplayName：由创建请求提供，不从 Template name 自动复制。

一个 immutable Template 必须拥有确定的 Bay 数量。因此，3、4、5、6 Bay 应分别成为独立模板，而不是一个带运行时数量参数的“参数化模板”。首个实际目录项的 Bay 数量和正式名称必须在实现前由专业规则确认。

### 5.3 Bay Types

第一目标模板只使用当前已支持且符合 Conventional family 的：

```text
BayTemplate
  Index = explicit positive BayIndex
  EquipmentConfiguration = LoadSwitchConfiguration
```

规则：

- 每个物理 Bay 都显式出现在 `Bays` 集合中；
- 集合注册顺序定义物理 Sequence；
- 初始 BayIndex 对批准模板显式写为 `1..N`；
- 不根据 BayIndex 排序或重新连续化；
- 不包含 BayFunction 或任何 Direction/Role/Purpose 替代字段；
- 不在第一模板中混入 `IntegratedFeederConfiguration`；
- 不以 LoadSwitch 结构推断 Incoming 或 Outgoing。

BayIndex `1..N` 是新实例的初始值，不是永久不可编辑的模板身份。未来实例 BayIndex 编辑必须通过独立 Command 完成，并保持 Sequence 不变；该能力不属于 F-2-B。

### 5.4 Interval Types

`LoadSwitchConfiguration` 由现有 Domain Builder 映射为：

```text
IntervalKind.LoadSwitchInterval
  + LoadSwitch
  + GroundSwitch
  + LoadSwitchThreePosition SwitchAssembly
```

初始 LoadSwitch 和 GroundSwitch 状态继续由现有 Builder 明确设置为 `Open`。Built-in factory 不绕过 Builder，不直接构造 `RingCabinetIntervalDefinition` 或 Domain Aggregate。

PT 不属于本模板。未来 PT 必须使用 dedicated IntervalKind、PT EquipmentConfiguration 和柜内专用拓扑表达，不能通过名称、BayIndex、Function、虚拟 Cable 或普通 External Cable Terminal 模拟。

### 5.5 Terminal Rules

Built-in Template 不直接保存或生成 Terminal ID。Terminal 与内部拓扑由 `RingCabinet.Create` 根据真实 IntervalKind 创建。

当前 LoadSwitch Interval 的 Terminal 合同保持为：

- LoadSwitch bus-side terminal 连接 MainBus node；
- LoadSwitch circuit-side terminal 连接 interval Circuit node；
- GroundSwitch device-side terminal 连接同一 Circuit node；
- GroundSwitch ground-side terminal 连接 Earth node；
- 每个普通 LoadSwitch Interval 具有一个由 Domain 创建的 external terminal；
- external terminal 的 owner、role、voltage、node 和 AllowedConnectionTypes 继续由 Domain 定义；
- Template 不保存 TerminalId，不生成 Stable ID，也不定义外部 Cable 连接。

选择模板只创建未连接的柜体实例。实际外部连接由后续连接命令处理，不属于 Built-in Template 内容。

### 5.6 Layout Rules

首个模板只使用：

```text
RingCabinetLayoutRule.Default
RuleId = builtin:ring-cabinet/default-v1
```

现有 Layout Builder 继续是布局事实源：

- Cabinet position 来自 Template Build Request；
- Bay 的水平顺序来自 Domain Sequence；
- Layout 不按 BayIndex 排序；
- 每个 interval 使用现有固定宽度、高度、padding 和 gap；
- Cabinet width 由 interval 数量计算；
- LoadSwitch 和 GroundSwitch 的相对位置由 `RingCabinetLayoutFactory` 决定；
- Built-in Template 不复制 geometry constants，也不直接构造 RuntimeLayout。

若未来出现不同物理外观，应新增明确的 LayoutRule 和对应 Rendering 实现，不能在同一个 RuleId 下改变几何语义。

### 5.7 Symbol Mapping Rules

Symbol 映射继续由 Rendering.Wpf 根据 Domain 结构决定，而不是由 Template 保存 SymbolKind：

```text
RingCabinet
  → SymbolKind.RingCabinet

IntervalKind.LoadSwitchInterval
  → LoadSwitchIntervalSymbol
  → SymbolKind.RingCabinetInterval

SwitchKind.LoadSwitch
  → corresponding switch symbol

SwitchKind.GroundSwitch
  → corresponding grounding switch symbol
```

`SymbolLibrary.ResolveSwitchKind` 和 `ResolveVisualState` 继续负责设备类型、状态到视觉符号的映射。Template 不携带 WPF、颜色、线宽、SymbolKind、SceneElement 或 HitTest 数据。

## 6. Creation Flow

Approved Built-in Template 的完整创建流程为：

```text
Template Selection
        |
        v
RingCabinetTemplateLibrary.TryGet(TemplateId)
        |
        v
Template Instance (immutable RingCabinetTemplate)
        |
        v
RingCabinetTemplateBuildRequest
        |
        v
Application Domain Builder
        |
        v
RingCabinetDefinition + RingCabinet Aggregate
        |
        v
Rendering.Wpf RuntimeLayout Builder
        |
        v
RingCabinetLayout + Rendering Scene
        |
        v
AddRingCabinetCommand / CommandStack
        |
        v
Project Persistence V4 saves Domain + Layout
```

关键合同：

- Library lookup 发生在 Desktop/orchestration boundary；
- BuildRequest 继续携带 `RingCabinetTemplate`，不改为 TemplateId；
- Builder 不知道 Library 或 Built-in source；
- Template 每次创建只 Build 一次；
- Persistence 不根据 TemplateId 重建工程；
- Redo 不重新查询 Library 或重新 Build；
- Project 文件保存生成后的 Stable IDs、Domain 和 Layout。

## 7. Mapping to Current Architecture

### 7.1 Domain

Domain 继续拥有生成后的电气结构事实：RingCabinet、IntervalKind、Switch、Terminal、ElectricalNode、SwitchAssembly、BayIndex、Sequence 和 Stable IDs。Domain 不依赖 TemplateLibrary，不保存 Built-in 身份，也不恢复 BayFunction。

### 7.2 Application

Application 拥有：

- Template Runtime Model；
- `RingCabinetTemplateLibrary`；
- 后续 Built-in factory/definitions；
- Template → Domain Builder。

Approved 内容应在 Application 创建。它不依赖 Rendering.Wpf、Desktop、Infrastructure 或文件系统。

### 7.3 Rendering.Wpf

Rendering.Wpf 继续拥有 RuntimeLayout Builder、LayoutFactory、SymbolLibrary、Scene 和 HitTest 映射。它只消费 Domain BuildResult、LayoutRule 和 Position，不查询 TemplateLibrary，也不保存 Built-in metadata。

### 7.4 Desktop

Desktop 后续负责：

- 从 Library 枚举可选模板；
- 保存用户选择的 TemplateId；
- 通过 `TryGet` 取得 Template；
- 收集实例 DisplayName 和 Position；
- 构造 BuildRequest；
- 调用现有 Template Creation Controller；
- 显示类型化 Build Failure。

Desktop 不编辑 Built-in Template，不临时重建模板内容，也不直接构造 Domain/Layout。

### 7.5 Infrastructure

Infrastructure V4 继续保存已生成的 Domain Aggregate 和 RuntimeLayout。第一版不保存 TemplateId，不引入 Built-in Template DTO，也不要求打开工程时加载 TemplateLibrary。

未来如需审计创建来源，可另行评估 optional creation metadata；该 metadata 不能成为 Restore 或 Redo 的依赖。

## 8. Approval Rules for Built-in Content

一个模板进入 Approved Built-in Catalog 前必须满足：

1. `TemplateId`、显示名称和展示顺序已确认；
2. Bay 数量已确认；
3. 每个 Bay 的显式 BayIndex 和 EquipmentConfiguration 已确认；
4. SecondaryConfiguration 已确认且当前 Builder 支持；
5. LayoutRule 已有对应实现；
6. Template.RequiredCapabilities 全部被当前 Domain/Layout Builder 支持；
7. Template → Domain → Layout → Command 的集成测试通过；
8. V4 save/reload 后 Stable IDs 和结构保持；
9. 不包含 PT、DTU 或其他未实现能力；
10. 不包含 Incoming/Outgoing/Tie 等已删除语义。

第一批 Catalog 只发布用户当前能够成功创建的模板，不展示“已知但不支持”的占位模板。

## 9. Testing Strategy for the Implementation Slice

后续 F-2-B 实现至少验证：

- Built-in factory 返回不可变 Library；
- TemplateId 唯一且稳定；
- 枚举顺序等于批准顺序；
- Template name、CabinetType、Bay 数量和 BayIndex 精确匹配批准内容；
- 所有首批 Bay 使用明确的 EquipmentConfiguration；
- 不存在 BayFunction、Direction、Role 或 Purpose；
- SecondaryConfiguration 和 RequiredCapabilities 正确；
- 所有模板均能被真实 Domain Builder 消费；
- 所有模板均能被 RuntimeLayout Builder 消费；
- Domain Sequence 按 Template Bays 顺序生成；
- Layout identity 与 Domain Stable IDs 一致；
- 连续 Build 生成不同实例 ID，同一次 Build 内身份一致；
- V4 persistence round-trip 不依赖 TemplateLibrary；
- Library 与 Built-in factory 不调用 `Guid.NewGuid`、Domain Create、Layout 或 Persistence。

测试数据必须区分 Approved Built-in 内容和 test-only fixtures，避免测试辅助对象被误认为正式模板。

## 10. Explicit Non-Goals

F-2-B 明确不实现：

- 自由 CAD 编辑；
- 任意拓扑生成；
- 用户自定义模板编辑器；
- JSON/User/Manufacturer Template Source；
- Template persistence schema；
- 参数化模板引擎；
- PT 或 DTU Domain；
- Cable/Overhead line 自动连接；
- Power flow、Direction、Source/Load role；
- BayIndex 编辑 Command；
- 新的 CommandStack、Undo/Redo 或 Rendering architecture。

## 11. Professional Decisions Required Before Implementation

当前架构已经具备实现 Built-in Catalog 的技术条件，但首个具体模板仍需冻结以下专业内容：

- “无间隔环网柜”的正式业务名称，确认其含义为无 Function 分类而非零物理间隔；
- 首个模板的物理 Bay 数量；
- 是否先批准 3 Bay，或同时批准 3/4/5/6 Bay 独立模板；
- 各模板正式中文显示名称；
- Catalog 展示顺序；
- 首批模板是否全部为 `LoadSwitchConfiguration`；
- 若纳入 IntegratedFeeder，具体 Bay 组合及 `GroundingStructureKind`；
- Built-in TemplateId 的最终 variant key。

这些内容不能由 BayIndex、Sequence、设备名称或 UI 顺序推断。未确认的模板不得进入 Approved Catalog。

## 12. Risks

- 将“无间隔”误解为零物理 Interval，会与当前 Runtime/Domain 不变量冲突；
- 在专业内容未确认时发布 ID，会把临时假设冻结成长期合同；
- 修改既有 TemplateId 下的结构，会导致同一身份跨版本语义漂移；
- 在 Template 中复制 Terminal、Layout 或 Symbol 规则，会形成多个事实源；
- 保存 TemplateId 并在 Restore/Redo 时重新 Build，会破坏 Stable ID；
- 把 Built-in 与 User Template 混为一个可变集合，会破坏来源和覆盖边界；
- 为首个模板提前引入参数化或通用拓扑 DSL，会扩大 F-2-B 范围。

## 13. Final Architecture Decision

F-2-B 采用以下设计：

```text
Application Built-in definitions
        |
        v
immutable RingCabinetTemplate instances
        |
        v
existing RingCabinetTemplateLibrary
        |
        v
Desktop selection
        |
        v
existing Build / Layout / Command / Persistence chain
```

首个目标是无 BayFunction 分类的 Conventional 10kV Ring Cabinet family，使用显式物理 Bays、`LoadSwitchConfiguration`、`NoSecondaryConfiguration` 和现有 Default LayoutRule。Template 不拥有 Terminal IDs、Layout geometry 或 SymbolKind；这些继续分别由 Domain 和 Rendering.Wpf 生成。

在首个模板的 Bay 数量、正式名称、展示顺序和最终 TemplateId 获得专业确认后，可以进入 Approved Built-in Templates 实现切片。若“无间隔”实际表示零物理间隔，则当前 F-2-B 不应实施该模板，应先开启独立 Domain/Rendering capability design。
