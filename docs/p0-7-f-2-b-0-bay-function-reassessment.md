# P0-7-F-2-B-0 BayFunction Requirement Reassessment

## 1. Context

P0-7-E 已完成模板运行时到命令历史的基础链路，P0-7-F-2-A 已提供不可变的模板库容器。P0-7-F-2-B 原计划定义首批 Approved Built-in Templates，但当前 `BayFunction` 合同要求每个模板间隔预先声明 `Incoming`、`Outgoing`、`Tie` 等分类。

最新专业规则确认这些分类不是当前 10kV 配电绘图项目需要长期保存和解释的稳定事实。因此，在发布 Built-in Templates 前必须重新评估 `BayFunction` 是否仍应属于核心 Domain 和 Template Runtime Model。

本审查基于 HEAD `ef532d1` 的实际代码。它不修改生产代码、测试、格式版本或迁移实现。

## 2. Updated Business Requirement

当前项目确认的业务边界如下：

- 一个物理间隔是“进线”“出线”还是“联络”，可能随电源方向、潮流方向和运行方式变化。
- 当前软件不做潮流分析，不维护运行方式，也不根据电源侧、负荷侧、左右位置、顺序或电缆方向重新分类间隔。
- `Incoming`、`Outgoing`、`Tie` 不驱动 Rendering、Topology、Command、Selection、Inspector 或 Dirty。
- 第一版 Built-in Template 不应要求用户预先指定这些分类。
- 当前稳定事实是物理排列 `Sequence`、实例业务编号 `BayIndex`、设备/间隔结构、开关与接地状态，以及由 Terminal、Node、Switch、SwitchAssembly 和 Connection 组成的电气拓扑。

`Sequence` 和 `BayIndex` 必须继续区分：

- `Sequence` 表示柜内物理排列顺序。
- Built-in Template 创建实例时可显式给出 `BayIndex = 1..N` 作为初始编号。
- 实例创建后，未来允许通过专用编辑命令把编号改为例如 `1, 2, 5, 7`，而不改变 `Sequence`。
- 模板中的初始 `BayIndex` 不是对实例的永久约束；本审查不设计或实现 BayIndex 编辑命令。

## 3. Current BayFunction Model

当前 Domain 枚举包含：

```text
Unknown
Incoming
Outgoing
Tie
PT
Metering
Reserve
```

该枚举当前跨越了至少三种不同语义：

- `Incoming`、`Outgoing`、`Tie`：随运行方式变化的分类。
- `PT`：设备/间隔结构能力。
- `Metering`、`Reserve`：尚未冻结的用途、标签或规划状态。

这些概念被放在同一个必填字段中，导致模型把运行分类、结构能力和未定义标签混为一体。

当前创建路径拒绝 `Unknown` 和 `PT`，恢复路径允许历史迁移产生的 `Unknown`，但仍拒绝 `PT`。因此 `Unknown` 实际上是历史格式兼容值，而不是可发布模板的合法业务值；`PT` 则只是尚未实现能力的占位标识。

## 4. Repository Usage Audit

### 4.1 Production usage by layer

