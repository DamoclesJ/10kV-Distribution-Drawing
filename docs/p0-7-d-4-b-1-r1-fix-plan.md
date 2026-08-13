# P0-7-D-4-B-1-R1 Domain 边界修复计划

## 1. 修复背景与目标

P0-7-D-4-B-1 已建立 BayIndex 与 BayFunction 的基本 Domain 模型，但 Final Review 发现三个阻断问题：

1. 公开旧创建入口仍能以缺失 BayIndex、`Function = Unknown` 创建新 RingCabinet；
2. RingCabinetIntervalRestoreDefinition 的兼容构造器在 Domain 层自动执行 `BayIndex = Sequence`、`Function = Unknown`；
3. 当前 LoadSwitchInterval 和 IntegratedFeederInterval 可以与 `BayFunction.PT` 组合，形成没有 PT Domain 结构的伪 PT Bay。

本修复计划的目标是恢复以下架构边界：

- 新建 Domain 必须接收完整 BayIndex 与已确认 Function；
- Unknown 只允许经过 Persistence Migration 后进入 Restore；
- RestoreDefinition 只表达完整当前 Domain 恢复输入；
- Version 兼容和缺字段补值只属于 Persistence；
- PT 在专用 Domain 能力完成前不可映射为现有 IntervalKind。

本文只设计修复方案，不修改代码、测试、Persistence、DTO、FormatVersion 或 Migration。

## 2. 当前创建入口扫描

### 2.1 RingCabinetIntervalDefinition

当前文件：

- `src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinetIntervalDefinition.cs`

当前公开入口分为两组。

不完整兼容入口：

- `CreateLoadSwitch(SwitchState, SwitchState, string?)`；
- `CreateIntegratedFeeder(GroundingStructureKind, SwitchState, SwitchState, SwitchState, string?)`。

它们当前生成：

- `BayIndex = null`；
- `Function = Unknown`。

完整显式入口：

- `CreateLoadSwitch(int bayIndex, BayFunction function, ...)`；
- `CreateIntegratedFeeder(int bayIndex, BayFunction function, ...)`。

显式入口当前校验 BayIndex、枚举合法性和 Unknown，但尚未拒绝 PT。

### 2.2 RingCabinetDefinition

当前文件：

- `src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinetDefinition.cs`

当前 `Create` 接收 IntervalDefinitions，并使用：

- `definition.BayIndex ?? sequence`

形成有效 BayIndex。该逻辑使缺失 BayIndex 的 Definition 可以继续进入新建路径，同时没有在聚合定义边界拒绝 Unknown。

### 2.3 RingCabinet.Create

当前文件：

- `src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinet.cs`

当前 `Create` 按 IntervalDefinitions 集合顺序生成 Sequence，并在内部构造 Interval 时再次使用：

- `definition.BayIndex ?? sequence`；
- `definition.Function`。

因此完整显式入口正确，但公开旧入口仍可生成 Unknown 新对象。

### 2.4 RingCabinet 便利 Factory

当前公开便利入口：

- `CreateNormalLoadSwitchCabinet`；
- `CreatePrimarySecondaryIntegratedCabinetBase`。

它们只接收 intervalCount 和设备状态，不接收每个 Bay 的 Index/Function，并调用不完整兼容重载。它们无法满足当前冻结的业务合同。

### 2.5 当前生产调用点

当前调用不完整入口的生产代码包括：

- `src/DistributionDrawing.Rendering.Wpf/Interaction/Devices/RingCabinetCreationFactory.cs`；
- `src/DistributionDrawing.Desktop/Demo/RingCabinetCompositionDemoFactory.cs`。

当前调用旧 Restore 构造器的代码包括：

- `src/DistributionDrawing.Infrastructure/Persistence/ProjectDomainDto.cs`。

当前调用 Domain 便利 Factory 的测试包括：

- `tests/DistributionDrawing.Domain.Tests/TopologyBoundaryTests.cs`；
- `tests/DistributionDrawing.Domain.Tests/PoleAttachmentTests.cs`；
- `tests/DistributionDrawing.Domain.Tests/IntegratedFeederIntervalEvaluationTests.cs`。

这些调用点说明：删除 Domain 绕过入口后，R1 的 Domain-only 工作树不能保证全解决方案编译。不得为了维持暂时编译而继续在 Domain 中补 Unknown 或执行迁移。

