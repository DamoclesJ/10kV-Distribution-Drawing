# P0-7-F-2-B-0-A BayFunction Removal and FormatVersion 4 Design

## 1. Context

P0-7-F-2-B-0 已冻结结论：`BayFunction` 不应继续作为当前项目核心 Domain 字段。当前 HEAD `9aa9611` 仍在 Domain、Application Template Runtime、Persistence V3、Rendering/Desktop 创建适配和测试中保存或传递该字段。

本设计给出一次可执行、可验证的删除方案。目标是在不改变普通环网柜结构、拓扑、布局和 Stable ID 的前提下：

- 从 Domain 和 Application Template Runtime 删除 Function；
- 把 Project Persistence 升级到 FormatVersion 4；
- 继续读取 Version 1、2、3 工程；
- 移除 Rendering/Desktop 中仅为 API 传递 Function 的代码；
- 为恢复 P0-7-F-2-B Approved Built-in Templates 建立明确进入条件。

本阶段只设计，不修改生产代码、测试或格式版本。

## 2. Frozen Business Decision

以下结论不再由实现阶段重新讨论：

- `Incoming`、`Outgoing`、`Tie` 会随电源方向、潮流方向或运行方式变化，不是当前软件持久化的稳定结构事实。
- `Reserve`、`Metering` 当前没有 Domain、Rendering、Command、Selection 或 Inspector 行为消费者。
- 不以 Direction、SourceSide、LoadSide、OperatingMode、FeederRole 或其他替代字段重新表达相同概念。
- 普通间隔的稳定事实是 `Sequence`、`BayIndex`、`IntervalKind` / EquipmentConfiguration、开关和接地结构、Terminal、ElectricalNode、SwitchAssembly 以及真实连接关系。
- PT 是结构型间隔，但必须与 `BayFunction.PT` 完全解耦，并在未来通过专用结构模型实现。
- F-2-B 在本设计的删除与兼容验证完成前保持暂停。

## 3. Target Model

删除后的普通环网柜模型具有以下形状：

```text
RingCabinet
  └─ RingCabinetInterval
       ├─ Sequence
       ├─ BayIndex
       ├─ DisplayName
       ├─ IntervalKind
       ├─ GroundingStructureKind (when applicable)
       ├─ SwitchDevices / SwitchAssembly
       ├─ ElectricalNode references
       └─ Terminal references
```

目标模型中不存在：

- `BayFunction` 类型；
- `RingCabinetInterval.Function`；
- Definition / RestoreDefinition 的 Function 参数；
- `BayTemplate.Function`；
- Project V4 interval DTO 的 `function` 字段；
- Desktop 手工创建 Function selector。

删除 Function 不改变普通 LoadSwitch 和 IntegratedFeeder interval 的结构合同。

## 4. Domain Removal

### 4.1 Delete BayFunction type

删除：

```text
src/DistributionDrawing.Domain/Devices/RingCabinets/BayFunction.cs
```

生产代码完成迁移后不得在 Domain 保留 obsolete、legacy 或 compatibility enum。旧文件兼容由 Infrastructure 的 JSON migration 负责，不能依赖 Domain 继续理解历史枚举。

### 4.2 RingCabinetInterval

从 internal constructor 删除：

- `BayFunction function` 参数；
- `Enum.IsDefined(function)` validation；
- `function == BayFunction.PT` validation；
- `Function = function` assignment。

删除公开只读属性：

```text
BayFunction Function
```

保留现有参数和验证：

- interval/parent IDs；
- `sequence > 0`；
- `bayIndex > 0`；
- display name；
- `IntervalKind`；
- GroundingStructureKind；
- intermediate/circuit/earth nodes；
- terminal IDs；
- switch collection and assembly structure。

### 4.3 RingCabinetIntervalDefinition

从 private constructor、`CreateLoadSwitch` 和 `CreateIntegratedFeeder` 删除 `BayFunction function` 参数与 `Function` 属性。

删除：

- enum-defined validation；
- Unknown/PT rejection；
- `EnsureCreatableBayMetadata(int, BayFunction)` 中所有 Function 分支。