| Layer | File / type | Usage classification | Actual effect |
| --- | --- | --- | --- |
| Domain | `BayFunction.cs` | Model declaration | 定义枚举，无独立行为 |
| Domain | `RingCabinetInterval` | Validation + data passthrough | 校验枚举、拒绝 PT、保存属性；不影响拓扑 |
| Domain | `RingCabinetIntervalDefinition` | Validation + mapping | 创建参数必填，拒绝 Unknown/PT，向 Aggregate 透传 |
| Domain | `RingCabinetDefinition` | Validation | 检查 Function 已定义且非 Unknown/PT |
| Domain | `RingCabinetRestoreDefinition` | Persistence/restore contract | 在恢复定义中携带 Function |
| Domain | `RingCabinet` | Validation + mapping | Create/Restore 透传 Function；结构创建按 `IntervalKind` 分支 |
| Application | `BayTemplate` | Template Runtime + validation | Function 为必填模板事实，拒绝 Unknown |
| Application | `RingCabinetTemplate` | Capability derivation | 仅以 `Function == PT` 派生 `TemplateCapability.PTBay` |
| Application | `RingCabinetTemplateDomainBuilder` | Mapping | 将 Template Function 直接映射到 Domain Definition |
| Infrastructure | `ProjectRingCabinetIntervalDto` / `ProjectDomainMapper` | Persistence + mapping | V3 保存、解析并恢复 Function |
| Infrastructure | `ProjectFormatMigration` | Compatibility/migration | V2 → V3 写入 `function = "unknown"`，不推断业务分类 |
| Rendering.Wpf | `RingCabinetCreationConfiguration` | Data passthrough | 手工创建配置携带 Function |
| Rendering.Wpf | `RingCabinetCreationFactory` | Mapping | 将配置 Function 传入 Domain Definition |
| Desktop | `RingCabinetCreationViewModel` | UI validation + mapping | 强制用户选择非 Unknown/PT Function |
| Desktop | `RingCabinetCreationDialog.xaml` | UI binding | 显示 Function 选择控件 |
| Desktop | `RingCabinetCompositionDemoFactory` | Demo fixture | 示例硬编码 Incoming/Outgoing/Tie |

没有生产使用点属于“根据 Function 改变真实业务行为”。现有使用均属于字段自身校验、DTO/Builder 映射、历史兼容或 UI 对该必填字段的维护。

### 4.2 Explicitly absent production behavior

全仓库审查未发现以下系统根据 `BayFunction` 改变行为：

- `RingCabinetLayoutFactory`、Interval/Switch symbol、Bounds、Anchor 或 Geometry；
- Terminal、ElectricalNode、Switch、SwitchAssembly 或 Connection 创建；
- Scene projection、HitTest 或 Selection resolver；
- Add/Remove Command、CommandStack、Undo/Redo 或 Dirty；
- SelectionReference、SelectionTransition 或 Inspector projection；
- Stable ID 生成或恢复。

### 4.3 Tests

现有测试中的 `BayFunction` 使用可归为以下类型：

- Domain 构造 fixture：`TestFixtures.cs`、`TopologyBoundaryTests.cs`、`IntegratedFeederIntervalEvaluationTests.cs`、`PoleAttachmentTests.cs`，用于满足现有必填 API。
- Domain metadata validation：`RingCabinetBayMetadataTests.cs`，验证 Unknown/PT/未定义值的现有防御。
- Application Template/Builder：`BayTemplateTests.cs`、`RingCabinetTemplateTests.cs`、`RingCabinetTemplateDomainBuilderTests.cs`，验证 Function 保存、映射及 PT Capability 派生。
- Application Library：`RingCabinetTemplateLibraryTests.cs` 只在 test-only Template fixture 中提供现有必填 Function；Library 本身不读取该字段。
- Infrastructure：`ProjectPersistenceRoundTripTests.cs` 和 `ProjectFormatMigrationTests.cs`，验证 V3 字段保存、历史 `unknown`、损坏字段拒绝及 Stable ID 保持。
- Rendering.Wpf：`RingCabinetTemplateLayoutBuilderTests.cs`、`RingCabinetTemplateBuildCoordinatorTests.cs`，只为合法 Template/Domain fixture 提供 Function。
- Desktop：`RingCabinetTemplateCreationControllerTests.cs`，只为组件集成 fixture 提供 Function。

这些测试证明当前 API 和持久化合同，而不是证明 Incoming/Outgoing/Tie 对业务结果有影响。删除字段时需要迁移测试，但不应把既有测试数量误判为业务必要性。

### 4.4 Documentation

`BayFunction` 出现在以下文档中：

- `p0-7-a-review.md`
- `p0-7-c-builder-readiness-review.md`
- `p0-7-d-2-a-business-rules.md`
- `p0-7-d-2-domain-compatibility-decision.md`
- `p0-7-d-3-domain-persistence-design.md`
- `p0-7-d-4-b-1-r1-fix-plan.md`
- `p0-7-d-4-b-1-r2-api-migration-plan.md`
- `p0-7-d-4-b-2-persistence-migration-plan.md`
- `p0-7-d-4-implementation-plan.md`
- `p0-7-e-1-template-builder-runtime-design.md`
- `p0-7-e-2-a-template-runtime-model-implementation-plan.md`
- `p0-7-e-2-b-1-template-runtime-model-implementation-plan.md`
- `p0-7-e-2-c-1-builder-core-design.md`
- `p0-7-f-1-template-library-design.md`
- `template-builder-design.md`
- `template-runtime-model-design.md`
- `template-system-design.md`