## 3. 创建入口修复设计

### 3.1 应删除的入口

从 `RingCabinetIntervalDefinition` 删除两个不完整公开重载：

- 不含 BayIndex/Function 的 CreateLoadSwitch；
- 不含 BayIndex/Function 的 CreateIntegratedFeeder。

删除而不是标记 Obsolete 的原因：

- Obsolete 入口仍可创建违规 Domain；
- 为其抛运行时异常只会把编译期合同退化为运行时失败；
- 为其自动补 Unknown 继续违反业务规则；
- 为其猜测 Incoming/Outgoing/Reserve 会引入未经确认的专业规则。

### 3.2 应保留的入口

保留并强化两个完整显式入口：

- CreateLoadSwitch 必须接收 BayIndex、Function 和开关初始状态；
- CreateIntegratedFeeder 必须接收 BayIndex、Function、GroundingStructureKind 和开关初始状态。

它们必须保证：

- BayIndex 大于 0；
- Function 是已定义枚举；
- Function 不为 Unknown；
- Function 不为 PT；
- 原有 SwitchState 和 GroundingStructureKind 校验保持不变。

### 3.3 RingCabinetDefinition 修复

将 `RingCabinetIntervalDefinition.BayIndex` 从 nullable `int?` 改为非 nullable `int`。

`RingCabinetDefinition` 不再执行 `BayIndex ?? sequence`，而是直接校验：

- 每个 Definition 的 BayIndex 大于 0；
- 柜内 BayIndex 唯一；
- 每个 Function 是已定义值；
- 每个 Function 不为 Unknown；
- 当前支持的 IntervalKind 不接受 PT。

聚合定义层保留防御性校验，即使 IntervalDefinition Factory 已经检查，也不能依赖单一入口假设未来不会增加新 Factory。

### 3.4 RingCabinet.Create 修复

Create 继续只从集合顺序产生 Sequence，但必须直接读取：

- `definition.BayIndex`；
- `definition.Function`。

删除所有 `?? sequence` 回退。

Create 不负责：

- 生成默认 BayIndex；
- 猜测 Function；
- 接受 Unknown；
- 将 PT 映射为 LoadSwitch/IntegratedFeeder。

### 3.5 RingCabinet 便利 Factory 决策

现有两个只按 intervalCount 创建整柜的便利 Factory 无法提供每个 Bay 的完整专业元数据。

推荐删除或改为内部测试辅助，而不是继续作为生产公开 API。调用方应改用：

- 完整 RingCabinetIntervalDefinition 集合；
- RingCabinetDefinition.Create；
- RingCabinet.Create。

如果未来确实需要便利 Factory，应另行设计显式接收每个 BayIndex/Function 的输入模型，不得仅接收 intervalCount 后自动分配专业功能。

## 4. 创建调用方迁移边界

### 4.1 Domain-only R1 范围

R1 只修改 Domain 与 Domain.Tests 时，可以完成：

- 删除 Domain 绕过入口；
- 收紧 Definition/Create/Restore；
- 修正 Function/IntervalKind 校验；
- 更新 Domain.Tests 使用显式元数据。

### 4.2 全解决方案编译约束

删除旧公开入口后，以下禁止范围内的现有调用点将需要后续适配：

- Rendering.Wpf RingCabinetCreationFactory；
- Desktop Demo Factory；
- Infrastructure ProjectDomainMapper。

因此 R1 不能同时满足以下三个条件：

1. 只修改 Domain/Domain.Tests；
2. 删除所有 Domain 绕过与迁移逻辑；
3. 立即保持整个 Solution 编译通过。

推荐处理方式：

- R1 先形成未提交的 Domain 边界修复，并运行 Domain.Tests；
- P0-7-D-4-B-2 在 Persistence 层实现 Version 3 DTO/Migration，更新 Mapper 使用完整 RestoreDefinition；
- 创建入口适配切片更新 Rendering/Desktop 配置以显式提供 BayIndex/Function；
- 三个边界全部闭合后再形成可发布提交序列。

不推荐保留 Domain 兼容重载来换取暂时的 Solution 编译，因为这正是本次 Review 的根因。

## 5. RestoreDefinition 边界修复设计