BayIndex validation 继续保留。实现时可把 helper 缩小为只验证 BayIndex，或依赖 constructor 的同一验证；不应借机重构其他 factory 行为。

目标 factory 形状为：

```text
CreateLoadSwitch(
    bayIndex,
    initialLoadSwitchState,
    initialGroundSwitchState,
    displayName?)

CreateIntegratedFeeder(
    bayIndex,
    groundingStructureKind,
    initialIsolationSwitchState,
    initialCircuitBreakerState,
    initialGroundSwitchState,
    displayName?)
```

### 4.4 RingCabinetDefinition

删除 interval-definition validation 中：

- `Enum.IsDefined(definition.Function)`；
- Unknown/PT rejection。

继续验证：

- collection / element non-null；
- at least one interval；
- every BayIndex positive；
- BayIndex uniqueness；
- CabinetId and DisplayName。

相关错误消息从“valid bay metadata”收窄为真实的 BayIndex/definition invariant。

### 4.5 RingCabinetIntervalRestoreDefinition

从 positional record 删除：

```text
BayFunction Function
```

不改变其余 ID、Sequence、BayIndex、IntervalKind、GroundingStructureKind、node、terminal、assembly 和 switch restore fields。

### 4.6 RingCabinet Create / Restore / validation

删除：

- Restore loop 对 Function enum/PT 的 validation；
- Create 和 Restore 向 interval constructor 传递 Function；
- `ValidateStructure` 对 `interval.Function` 的 validation。

Create 继续按 `RingCabinetIntervalDefinition.IntervalKind` 选择 LoadSwitch 或 IntegratedFeeder 创建逻辑。Restore 继续按 persisted `IntervalKind` 恢复相应结构。

### 4.7 Domain invariants explicitly unchanged

本次删除不得改变：

- Sequence 必须与 aggregate 物理顺序一致；
- BayIndex 必须为正且柜内唯一；
- `IntervalKind` 的结构分派；
- switch initial/restored state；
- IntegratedFeeder 的 GroundingStructureKind；
- Terminal、ElectricalNode、Switch 和 SwitchAssembly topology；
- external terminal 对现有普通 interval 的合同；
- current pure-template interval-count rules；
- Aggregate stable identity and restore validation。

## 5. Application Removal

### 5.1 BayTemplate

构造器从：

```text
BayTemplate(index, function, equipmentConfiguration)
```

改为：

```text
BayTemplate(index, equipmentConfiguration)
```

删除：

- `Function` property；
- Function enum validation；
- Unknown rejection；
- Domain `BayFunction` using。

`Index` 继续代表模板提供的初始 BayIndex，并保持 positive validation。EquipmentConfiguration 继续 non-null。

### 5.2 RingCabinetTemplate

删除 `DeriveCapabilities` 中：

```text
bay.Function == BayFunction.PT
```

以及由此派生 `TemplateCapability.PTBay` 的逻辑。

`TemplateCapability.PTBay` 可暂时保留为未来 capability vocabulary，但当前没有任何合法 Template configuration 会派生它。现有 Layout Builder 对手工构造不一致 `DomainBuildResult` 的 PT defensive guard 可以保留。未来 PT configuration 进入模型时，再由真实结构派生该 capability。

EquipmentConfiguration 继续派生 LoadSwitch/IntegratedFeeder capabilities，SecondaryConfiguration 继续派生 DTU capability。

### 5.3 RingCabinetTemplateDomainBuilder

`CreateIntervalDefinition` 删除 `bay.Function` 参数传递：

- LoadSwitch 只传 BayIndex 和 switch states；
- IntegratedFeeder 只传 BayIndex、GroundingStructureKind 和 switch states。

Builder 不注入 Unknown、Outgoing 或任何假 Function，也不增加新的方向规则。

Builder 的 Domain mapping、failure model、Stable ID creation boundary 和 two-bay rule保持不变。

## 6. Template Library Impact