这些文档记录当时已冻结的模型，而不是最新专业规则。若完成删除，应更新 `project-current-state.md` 和面向当前架构的有效文档；历史阶段报告可保留为决策轨迹，并通过新决策文档明确 superseded 边界。

## 5. Domain Impact

### 5.1 Current Domain behavior

`RingCabinet.Create` 真正按 `RingCabinetIntervalDefinition.IntervalKind` 选择 LoadSwitch 或 IntegratedFeeder 结构，并据此创建 Switch、Terminal、ElectricalNode 和 SwitchAssembly。`Function` 仅被传入 interval 并保存。

`RingCabinet.Restore` 也只校验并透传 Function。恢复的实体 ID 来自 RestoreDefinition；Function 不参与实体关联或结构校验。

### 5.2 Effect of removal

完全删除 `BayFunction` 会要求修改：

- `RingCabinetInterval` 的属性和构造参数；
- `RingCabinetIntervalDefinition` 的 factory 参数和 validation；
- `RingCabinetDefinition` 的 metadata validation；
- `RingCabinetRestoreIntervalDefinition` 的恢复合同；
- 所有调用这些 API 的 Builder、Factory、Persistence mapper、Demo 和测试。

这些是 API 和数据合同修改。按当前实现，删除不会改变：

- LoadSwitch / IntegratedFeeder 结构选择；
- topology 创建；
- switch/grounding state；
- Aggregate 的 interval 顺序；
- Stable ID 生成和恢复；
- Domain Command 行为。

因此应明确区分“迁移面较广”和“存在业务行为依赖”：前者成立，后者不成立。

## 6. Persistence Impact

### 6.1 Current format

`ProjectFileFormat.CurrentVersion` 当前为 3。V3 interval DTO 要求 `function` 字段；保存时编码全部枚举值，读取时解析为 `BayFunction`。缺失或不支持的 V3 Function 当前被视为损坏数据。

V2 → V3 migration 使用：

```json
{
  "bayIndex": "sequence 的值",
  "function": "unknown"
}
```

这是兼容性填充，不是从旧文件推断 Incoming/Outgoing/Tie。

### 6.2 Recommended future V4 contract

正式从当前 Domain 删除 Function 时应提升到 FormatVersion 4：

- V4 canonical DTO 不再保存 `function`。
- V4 save 不输出该字段。
- V4 restore 不需要也不解析该字段。
- V3 → V4 migration 删除或忽略旧 `function`，保留 `bayIndex`、sequence、结构、状态、拓扑和全部 Stable ID。
- V1/V2 文件继续沿已有迁移链进入 V3，再由 V3 → V4 去除 Function；不尝试推断分类。
- 旧 V3 中的合法 `function` 值只视为 legacy input，不进入新 Domain。

对 V3 缺失或未知 Function 的处理必须在 V4 迁移设计中显式冻结。由于该字段在 V4 已被判定无业务语义，推荐 V3 → V4 对它采取“读取原始 JSON 后忽略/删除”的兼容策略，而不是为无意义字段阻断其余合法工程数据。其他 V3 必填结构仍按原规则验证。

不能只删除 DTO 字段而保持 FormatVersion 3；那会改变同一版本的既有 schema。也不能要求 Project Restore 根据 TemplateId 重新 Build。

## 7. Template Runtime Impact

当前 `BayTemplate` 强制保存 Function，Domain Builder 又把它原样传入 Definition。这正是 F-2-B 被阻断的原因：任何 Built-in Template 都必须声明并不存在的稳定分类。

移除后，第一版 Built-in Template 可以自然表达为：

- 普通柜：Template identity/metadata、N 个有明确初始 BayIndex 的 Bay、`LoadSwitchConfiguration`、LayoutRule 和 SecondaryConfiguration。
- 一二次融合柜：相同骨架，但使用 `IntegratedFeederConfiguration`，并显式定义真实结构事实 `GroundingStructureKind`。