### 5.1 当前问题

当前 `RingCabinetIntervalRestoreDefinition` 主构造器已经显式包含：

- Sequence；
- BayIndex；
- Function。

但额外的旧签名构造器允许省略 BayIndex/Function，并自动执行：

- `BayIndex = Sequence`；
- `Function = Unknown`。

该构造器同时承担缺字段补值、版本兼容和 Migration，违反 Domain/Persistence 边界。

### 5.2 应删除的逻辑

删除 `RingCabinetIntervalRestoreDefinition` 的 12 参数兼容构造器。

RestoreDefinition 最终只保留完整构造合同，调用者必须明确提供：

- Sequence；
- BayIndex；
- Function；
- 全部 Stable ID 与现有结构字段。

不得提供：

- nullable BayIndex；
- Function 默认参数；
- 从 Sequence 补 Index 的构造器；
- 自动补 Unknown 的构造器；
- 根据名称或设备结构推断字段的 Factory。

### 5.3 RingCabinet.Restore 职责

Restore 只负责：

- 使用传入 Stable ID 重建 Domain；
- 验证 Sequence 与集合顺序一致；
- 验证 BayIndex 大于 0且柜内唯一；
- 验证 Function 是已定义值；
- 允许规范枚举值 Unknown；
- 拒绝 PT 与当前 IntervalKind 组合；
- 验证现有 Node、Terminal、Switch 和 Assembly 不变量。

Restore 不负责：

- 判断文件版本；
- 补缺失字段；
- 把 Sequence 转成 BayIndex；
- 生成 Unknown；
- 修复 DTO；
- 写回项目文件。

### 5.4 Unknown 的边界

Restore 允许 Unknown 是必要的，因为 Version 2 Migration 会显式生成完整的 Version 3 数据：

- Index = 原 Sequence；
- Function = Unknown。

此时 Unknown 已是完整、明确的当前格式 Domain 输入，不是 Restore 的默认值。

## 6. BayFunction 与 IntervalKind 组合规则

### 6.1 两个概念继续分离

BayFunction 表示电气用途；IntervalKind 表示当前 Domain 已实现的设备结构。不得从其中一个推断另一个。

当前只冻结最小结构安全规则，不新增完整专业兼容矩阵。

### 6.2 新建路径最小矩阵

| IntervalKind | Incoming | Outgoing | Tie | Metering | Reserve | Unknown | PT |
| --- | --- | --- | --- | --- | --- | --- | --- |
| LoadSwitchInterval | 允许 | 允许 | 允许 | 允许保存元数据 | 允许保存元数据 | 禁止 | 禁止 |
| IntegratedFeederInterval | 允许 | 允许 | 允许 | 允许保存元数据 | 允许保存元数据 | 禁止 | 禁止 |

这里“允许保存元数据”不表示已经冻结 Metering/Reserve 的全部设备兼容、拓扑或运行规则，只表示在没有进一步专业约束前，Domain 不从设备结构反向推断或拒绝这两个长期功能值。

### 6.3 Restore 路径最小矩阵

Restore 与新建相比只放宽 Unknown：

| IntervalKind | 已定义非 PT Function | Unknown | PT | 未定义枚举值 |
| --- | --- | --- | --- | --- |
| LoadSwitchInterval | 允许 | 允许 | 禁止 | 禁止 |
| IntegratedFeederInterval | 允许 | 允许 | 禁止 | 禁止 |

### 6.4 PT 必须拒绝的原因

PT 已冻结为专用一次 Bay，需要：

- PT Domain；
- PT 专用一次设备结构；
- PT Terminal；
- PT Layout；
- Rendering；
- Persistence。

当前 LoadSwitchInterval 与 IntegratedFeederInterval 都不具备该结构。接受 `Function = PT` 会使业务功能与实际拓扑矛盾，并可能让 Inspector、工作票和后续分析误判。

未来 PT Domain 实现后，应新增专用 IntervalKind/Domain Factory，再扩展兼容矩阵；不能提前借用现有 IntervalKind。

### 6.5 校验位置

建议在 RingCabinetIntervalDefinition 中集中提供新建校验，在 RingCabinet Restore/Interval 构造边界提供恢复防御校验。

最小规则应明确区分：