`RingCabinetTemplateLibrary` 只保存、排序并按 TemplateId 查询 immutable Template，本身不读取 BayFunction。因此原则上不需要生产逻辑修改。

其测试 fixture 需要改用新的 `BayTemplate(index, equipmentConfiguration)` API，但 Library 的以下合同不变：

- registration order；
- TemplateId equality and duplicate protection；
- immutable collection；
- same Template object identity；
- no capability interpretation。

删除完成后，未来 Built-in Template 可自然表达：

```text
LoadSwitch template:
  N bays
  initial BayIndex = 1..N
  LoadSwitchConfiguration per bay

IntegratedFeeder template:
  N bays
  initial BayIndex = 1..N
  IntegratedFeederConfiguration per bay
```

不再要求 Incoming、Outgoing、Tie、Reserve 或 Metering。

## 7. Rendering Impact

当前 `RingCabinetLayoutFactory`、symbols、geometry、scene projection 和 HitTest 不读取 Function。Rendering 只在手工创建适配中透传该字段。

删除范围：

- 从 `RingCabinetIntervalCreationConfiguration` positional record 删除 `BayFunction Function`；
- 从 `RingCabinetCreationFactory` 的两种 definition factory 调用删除 `configuration.Function`；
- 删除不再需要的 Domain BayFunction using。

不得修改：

- RingCabinetLayoutFactory；
- cabinet/interval width and spacing；
- switch symbol positions；
- Bounds / Anchor；
- DrawingSceneBuilder；
- HitTest identity；
- grounding rendering。

Template RuntimeLayout Builder 和 Full Build Coordinator 继续消费已构造的 Domain aggregate，不需要 Function 特殊逻辑。

## 8. Desktop Impact

### 8.1 Manual creation UI

当前 `RingCabinetCreationDialog.xaml` 包含“电气功能”列和 Function ComboBox。删除后：

- 移除该 header、column definition 和 ComboBox；
- 调整其余列的 Grid.Column；
- 不新增 Direction、Role、Purpose 或 Type Tag 替代输入。

`RingCabinetCreationViewModel` / interval row 删除：

- SupportedFunctions collection；
- nullable Function backing field/property；
- Function selection validation and error message；
- configuration projection中的 Function argument。

手工创建仍要求真实结构输入：BayIndex、DisplayName、IntervalKind，以及 IntegratedFeeder 所需的 GroundingStructureKind。

### 8.2 Demo and other callers

`RingCabinetCompositionDemoFactory` 删除 factory arguments 中的 Incoming/Outgoing/Tie。所有其他 Domain/Application factory callers 按新签名迁移，不引入 replacement value。

### 8.3 Inspector

当前 `PropertyProjector` 不展示 Function，Selection resolver 也不读取它。因此 Inspector 无需功能修改。删除后不新增 Function row，也不需要迁移 selection identity。

## 9. Persistence V4

### 9.1 Version constants

建议显式保留历史常量：

```text
Version1 = 1
Version2 = 2
Version3 = 3
Version4 = 4
CurrentVersion = Version4
```

`IsSupportedVersion` 继续接受 `1..CurrentVersion`。

当前实现只有 Version1、Version2 和 `CurrentVersion = 3`。新增 V4 时，V2 migration 分支不能再把 `version` 直接设为 `CurrentVersion`，必须显式设为 `Version3`，否则会跳过 V3 → V4。

### 9.2 V4 interval DTO

从 `ProjectRingCabinetIntervalDto` 删除：

```text
string Function
```

V4 继续保存：

- IntervalId / ParentCabinetId；
- Sequence / BayIndex；
- DisplayName / IntervalKind；
- GroundingStructureKind；
- Intermediate/Circuit/Earth nodes；
- ExternalTerminalId；
- SwitchAssemblyId；
- switch device and terminal identities/state。

### 9.3 Mapper

Save mapping 删除 `Encode(interval.Function)`，并删除 `Encode(BayFunction)`。

Restore mapping 不再解析 DTO Function，也不向 `RingCabinetIntervalRestoreDefinition` 传递 Function。真正有意义的结构字段继续 strict parse and validate。