模板仍应显式注册每个初始 BayIndex，例如 `1..N`。Library 不自动生成、连续化或排序 BayIndex；Built-in construction 可以使用小型 helper 机械生成已批准数量的连续初始值，但该 helper 只能表达已冻结模板事实，不能成为运行时 fallback。

不可变 `RingCabinetTemplateLibrary` 本身无需改变职责。它目前不读取 Function，也不重新计算 Capability；删除只影响注册进 Library 的 Template 类型形状。

## 8. Rendering Impact

`RingCabinetLayoutFactory` 使用 Domain interval sequence 和 `IntervalKind` 生成 layout。Symbol、Anchor、Bounds、HitTest 和 Scene projection 没有读取 Function。

删除 Function 不需要新的布局规则，也不应改变任何几何事实源。Rendering.Wpf 中仅需删除手工创建配置对该字段的携带及 Factory 参数映射；这属于 API 清理，不是渲染行为修改。

## 9. Desktop / Inspector Impact

当前 Desktop 手工创建界面要求每行选择 Function，并禁止 Unknown/PT。这是当前字段造成的 UI 输入负担，不是用户已确认的业务需求。

Inspector / `PropertyProjector` 当前不展示或编辑 Function。Selection、Command、Dirty 和 Scene refresh 也不依赖它。删除后的预期 Desktop 影响是：

- 删除手工创建表格中的 Function 选择及相关 validation/mapping；
- Demo fixture 不再填充 Incoming/Outgoing/Tie；
- 不新增任何方向推断或替代字段；
- 不影响 Template Creation Controller、SelectionTransition、Undo/Redo 或 Inspector 解析。

如果未来需要显示临时的“进线/出线”文本，它应先有独立、明确的用户故事和生命周期，不能重新进入当前核心结构模型。

## 10. Stable ID Impact

`BayFunction` 不参与以下任何 ID 的生成、hash、lookup 或恢复：

- CabinetId；
- IntervalId；
- SwitchId；
- TerminalId；
- ElectricalNodeId；
- SwitchAssemblyId。

新 Aggregate 的 ID 由现有创建流程生成；Restore 使用持久化的显式 ID。删除 Function 不应改变 Stable ID 语义。V3 → V4 migration 必须用测试证明读取旧文件后上述 ID 原样保持。

## 11. PT Analysis

`PT` 与 Incoming/Outgoing/Tie 不同：它代表真实设备和间隔结构能力。但把它放在 `BayFunction` 中仍是错误的长期建模。

当前代码用 `BayFunction.PT` 派生 `TemplateCapability.PTBay`，随后 Domain Builder 拒绝该 capability；Domain Create/Restore 也拒绝 PT。这只是尚未实现功能的占位边界，不是已完成的 PT Domain Model。

长期建议：

- 从 `BayFunction` 一并删除 PT。
- 不在本轮或删除迁移中实现 PT。
- 未来 PT 进入范围时，由专用 `IntervalKind`、`PTEquipmentConfiguration`、PT Domain structure 或等价结构模型表达。
- `TemplateCapability.PTBay` 届时应由该结构配置派生，而不是由显示分类派生。
- PT Domain、Layout、Topology、Persistence 和 Inspector 应在独立切片中共同冻结。

不能因为当前 PT Domain 尚未实现，就把 PT 永久绑定在不合适的枚举上。

## 12. Metering / Reserve Analysis

### 12.1 Metering

当前没有 Domain、Rendering、Command、Selection 或 Inspector 行为读取 `Metering`。其语义可能是：

- 未来专用计量设备/间隔结构；或
- 用户可见用途标签。

在专业结构未确认前，它不是当前稳定 Domain 事实。若未来代表真实计量设备，应由专用 EquipmentConfiguration/IntervalKind 表达；若只用于显示，应进入明确的 optional annotation/metadata 模型。当前不应以必填 Function 保留。

### 12.2 Reserve