- Creation：拒绝 Unknown、PT 和未定义值；
- Restore：允许 Unknown，拒绝 PT 和未定义值。

不要把该规则放入 Template、Rendering 或 UI 作为唯一事实源。

## 7. 测试调整计划

### 7.1 删除错误预期

从 `RingCabinetBayMetadataTests` 删除：

- `LegacyCreation_RemainsAvailableWithoutGuessingFunction`。

该测试将新建 Unknown 固化为受支持行为，与冻结规则冲突。

### 7.2 调整既有测试辅助入口

现有 Domain.Tests 中调用旧便利 Factory 的测试应改为使用显式 Definition：

- TopologyBoundaryTests；
- PoleAttachmentTests；
- IntegratedFeederIntervalEvaluationTests。

测试 Fixture 可以显式使用与测试目的无冲突的 Function，例如 Outgoing，但必须明确这是测试数据，不是生产默认规则。

### 7.3 新增创建校验测试

至少增加：

- RingCabinetDefinition 拒绝 Unknown；
- 不完整旧 CreateLoadSwitch 重载已不存在；
- 不完整旧 CreateIntegratedFeeder 重载已不存在；
- LoadSwitch + PT 被拒绝；
- IntegratedFeeder + PT 被拒绝；
- LoadSwitch + Incoming/Outgoing/Tie 正常创建；
- IntegratedFeeder + Incoming/Outgoing/Tie 正常创建；
- 非连续 BayIndex 继续允许；
- 重复和非正 BayIndex 继续拒绝。

“旧重载不存在”主要由编译期合同保证；不建议用脆弱的反射测试枚举所有方法签名，除非项目已有公共 API 形状测试模式。

### 7.4 新增 Restore 校验测试

至少增加：

- Restore 显式 Unknown 成功；
- Restore 非正 BayIndex 失败；
- Restore 重复 BayIndex 失败；
- Restore 未定义 Function 失败；
- Restore LoadSwitch + PT 失败；
- Restore IntegratedFeeder + PT 失败；
- Restore 保持全部 Stable ID。

### 7.5 “缺失 Bay Metadata”测试边界

删除兼容构造器后，Domain RestoreDefinition 在 C# 类型层面要求 BayIndex/Function，缺失参数应成为编译错误，而不是运行时 Domain 状态。

因此：

- Domain.Tests 不需要制造一个“不完整 RestoreDefinition”对象；
- Version 2 DTO 缺少字段的行为由后续 Persistence Migration 测试覆盖；
- Version 3 DTO 缺字段应由 B-2 当前格式严格读取测试覆盖；
- Domain 运行时继续测试 0 BayIndex 和未定义 Function，防止无效值绕过类型合同。

## 8. 预计修改文件

### 8.1 R1 必须修改

| 文件 | 修改目的 | 主要风险 | 必须性 |
| --- | --- | --- | --- |
| `RingCabinetIntervalDefinition.cs` | 删除不完整重载；BayIndex 非 nullable；拒绝 Unknown/PT | 现有调用点编译失败 | 必须 |
| `RingCabinetDefinition.cs` | 删除 Sequence 回退；聚合层拒绝 Unknown/PT；保持唯一性 | Definition 校验与 Factory 重复但属于防御边界 | 必须 |
| `RingCabinet.cs` | 删除 BayIndex 回退；Restore 拒绝 PT；调整/移除不完整便利 Factory | 影响现有测试与上层创建调用 | 必须 |
| `RingCabinetRestoreDefinition.cs` | 删除自动补值兼容构造器 | 当前 Persistence Mapper 需在 B-2 适配 | 必须 |
| `RingCabinetInterval.cs` | 增加或集中 Function/IntervalKind 最终防御校验 | 要区分新建与 Restore 的 Unknown 规则 | 视校验放置方案而定 |
| `RingCabinetBayMetadataTests.cs` | 删除错误兼容测试并补 PT/Restore 边界 | 当前无 dotnet 环境时无法本机执行 | 必须 |
| 现有 Domain.Tests 调用文件 | 改用完整 Definition | 测试数据 Function 必须显式 | 必须 |

### 8.2 R1 不修改

R1 不修改：

- Persistence；
- Project DTO；
- FormatVersion；
- Migration；
- Command；
- Selection；
- Rendering；
- Desktop UI；
- Template Builder。