### 9.4 V4 save and reload

V4 save 的 canonical JSON 不包含 `function`。V4 reload 恢复现有全部结构和 identity，不依赖 Template Library 或 TemplateId，也不重新调用任何 Builder。

## 10. Migration Chain

### 10.1 Required sequence

保持顺序迁移：

```text
V1 → V2 → V3 → V4
V2 → V3 → V4
V3 → V4
V4 → no migration
```

现有步骤历史合同不改写：

- V1 → V2 增加 Professional section；
- V2 → V3 设置 `bayIndex = sequence` 和 `function = "unknown"`；
- 新增 V3 → V4 删除 legacy `function`。

不增加 V1/V2 → V4 shortcut。顺序链更容易审计，并保持每个版本转换的单一历史责任。V2 → V3 临时生成 `unknown` 后，由下一步立即删除；这只是 migration pipeline 的中间 JSON，不进入 V4 DTO 或 Domain。

### 10.2 V3 → V4 algorithm

对每个 `domain.ringCabinets[*].intervals[*]`：

```text
interval.Remove("function")
```

如果 payload 没有 Domain，沿用当前 no-domain handling。若 Domain/cabinets/intervals 节点存在但 JSON 结构错误，继续使用当前 RequireObject/RequireArray 风格明确失败。

迁移不得读取、parse、normalize 或解释 Function 值，也不得生成任何 ID。

### 10.3 Physical removal versus unmapped-property skip

选择方案 A：V3 → V4 在 JsonObject 中物理 `Remove("function")`。

理由：

- migration output 明确成为 canonical V4 shape；
- tests 可直接观察旧字段已被淘汰；
- 避免“V4 migration 完成但 payload 仍携带 V3 字段”的含混状态；
- 后续维护者不必依赖 serializer 的全局 unmapped-member policy 理解版本迁移；
- 旧工程下一次保存不会 round-trip legacy Function。

当前 `ProjectFileContainer` 使用 `JsonUnmappedMemberHandling.Skip`。这仍用于 V4 source payload 的一般未知字段兼容，但不能替代显式的 V3 → V4 schema migration。

## 11. V3 Compatibility

### 11.1 Legacy values

旧 V3 文件可能含有：

```text
unknown
incoming
outgoing
tie
pt
metering
reserve
```

也可能由于历史损坏含未知字符串、null、数字、对象，或完全缺失 `function`。

### 11.2 Decision: discard without validation

V3 → V4 对 Function 采用“接受并丢弃”策略：

- 不先执行旧 V3 enum strict validation；
- 不要求字段存在；
- 不要求值为 string；
- 无论值为何都删除，不进入 current DTO/Domain。

这是有意识的兼容性放宽。既然 Function 已确认不是业务事实，让无意义旧字段阻断其余合法且有价值的工程数据没有收益。

该放宽只适用于被删除的 Function。它不允许绕过 BayIndex、IntervalKind、topology、Stable ID 或其他 V3/V4 结构验证。

### 11.3 V3 historical contract remains true

文档和测试必须继续承认：

- Version 3 的 schema 曾包含 `function`；
- V2 → V3 曾写入 `function = "unknown"`；
- 在 CurrentVersion 仍为 3 时，缺失/非法 Function 曾被 current mapper 判为损坏。

V4 migration 改变的是“如何兼容读取历史 V3”，不是改写 V3 的历史定义。

## 12. V4 Strictness

### 12.1 Fields that remain strict

V4 必须继续拒绝真正有意义的数据错误，包括但不限于：

- BayIndex 小于 1；
- cabinet 内 duplicate BayIndex；
- invalid Sequence / owner relationship；
- unsupported IntervalKind；
- invalid GroundingStructureKind；
- switch/assembly kind or membership mismatch；
- empty/duplicate Stable IDs；
- invalid Terminal/ElectricalNode ownership；
- malformed topology；
- required switch collections or structure fields missing。

### 12.2 Function behavior in V4