当前 `Reserve` 同样没有行为消费者。它更接近规划状态或业务标签，而非设备结构。若未来确有需求，应单独定义其编辑、持久化和展示语义，例如显式的可选状态或 annotation；不应继续作为每个间隔必须选择的 Function。

结论是：enum 已存在并不构成保留理由。Metering 和 Reserve 应随 BayFunction 从核心字段移除，未来按真实用例分别建模。

## 13. Option A: Remove BayFunction

### Shape

- Domain 不再保存 Incoming/Outgoing/Tie/PT/Metering/Reserve。
- Template Runtime 不再要求 Function。
- Manual creation 和 Built-in Templates 不再请求无意义分类。
- Persistence 升级 V4，并兼容读取 V1/V2/V3。

### Benefits

- 模型与最新业务事实一致。
- Built-in Templates 不需要虚构 Function 或使用 Unknown fallback。
- 删除重复 validation、mapping、DTO 和 UI 输入负担。
- PT 可以在未来进入正确的结构模型。
- 避免当前错误语义继续扩散到 Library、UI 和更多持久化文件。

### Costs

- 需要跨 Domain、Application、Infrastructure、Rendering、Desktop 和测试调整 API。
- 必须设计并验证 V4 migration。
- 历史文档和 fixture 需要明确更新或标记旧决策。

### Long-term complexity

一次性迁移成本较高，但长期模型最小且没有无消费者字段。

## 14. Option B: Retain Domain Field but Hide It from Template/UI

### Shape

Domain 和 V3 DTO 暂留 Function，Template/UI 不要求用户提供；内部必须注入兼容值。

### Consequences

- 当前可用的兼容值只能是 `Unknown` 或任意伪造分类。
- 使用 Unknown 需要放宽创建 validation，并把原先 migration-only 值变成正常业务值。
- 使用 Incoming/Outgoing 等默认值会直接违反最新专业规则。
- 字段仍会污染 Definition、RestoreDefinition、Builder、DTO、测试和未来 UI。
- 后续删除成本继续增长，Built-in Template IDs 和内容也可能被错误合同锁定。

该方案只有短期改动较小的优势，但会保留已确认错误的核心模型，不推荐。

## 15. Option C: Demote Function to Optional Metadata

### Shape

把 Function 改为 nullable/optional classification，不驱动 topology。

### Consequences

- 可以表达“当前无分类”，但仍需决定字段生命周期、编辑、持久化、显示和运行方式变化时的更新责任。
- 引入 null/optional validation、DTO compatibility 和 UI 状态，却没有当前消费者。
- PT 仍不能合理地作为 metadata；Metering/Reserve 的语义也没有因此澄清。
- 容易诱使未来代码把 optional label 当成结构事实。

只有出现明确的可选注释/标签需求后，才值得设计独立 metadata。当前采用该方案属于过早复杂化，不推荐。

## 16. Recommendation

明确推荐方案 A：正式删除 `BayFunction`，并且应在 P0-7-F-2-B Approved Built-in Templates 之前完成。

`BayFunction` 不应继续作为当前项目核心 Domain 字段。理由不是它“暂时没有 UI”，而是其成员混合了动态运行分类、结构能力和未定义标签，且当前系统没有任何需要该字段的业务行为。

F-2-B 若先行，将被迫在每个 Approved Template 中写入伪造 Function 或 Unknown，形成新的长期数据和测试合同。暂停 F-2-B 是正确边界。

删除不应伴随 Incoming/Outgoing 推断、潮流模型、PT 实现、BayIndex 编辑或新的 optional metadata。

## 17. Migration Strategy

由于 Domain API、Persistence format 和所有调用方存在编译耦合，不建议通过长期 transitional `Unknown` adapter 把错误字段延续多个阶段。推荐以下可审查切片：

### P0-7-F-2-B-0-A: BayFunction Removal Contract

- 冻结 V4 schema、V1/V2/V3 compatibility、损坏旧 Function 的处理规则。
- 冻结删除后的 Domain/Application API 形状。
- 列出所有 production/test/doc 修改矩阵。
- 仅设计，不实现。

### P0-7-F-2-B-0-B: Atomic Model and Format V4 Removal