### 8.3 后续必须适配但不属于 R1

以下文件不在 R1 修改范围，但在全解决方案恢复编译前必须更新：

- `ProjectDomainDto.cs`：B-2 Mapper 使用完整 RestoreDefinition；
- `RingCabinetCreationConfiguration.cs` 和 CreationFactory：显式提供 BayIndex/Function；
- Desktop RingCabinetCreation ViewModel/Dialog：收集并校验元数据；
- Desktop Demo Factory：显式测试数据；
- FormatVersion/Migration 文件：Version 2 → 3 补值。

## 9. 修复后的架构链路

### 9.1 旧工程加载

旧文件链路冻结为：

Version 2 文件
→ Persistence Migration
→ 完整 Version 3 DTO（Index = Sequence，Function = Unknown）
→ ProjectDomainMapper
→ 完整 RingCabinetIntervalRestoreDefinition
→ RingCabinet.Restore
→ Domain。

Domain 不知道源文件版本，也不执行缺字段补值。

### 9.2 新建链路

新建链路冻结为：

Template / UI / Builder
→ 完整 BayIndex + 已确认 BayFunction
→ RingCabinetIntervalDefinition
→ RingCabinetDefinition
→ RingCabinet.Create
→ Domain。

任何新建调用方缺少 Bay 元数据都应在编译期或 Definition 校验阶段失败。

### 9.3 PT 链路

当前 PT 请求：

Template PT Capability
→ Builder 能力检查
→ UnsupportedCapability。

不得进入 LoadSwitch/IntegratedFeeder Definition 或 Restore。

## 10. 推荐实施顺序

1. 删除 RingCabinetIntervalDefinition 的两个旧创建重载。
2. 将 Definition BayIndex 改为非 nullable，删除所有 `?? sequence`。
3. 在 RingCabinetDefinition 增加 Unknown/PT 防御校验。
4. 删除 RestoreDefinition 的旧兼容构造器。
5. 在 Restore 路径增加 PT 和未定义值校验，继续允许显式 Unknown。
6. 调整或移除缺少元数据的 RingCabinet 便利 Factory。
7. 更新 Domain.Tests 所有创建辅助入口为显式元数据。
8. 删除错误 LegacyCreation 测试，补充 PT、Restore 非正和非法 Function 测试。
9. 执行 `git diff --check`。
10. 环境允许时执行 Domain.Tests。
11. 明确记录全 Solution 在 B-2/创建入口适配前可能存在的预期编译缺口，不伪称 Build 通过。
12. 完成 Persistence B-2 与创建入口适配后，再执行完整 Solution build/test 和提交审查。

## 11. 验收标准

R1 Domain 边界满足：

- 所有公开新建 Definition 入口必须显式接收 BayIndex/Function；
- 新建 Unknown 无任何公开绕过入口；
- BayIndex 不再 nullable，不再从 Sequence 自动补充；
- RestoreDefinition 无版本兼容构造器；
- Restore 只接受完整元数据；
- Restore 显式 Unknown 合法；
- LoadSwitch/IntegratedFeeder 的 PT Function 在 Create 和 Restore 中均被拒绝；
- Sequence、BayIndex、Function 语义保持分离；
- Stable ID 创建/恢复逻辑不变；
- Domain.Tests 覆盖上述边界。

发布边界满足：

- Persistence Migration 完成缺字段补值；
- Mapper 使用完整 RestoreDefinition；
- Rendering/Desktop 创建入口提供完整元数据；
- 完整 Solution build/test 通过。

## 12. 最终结论

本次修复不能通过保留 Domain 兼容入口解决。正确方向是删除所有不完整新建/恢复重载，让 Bay 元数据成为 Domain API 的强制合同，并把 Version 2 缺字段补值完全移回 Persistence Migration。

PT 是当前唯一已经冻结的 Function/IntervalKind 强结构冲突，必须在 Create 和 Restore 中拒绝。其他已定义非 PT Function 暂只作为长期业务元数据保存，不在 R1 自行扩展完整专业兼容矩阵。

由于当前上层调用仍依赖旧签名，Domain-only R1 不应被描述为全解决方案可发布完成。它应作为边界修正切片，与后续 B-2 Persistence Migration 和创建入口适配共同闭合后再通过完整编译验收。