- V4 缺少 `function`：正常成功。
- V4 canonical save：绝不输出 `function`。
- 外部 V4 payload 额外携带 legacy `function`：按当前 `UnmappedMemberHandling.Skip` 忽略，不进入 Domain；后续保存不回写。

V4 对 extra Function 的容忍是 unknown-property policy，不代表该字段重新成为 V4 schema。应增加明确测试，防止未来 mapper 意外重新读取它。

## 13. Stable ID Contract

BayFunction 删除和任何版本迁移不得生成或替换 Stable ID。

必须保持：

- CabinetId；
- MainBusNodeId；
- IntervalId；
- SwitchId；
- TerminalId；
- ElectricalNodeId；
- SwitchAssemblyId；
- existing layout identity references。

V3 → V4 只删除 JSON property。V1/V2 的现有 ID compatibility 行为继续保留。V4 save → reload 通过现有 RestoreDefinition 使用持久化 ID，不调用 `RingCabinet.Create`、Template Domain Builder 或 Layout Builder 重建 identity。

验证必须从恢复后的实际 aggregate 重新读取并逐项比较，而不是只比较 CabinetId。

## 14. Test Migration

### 14.1 Domain.Tests

修改范围：

- 所有 Definition / helper fixture 删除 Function argument；
- 删除 `RingCabinetBayMetadataTests` 中只验证 Function 保存、Unknown/PT/undefined 的测试；
- 保留并强化 BayIndex positive、duplicate、Sequence、Create/Restore、IntervalKind 和 topology tests；
- 必要时把该测试文件/类型重命名为 BayIndex 或 interval metadata tests；
- PT rejection 不再通过 Function 测试，未来由专用 PT model 测试。

### 14.2 Application.Tests

修改范围：

- `BayTemplateTests` 改为验证 Index 和 EquipmentConfiguration，不再断言 Function；
- Template、Domain Builder、Library fixtures 使用新 constructor；
- 删除 Function mapping assertions；
- 删除通过 `BayFunction.PT` 派生/拒绝 PT capability 的 Template tests；
- 保留 EquipmentConfiguration capability、BayIndex order/uniqueness、LoadSwitch、IntegratedFeeder、DTU 和 two-bay failure tests；
- 保留 Library immutability、lookup、object identity 和 non-sequential BayIndex tests。

### 14.3 Infrastructure.Tests

历史与 current tests 分开表达：

- 保留测试证明 V2 → V3 中间步骤会产生 `function = "unknown"`；
- 新增 V3 → V4 删除 Function 的 JsonObject migration test；
- 新增 V1/V2 完整链最终输出 V4 且不含 Function；
- 将 round-trip 测试命名和断言更新为 V4，不再比较 Function；
- 新增 V4 canonical save 不写 Function；
- 新增 V4 缺 Function 成功；
- 新增 V4 extra legacy Function 被忽略且 reload/save 后消失；
- 将旧“corrupted V3 missing/invalid function fails”测试改为 V3 missing/unknown/non-string Function 均可迁移，其他结构 corruption 仍失败；
- 对 V1/V2/V3 → V4 和 V4 round-trip 逐项验证全部 Stable ID。

### 14.4 Rendering.Wpf.Tests

- Template/Layout/Coordinator fixtures 删除 Function arguments；
- 保留 layout identity、sequence/non-sequential BayIndex、position、geometry 和 Stable ID assertions；
- direct Layout Builder 的 inconsistent `TemplateCapability.PTBay` defensive guard 可保留，因为它测试 capability boundary，不依赖 BayFunction；
- 不新增 PT layout success test。

### 14.5 Desktop.Tests

- Template creation fixtures 删除 Function；
- manual creation ViewModel tests 删除 Function selector/validation assertions；
- 增加配置可在没有 Function 输入时成功创建的测试；
- 保留 CommandStack、SelectionTransition、Undo/Redo、Dirty、Scene、Resolver、Inspector 和 Stable ID integration assertions。

## 15. Implementation Atomicity

B-0-B 应作为一个完整编译闭环实施并以单个可构建提交交付，范围包括：