- 删除 Domain interval/definition/restore 的 Function。
- 删除 BayTemplate Function 和从它派生 PT capability 的逻辑。
- 更新所有 compile-time callers：Domain Builder、Rendering manual creation adapter、Desktop ViewModel/Dialog/Demo。
- 同一切片提升 FormatVersion 4，增加 V3 → V4 migration，并让当前 DTO/mapper 不再依赖 Function。
- 同步迁移受影响测试，保持 solution 在提交点可编译。

该切片跨层但属于一个不可分割的 schema/API 变更。强行拆成“先 Domain、后 Persistence”会留下无法编译的中间状态；先注入 Unknown 则会制造本次明确拒绝的临时语义。

### P0-7-F-2-B-0-C: Legacy and Integration Verification

- 真实验证 V1/V2/V3 文件迁移到 V4。
- 验证所有 Domain/Layout Stable ID 保持。
- 验证 manual/template create、layout、command、selection、undo/redo 无回归。
- 静态确认 Rendering/Topology 无 Function 分支残留。

### P0-7-F-2-B-0-D: Architecture State Documentation

- 更新 `project-current-state.md` 和当前有效架构文档。
- 保留历史阶段文档作为决策轨迹，并明确新决策替代旧 Function 合同。

完成这些切片后再恢复 F-2-B。若希望更小提交，应先设计可编译且短命的迁移 scaffolding，但它不得把 Unknown 暴露为新业务默认值；当前没有足够收益支持该额外复杂度。

## 18. F-2-B Impact

F-2-B 目前存在架构阻断：Approved Built-in Templates 不能在现有必填 Function 合同上定义。

完成删除后，F-2-B 仍需专业确认以下事实：

- 首批普通 LoadSwitch 模板的间隔数量和显示名称；
- 首批 IntegratedFeeder 模板的间隔数量和显示名称；
- IntegratedFeeder 的明确 `GroundingStructureKind`；
- Template 展示顺序；
- 是否所有首批 Bay 都使用同一种 EquipmentConfiguration。

不再需要确认 Incoming/Outgoing/Tie 排列。Built-in Templates 可显式提供初始 BayIndex `1..N`，但后续实例 BayIndex 编辑属于独立功能。

PT、DTU、2-bay、JSON、Template Persistence 和 Template Editor 仍不属于首批内容。

## 19. Risks

- **Format compatibility risk:** V4 migration 若只修改 DTO 而未保留 V1/V2/V3 链，会破坏旧文件读取。
- **Corrupt V3 policy risk:** 必须明确旧 Function 缺失/未知在 V4 migration 中是否忽略，避免 mapper 与 migration 规则冲突。
- **API breadth risk:** 字段已出现在多层构造签名中，删除必须依赖完整编译和测试清单，不能只改 Domain。
- **PT capability risk:** 删除当前 PT 派生后，不得偷偷把 PT 当作 LoadSwitch；PT 应继续 unsupported，直到专用结构模型完成。
- **Documentation drift:** 历史设计仍包含旧 Function 决策，需要明确“历史记录”与“当前合同”。
- **BayIndex conflation risk:** 删除 Function 后不能把 BayIndex 或 Sequence 当成方向分类替代品。
- **Scope expansion risk:** 本变更不应扩展为潮流、运行方式、BayIndex 编辑或模板参数化系统。

## 20. Decision Summary

最终架构决策如下：

1. `BayFunction` 不应继续作为当前项目核心 Domain 字段。
2. Incoming/Outgoing/Tie 是当前系统不维护的运行分类，不应保存或推断。
3. PT 是结构能力，应在未来通过专用 Interval/Equipment/Domain 模型表达。
4. Metering/Reserve 当前无行为消费者，应移除，未来按真实结构或 metadata 用例重新建模。
5. 删除会改变 API 和 Persistence schema，但不会改变当前 topology、rendering、command、selection 或 Stable ID 业务语义。
6. 正式删除必须使用 FormatVersion 4，并兼容读取 V1/V2/V3；旧 Function 在迁移后不进入当前 Domain。
7. F-2-B 在删除和兼容验证完成前保持暂停。
8. 本审查不修改任何生产代码、测试、迁移或专业模板内容。