- Domain type/API removal；
- Application Template Runtime and Builder migration；
- Infrastructure FormatVersion 4、DTO、mapper and migration；
- Rendering/Desktop compile-time callers；
- 全部受影响测试。

原因是 Domain constructor/record signatures、Persistence mapper 和上层 callers 直接编译耦合。先删除 Domain Function 会立即使 Infrastructure、Rendering、Desktop 和 tests 无法编译；先注入 Unknown compatibility adapter 又会延续已否定的语义。

实现过程内部可以按层分步编辑和局部检查，但最终 review/commit 不应暴露不可编译中间状态。若必须拆 commit，唯一可接受方式是先引入短命、明确且不对外形成新业务默认的 compatibility scaffolding，并在后续 commit 立即删除；当前不建议承担这项额外复杂度。

## 16. PT Future Boundary

PT 不在 B-0-B 实现范围。删除 `BayFunction.PT` 后，当前普通 interval 不得借用 LoadSwitch/IntegratedFeeder 或 External Cable Terminal 模拟 PT。

未来 PT 模型必须满足：

- PT 是 RingCabinet 内部的结构型 interval；
- 通过 dedicated IntervalKind、PT EquipmentConfiguration、PT Domain model 或等价结构表达；
- PT terminal 是 dedicated internal PT terminal，不是普通 External Cable Terminal；
- topology 表达 `PT interval → dedicated PT terminal → PT equipment`；
- 内部连接属于 RingCabinet aggregate topology；
- 不创建虚拟 Cable、CableTermination 或外部 Cable Connection。

已知一次结构至少需要独立支持：

```text
A: main bus → isolation switch → PT
B: main bus → circuit breaker → PT
```

两者均具有接地刀闸结构。具体 SwitchAssembly、terminal/node ownership、state rules、layout 和 persistence 必须在未来 PT 专项设计中冻结。

## 17. BayIndex Future Editing Boundary

Built-in Template 可显式给出初始 `BayIndex = 1..N`。该值在创建时复制到 Domain instance，Template 不对实例建立运行时约束。

未来 BayIndex 编辑必须：

- 作用于画布中的 Domain instance；
- 保持 BayIndex positive and cabinet-unique；
- 不改变 interval Sequence；
- 通过 CommandStack 支持 Undo/Redo/Dirty；
- 不重新 Build Cabinet/Layout 或重新生成 Stable ID。

B-0-A/B-0-B 不设计或实现该 edit command。

## 18. F-2-B Entry Criteria

只有同时满足以下条件，才允许开始 Approved Built-in Templates：

- `BayFunction.cs` 已删除且生产代码无 `BayFunction` reference；
- `RingCabinetInterval`、Definition、RestoreDefinition 无 Function；
- `BayTemplate` 和 Domain Builder 无 Function；
- Template Library tests 已适配且 Library boundary 不变；
- Project `CurrentVersion = 4`；
- V4 DTO/save 不含 Function；
- V1、V2、V3 文件可迁移并读取；
- V3 legacy/unknown Function 已按本设计丢弃；
- V4 strict structural validation 仍通过测试；
- V3 → V4 和 V4 round-trip 保持全部 Stable ID；
- Rendering/Desktop 不再传递或要求 Function；
- Layout、Scene、Command、Selection、Undo/Redo、Dirty integration 无回归；
- solution build 和相关 tests 在 .NET SDK 10.0.400 环境实际通过；
- `git diff --check` 通过且 working tree clean。

## 19. Risks

- **Migration-chain bug:** 当前 V2 branch 把 version 设为 `CurrentVersion`；若只把 CurrentVersion 改成 4，会错误跳过 V3 → V4。
- **Historical-contract drift:** 删除 current DTO 字段不能抹去 V3 曾要求 Function 的事实。
- **Over-relaxed migration:** 只能放宽已删除 Function；其他结构字段必须继续 strict。
- **Serializer-policy ambiguity:** V3 migration 应物理删除字段，不能只依赖 unknown-property skip。
- **Atomicity breadth:** 变更横跨多层；遗漏一个 factory/test fixture 会造成编译失败。
- **PT regression:** 删除 PT capability derivation 后，不得把 PT 模板默认为普通 interval；PT 继续不支持。
- **Stable ID regression:** migration/restore 测试必须逐项验证，不得通过重新 Build 生成替代对象。
- **UI grid regression:** 删除 XAML column 时需同步 headers、item columns 和窗口布局。
- **Environment risk:** 当前主要环境可能没有 dotnet，静态检查不能替代真实编译。

## 20. Implementation Plan

### Step 1: Domain API removal

- 删除 BayFunction type、properties、parameters and validation。
- 保持所有结构和 identity invariants。
- 迁移 Domain fixtures/tests。

### Step 2: Application runtime and builder migration

- 简化 BayTemplate。
- 删除 Function mapping and PT derivation。
- 迁移 Application tests and Library fixtures。

### Step 3: Persistence V4

- 增加 Version3/Version4 constants and set CurrentVersion to Version4。
- 修正 sequential migration version assignment。
- 实现 V3 → V4 physical removal。
- 删除 current DTO/mapper Function。
- 迁移 round-trip、corruption、legacy and stable-ID tests。

### Step 4: Rendering/Desktop callers

- 删除 creation configuration passthrough。
- 删除 Desktop selector、validation、mapping and demo values。
- 保持 layout/scene/command/selection behavior。

### Step 5: Repository-wide verification

- `rg BayFunction`：生产代码应为零；历史 docs/tests 可按迁移目的保留明确字符串，但不能引用已删除类型。
- `rg '"function"'`：只允许出现在 V2→V3、V3→V4 和 legacy compatibility tests/docs；V4 DTO/save 不得出现。
- `git diff --check` and XML/XAML checks。
- 检查 dependency direction and scope。

### Step 6: Build and tests on required SDK

在具有 .NET SDK 10.0.400 的环境至少执行：

```text
dotnet build src/DistributionDrawing.sln
dotnet test tests/DistributionDrawing.Domain.Tests/DistributionDrawing.Domain.Tests.csproj
dotnet test tests/DistributionDrawing.Application.Tests/DistributionDrawing.Application.Tests.csproj
dotnet test tests/DistributionDrawing.Infrastructure.Tests/DistributionDrawing.Infrastructure.Tests.csproj
dotnet test tests/DistributionDrawing.Rendering.Wpf.Tests/DistributionDrawing.Rendering.Wpf.Tests.csproj
dotnet test tests/DistributionDrawing.Desktop.Tests/DistributionDrawing.Desktop.Tests.csproj
```

若当前环境没有 dotnet，实施报告必须明确“未执行”，不能声称通过；B-0-B 最终关闭前仍需在指定 SDK 环境完成验证。

## 21. Decision Summary

1. 删除 `BayFunction.cs`，不在 Domain 保留 compatibility enum。
2. Domain interval、definition、restore 和 validation 完全移除 Function。
3. `BayTemplate` 只保存 Index 和 EquipmentConfiguration；Builder 不注入替代值。
4. `ProjectFileFormat.CurrentVersion` 升级到 4，并显式保留 Version3/Version4 constants。
5. 保持 V1 → V2 → V3 → V4 顺序链，不增加 shortcut。
6. V3 → V4 物理删除 `function`，不验证旧值；旧值不进入 current DTO/Domain。
7. V4 完全不保存或要求 Function；额外 legacy property 按现有 unmapped-member policy 忽略并在下一次保存消失。
8. V4 继续严格验证 BayIndex、IntervalKind、设备结构、topology 和 Stable ID。
9. 所有迁移和 round-trip 必须保持 Cabinet、Interval、Switch、Terminal、ElectricalNode、SwitchAssembly 和 MainBus IDs。
10. B-0-B 作为一个完整编译闭环实施；在实际 build/test 通过前不得进入 F-2-B。
11. PT 留给未来 dedicated structural model，不使用 External Cable 模拟内部 PT connection。
12. BayIndex `1..N` 是 Built-in Template 的实例初始值，未来可独立编辑且不改变 Sequence。
