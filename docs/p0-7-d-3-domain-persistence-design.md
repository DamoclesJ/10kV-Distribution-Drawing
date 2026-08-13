# P0-7-D-3 Domain/Persistence 最小补充设计

## 1. 文档目的与边界

本文设计 Template Builder 进入生产实现前，现有 RingCabinet Domain 与 Persistence 所需的最小补充。

本文承接以下已冻结决策：

- Sequence 表示物理排列顺序；
- Bay Index 表示稳定的现场业务间隔编号；
- BayFunction 表示与设备结构分离的电气功能；
- 新建对象不允许 `Unknown`；
- 旧工程迁移允许 `Unknown`；
- CabinetType 与 EquipmentConfiguration 不作为重复事实进入 Domain；
- 第一版不持久化 TemplateReference。

本文只描述后续实现边界，不修改 Domain、DTO、FormatVersion、Migration、Command、Selection、Rendering 或 UI。

## 2. 当前代码事实

### 2.1 RingCabinet 聚合

当前 `RingCabinet` 是完整聚合根，已经包含：

- 有序的 `RingCabinetInterval`；
- MainBus、Circuit、Earth、Intermediate 等 ElectricalNode；
- 内外部 Terminal；
- SwitchDevice；
- SwitchAssembly；
- IntervalKind；
- IntegratedFeeder 的 GroundingStructureKind。

`RingCabinet.Create` 从 `RingCabinetDefinition.IntervalDefinitions` 的集合顺序生成 Interval，并将集合位置转换为从 1 开始的 Sequence。聚合内部的 Stable ID 在首次创建时生成。

`RingCabinet.Restore` 通过 `RingCabinetRestoreDefinition` 恢复全部 Stable ID，并要求每个 Interval 的 Sequence 等于其集合位置加 1。

### 2.2 RingCabinetInterval 当前字段

当前 `RingCabinetInterval` 已表达：

- `IntervalId`；
- `ParentCabinetId`；
- `Sequence`；
- `DisplayName`；
- `IntervalKind`；
- Switch 集合与 SwitchAssembly；
- GroundingStructureKind；
- 内部 Node ID 与 ExternalTerminalId。

当前能力判断如下：

| 业务事实 | 当前是否支持 | 当前表达方式 |
| --- | --- | --- |
| 间隔物理顺序 | 是 | `RingCabinetInterval.Sequence` |
| 间隔业务编号 | 否 | 没有独立字段；DisplayName 不能替代 |
| 间隔电气功能 | 否 | 没有 BayFunction；IntervalKind 只表示设备结构 |

因此，Sequence 不需要新增；需要保持其既有语义，并新增 Bay Index 与 BayFunction。

### 2.3 当前 Persistence

当前 `ProjectRingCabinetIntervalDto` 已保存 Sequence、DisplayName、IntervalKind、GroundingStructureKind、拓扑 ID、SwitchAssemblyId 和 Switch 集合，但不保存 Bay Index 或 BayFunction。

`ProjectDomainMapper` 负责：

- 从 Domain 投影 RingCabinet DTO；
- 将 DTO 转换为 `RingCabinetIntervalRestoreDefinition`；
- 调用 `RingCabinet.Restore`；
- 校验恢复后的 Node、Terminal 和 Switch 数据。

当前 `ProjectFileFormat.CurrentVersion` 为 2，容器只明确支持 PreviousVersion 1 与 CurrentVersion 2。Version 1 到 Version 2 的兼容逻辑在读取容器时补充 Professional section，并将有效 Manifest 提升为当前版本。

新增长期 Domain 事实后，不能继续沿用 FormatVersion 2。

## 3. RingCabinetInterval Domain 补充

### 3.1 推荐字段

`RingCabinetInterval` 最小需要保持或增加以下三个公开只读属性：

| 属性 | 状态 | 职责 |
| --- | --- | --- |
| `Sequence` | 已存在，保持 | 柜内物理排列顺序；供 Layout、Rendering 和遍历使用 |
| `BayIndex` | 新增 | 现场业务间隔编号；供 Inspector、工作票、台账和设备识别使用 |
| `Function` | 新增 | Bay 的长期电气功能事实，类型为 Domain `BayFunction` |

Domain 属性推荐命名为 `BayIndex`，以避免与集合下标、`System.Index` 和物理 Sequence 混淆。Template 侧仍可使用已冻结的 `BayTemplate.Index`；映射关系为：

| Template | Domain |
| --- | --- |
| Bays 集合位置 | `RingCabinetInterval.Sequence` |
| `BayTemplate.Index` | `RingCabinetInterval.BayIndex` |
| `BayTemplate.Function` | `RingCabinetInterval.Function` |

如果实现阶段决定 Domain 属性也命名为 `Index`，业务语义不得改变；本文推荐 `BayIndex` 只是为了代码可读性。

### 3.2 Sequence

Sequence 继续表示物理顺序：

- 从 1 开始；
- 同一 RingCabinet 内连续；
- 同一 RingCabinet 内唯一；
- 只在所属 RingCabinet 内有意义；
- 由 Intervals 集合顺序确定；
- 不代表现场编号；
- 不从 BayIndex 推导。

现有 `RingCabinetInterval` 已拒绝小于 1 的 Sequence，`RingCabinet.Restore` 已校验 Sequence 与集合位置一致。这些约束应保留。

后续若实现 Bay 重排，Sequence 可以随物理位置变化；BayIndex 不应随之变化。本阶段不设计重排 API。

### 3.3 BayIndex

BayIndex 是 RingCabinetInterval 的长期业务事实：

- 必须大于 0；
- 同一个 RingCabinet 内必须唯一；
- 允许缺号；
- 不要求连续；
- 不要求等于 Sequence；
- 不因排序变化而自动改变；
- 不从 DisplayName、IntervalKind 或设备结构推断。

单个 Interval 构造边界负责校验 `BayIndex > 0`；RingCabinet 聚合边界负责校验柜内唯一性。

### 3.4 Function

Function 是 RingCabinetInterval 的长期电气功能事实：

- 必须是已定义的 Domain `BayFunction` 枚举值；
- 与 IntervalKind、Switch 组合和 GroundingStructureKind 分离；
- 不从 DisplayName 或设备结构推断；
- 新建路径不允许 `Unknown`；
- 旧数据恢复路径允许 `Unknown`。

Domain 对象始终具有 Function，不采用 nullable。`Unknown` 已承担旧数据“事实尚未确认”的明确语义，nullable 会引入第二种未知状态。

## 4. Domain 不变量与校验层级

### 4.1 RingCabinetInterval 层级

Interval 自身应保证：

- Sequence 大于等于 1；
- BayIndex 大于 0；
- Function 是已定义的 BayFunction；
- 现有 IntervalKind、GroundingStructureKind、Node、Terminal、Switch 和 Assembly 不变量继续成立。

Interval 自身不能判断 BayIndex 是否与兄弟 Interval 冲突，因此唯一性不放在单个 Interval 构造器中。

### 4.2 RingCabinetDefinition 层级

新建定义必须在任何内部 Stable ID 生成前完成：

- 至少一个 IntervalDefinition；
- 每个 IntervalDefinition 的 BayIndex 大于 0；
- BayIndex 在定义集合内唯一；
- 每个 Function 是已定义值；
- 每个 Function 不为 `Unknown`。

Sequence 不作为 `RingCabinetIntervalDefinition` 的独立输入。它继续由 IntervalDefinitions 的集合顺序生成，避免调用方同时提供 Sequence 和集合顺序造成冲突。

`RingCabinetIntervalDefinition` 需要增加 BayIndex 与 Function，现有 `CreateLoadSwitch` 和 `CreateIntegratedFeeder` 工厂需要显式接收这两个业务事实。

不得根据 IntervalKind 给 Function 设置默认值。LoadSwitchInterval 或 IntegratedFeederInterval 都不能证明其一定是 Incoming、Outgoing、Tie、Metering 或 Reserve。

### 4.3 RingCabinet Restore 层级

`RingCabinetIntervalRestoreDefinition` 需要增加 BayIndex 与 Function，并继续显式承载 Sequence 和全部 Stable ID。

Restore 必须验证：

- Sequence 与集合位置一致；
- BayIndex 大于 0；
- BayIndex 在柜内唯一；
- Function 是已定义值；
- `Unknown` 可以存在；
- 原有拓扑与 Stable ID 不变量继续成立。

Restore 允许 Unknown 是旧工程兼容策略，不代表普通新建 API 可以创建 Unknown。

### 4.4 RingCabinet 聚合防御性校验

`RingCabinet.ValidateStructure` 或等价聚合校验应最终确认：

- Sequence 为 `1..N`；
- BayIndex 均为正整数且柜内唯一；
- Function 均为已定义值；
- ParentCabinetId、Node、Terminal、Switch 和 Assembly 关系完整。

即使 Definition 和 Restore 输入已经预校验，聚合仍应保留最终一致性检查，防止未来新增内部创建路径绕过规则。

## 5. BayFunction 枚举归属

### 5.1 方案比较

如果 BayFunction 只存在 Template 层，生成后的 RingCabinetInterval 会丢失 Incoming、Outgoing、Tie、PT、Metering 或 Reserve 等长期业务事实，Inspector、工作票和后续分析只能依赖 TemplateReference 或名称猜测。

如果 BayFunction 属于 Domain，生成后的聚合可以脱离 Template 独立存在，并由现有 Persistence、Selection 和 Inspector 消费。

### 5.2 冻结结论

BayFunction 应定义在 RingCabinet Domain 命名空间，与 IntervalKind 并列但职责不同。

第一版枚举值：

- `Unknown`；
- `Incoming`；
- `Outgoing`；
- `Tie`；
- `PT`；
- `Metering`；
- `Reserve`。

其中 Unknown 只用于旧工程迁移和兼容恢复；新建 Definition 必须拒绝 Unknown。

未来扩展枚举时必须：

- 只追加经专业确认的语义；
- 同步 DTO 编码/解析；
- 保持未知序列化值加载失败，而不是静默映射为 Unknown；
- 不把厂家型号、设备类型或自由文本加入 BayFunction；
- 根据是否新增持久化语义评估 FormatVersion 和兼容策略。

`BusSection`、`Auxiliary` 和厂家特定用途不属于第一版。

## 6. Template 与 Domain 边界

Template 继续只负责描述：

- CabinetType；
- 有序 Bays；
- Bay Index；
- BayFunction；
- EquipmentConfiguration；
- LayoutRule；
- RequiredCapabilities。

Builder 创建完成后：

- RingCabinetInterval 保存 Sequence、BayIndex 和 Function；
- EquipmentConfiguration 映射为实际 IntervalKind、GroundingStructureKind、Switch、Terminal、ElectricalNode 和 SwitchAssembly；
- RuntimeLayout 保存实例几何；
- Domain 与 RuntimeLayout 不引用 Template 对象。

第一版不持久化 TemplateId、TemplateReference、TemplateVersion 或原始 Template 参数。这是合理的最小边界，因为生成后的 Domain 已无损保存长期业务事实，实际设备结构和 Layout 也已成为权威实例状态。

该选择意味着第一版不提供“模板更新后自动同步已有柜体”。如果未来需要模板来源审计或实例升级，必须另行设计，不得把 TemplateReference 当作当前 Domain 事实的替代品。

## 7. 创建 API 影响

### 7.1 Definition API

为了保证新建对象不出现 Unknown，以下创建输入必须显式承载 BayIndex 与 Function：

- `RingCabinetIntervalDefinition`；
- `RingCabinetDefinition` 的 IntervalDefinitions；
- 未来 Template Runtime Model 到 Definition 的映射。

Sequence 仍从集合顺序产生，不作为重复输入。

### 7.2 现有便利 Factory

当前按 intervalCount 创建整柜的便利 Factory 无法仅凭数量或 IntervalKind 确定每个 Bay 的 Function。

后续实现不得：

- 将全部 Bay 默认设为 Outgoing；
- 从第一个或最后一个位置猜 Incoming/Tie；
- 将无法确认的 Function 设为 Unknown 后走普通新建路径。

这些便利 Factory 应改为接收明确的 IntervalDefinition 集合或明确的 BayIndex/Function 输入；具体公共 API 兼容方案在实现 Review 中确定。

### 7.3 现有 Desktop 创建入口

当前 `RingCabinetCreationConfiguration` 只有 DisplayName、IntervalKind 和 GroundingStructureKind，无法满足新建 Domain 对 Function 的显式要求。

因此 Domain/Persistence 补充落地时，现有 Desktop 创建配置也必须在同一可发布切片内提供：

- 每行 BayIndex；
- 每行 BayFunction；
- 正整数、柜内唯一和 Unknown 禁止的友好前置校验。

普通 UI 可以默认产生连续 BayIndex，但用户必须能覆盖。Function 不得根据名称、位置或 IntervalKind 自动猜测。

Inspector 仍只读；创建 Dialog 的输入不等于既有对象属性编辑能力。

## 8. Persistence DTO 设计

### 8.1 DTO 修改范围

`ProjectRingCabinetIntervalDto` 需要在现有字段基础上增加：

| DTO 字段 | 类型 | 来源 |
| --- | --- | --- |
| `Index` | `int` | `RingCabinetInterval.BayIndex` |
| `Function` | `string` | Domain BayFunction 的规范编码 |

Sequence 已存在，无需新增或重命名。

DTO 继续只保存数据，不包含：

- 业务校验方法；
- Domain 行为；
- TemplateReference；
- CabinetType；
- EquipmentConfiguration；
- Layout 规则或具体 Rendering 状态。

Layout DTO 不需要修改。BayIndex 与 Function 是 Domain 事实，不是 Layout 几何。

### 8.2 Domain Mapper

保存方向需要：

- 写出 Sequence；
- 写出 BayIndex 为 DTO Index；
- 使用现有枚举编码风格写出 Function；
- 继续写出全部现有 Interval、拓扑和 Switch 数据。

恢复方向需要：

- 只接收已迁移为当前格式的 DTO；
- 解析 Function 的规范字符串；
- 将 Sequence、Index 和 Function 传入 RestoreDefinition；
- 由 RingCabinet.Restore 执行最终 Domain 校验。

Domain Mapper 不应根据缺失字段自行判断旧版本，也不应从 DisplayName 或结构推断值。版本迁移必须在 DTO 进入 Domain Mapper 前完成。

### 8.3 当前格式严格性

FormatVersion 3 的当前 DTO 中，Index 与 Function 在语义上必须存在。

为了兼容 Version 2 JSON 的反序列化，实现时可以采用版本专用 DTO，或在读取层使用 nullable migration input；但完成迁移后的当前 DTO 必须具有明确的正整数 Index 和非空 Function。

不得让 Version 3 缺少字段时使用语言默认值 `0` 或 `null` 并继续恢复。当前格式缺字段应视为损坏文件并明确失败。

## 9. FormatVersion 设计

### 9.1 版本升级结论

新增 BayIndex 与 BayFunction 改变了 Project Domain 的持久化合同，需要将 `ProjectFileFormat.CurrentVersion` 从 2 升级到 3。

理由：

- 两个字段是新的长期业务事实；
- Version 2 文件没有这些字段；
- 缺失字段需要确定性迁移，而不是普通默认值；
- 保存后的 Version 3 必须能与 Version 2 明确区分。

### 9.2 迁移链

当前代码只使用单一 PreviousVersion 常量表达 Version 1 到 Version 2。引入 Version 3 后，安全设计应支持显式迁移链：

1. Version 1 → Version 2：沿用现有 Professional section 补充；
2. Version 2 → Version 3：为每个 RingCabinetInterval 补充 Index 与 Function；
3. Version 1 → Version 3：依次执行上述两步；
4. Version 3：严格按当前合同读取，不执行旧格式默认补充。

不能仅把 `PreviousVersion` 改为 2 后意外失去 Version 1 的加载能力。实现阶段应把受支持版本和逐步迁移明确化，而不是继续依赖“唯一上一版本”的假设。

### 9.3 Version 2 → Version 3 规则

对每个旧 RingCabinetInterval：

- `Index = Sequence`；
- `Function = Unknown`。

同时保持：

- CabinetId、IntervalId、SwitchId；
- TerminalId、ElectricalNodeId、SwitchAssemblyId；
- Interval 顺序；
- DisplayName；
- IntervalKind 与 GroundingStructureKind；
- RuntimeLayout；
- Professional 数据。

禁止：

- 从 DisplayName 解析 Index；
- 从 IntervalKind 猜测 Function；
- 从 Switch 组合或拓扑猜测 Function；
- 从 Layout 位置推断 Index；
- 在迁移时生成新的 Stable ID。

### 9.4 保存行为

旧工程加载并迁移后，运行时 ProjectFileDocument 应视为当前 Version 3。后续保存写出完整 Index 与 Function，不保留缺字段的 Version 2 形式。

迁移本身是加载兼容，不应被记录为用户 Command，也不应进入 CommandStack。工程打开后的 Dirty 语义应沿用现有 ProjectService/Session 约定，并在实现 Review 中用测试确认；本文不另建第二套 Dirty 规则。

## 10. Migration 风险与保护

### 10.1 特殊历史编号

`Index = Sequence` 是确定性兼容兜底，不代表已确认现场编号。历史柜体可能实际使用 `1、2、5、7` 等编号，而 Version 2 没有保存该事实。

迁移不能恢复从未持久化的信息。为避免错误假设：

- 不从名称猜测；
- Inspector 明确显示迁移后的 Index 和 Unknown Function；
- 依赖准确 Function 的专业能力必须拒绝 Unknown；
- 未来应设计受控的 Index/Function 校正流程；
- 在校正能力完成前，不把迁移值宣称为现场确认数据。

### 10.2 Unknown 的作用

Unknown 允许合法旧工程在不伪造专业用途的前提下打开，并明确暴露信息缺失。

Unknown 不是：

- Outgoing 的别名；
- Reserve 的默认值；
- 新建模板的占位输入；
- 未识别序列化字符串的容错目标。

如果 Version 3 文件包含未定义的 Function 字符串，应加载失败；只有明确的规范值 `Unknown` 才表示迁移兼容状态。

### 10.3 原子迁移

迁移应在内存中生成完整的当前 DTO/ProjectFileDocument，完成全部校验后再交给 Domain Mapper。失败时不得覆盖原文件，也不得留下部分 Domain 或 Layout。

加载操作不应就地改写工程文件。只有用户后续正常保存时才写出 Version 3。

## 11. Inspector 与 UI 影响

### 11.1 Selection

不需要修改：

- `SelectionTargetKind`；
- `SelectionReference`；
- `ResolvedSelection` 的对象身份语义；
- `SelectionObjectResolver` 的 Interval 查找逻辑。

原因是 RingCabinetInterval 的 Stable ID、ParentId 和 Layout 引用均不变化，只增加长期属性。

### 11.2 Inspector

现有 `PropertyProjector.ProjectInterval` 已投影 Sequence、DisplayName、IntervalKind 等字段。第一版应增加只读行：

- BayIndex；
- BayFunction。

不需要新增通用 Inspector Snapshot 类型；继续使用现有 PropertyRow 投影即可。

第一版不允许普通 Inspector 修改 BayIndex 或 Function，原因包括：

- BayIndex 修改需要柜内唯一冲突处理；
- Function 修改可能影响后续工作票和专业分析；
- 两者都需要专用 Command、校验和审计语义；
- 当前尚未冻结既有对象修改流程。

只读展示不会绕过 CommandStack，也不会改变 Dirty。

### 11.3 创建 UI

如第 7.3 节所述，现有 RingCabinet 创建 Dialog 必须补充新建时所需的显式 BayIndex 与 Function。该输入属于创建配置，不是 Inspector 编辑。

如果 Domain 实现先于 Desktop 输入完成，现有生产创建入口将无法合法提供 Function。因此 Domain、Persistence、Migration 和创建入口应作为同一可发布切片验证，避免中间版本破坏已有创建闭环。

## 12. Command、Undo/Redo 与 Dirty 影响

### 12.1 现有 Add/Remove Command

现有 `AddRingCabinetCommand` 保存首次创建完成的 RingCabinet 与 RingCabinetLayout 对象；Undo 移除同一对象，Redo 重新加入同一对象。

新增 BayIndex 与 Function 后：

- Command 结构不需要修改；
- Undo/Redo 自动保留两个属性；
- RingCabinetId、IntervalId、SwitchId、TerminalId、NodeId 和 Layout ID 不变化；
- Dirty 仍由 CommandStack 控制。

`RemoveRingCabinetCommand` 同样保存并恢复同一个聚合和 Layout，不需要字段级逻辑。

### 12.2 模板生成 Command

一个模板生成动作仍应形成一个原子 Command：

- Builder 先生成完整 RingCabinet 与 RuntimeLayout；
- Command 保存这组固定对象；
- Execute 原子加入；
- Undo 完整移除；
- Redo 恢复首次生成的相同对象；
- Redo 不重新运行 Builder、不重新生成 Stable ID。

如果第一版 Template Builder 只生成现有 RingCabinet 聚合，现有 AddRingCabinetCommand 可以继续作为原子加入边界；是否需要模板专用命令取决于 BuildResult 是否包含现有 Command 无法承载的其他对象，不应提前新增。

### 12.3 属性编辑

本阶段不设计 BayIndex/Function 编辑 Command。未来若允许修改，必须通过 CommandStack，并另行定义 SelectionTransition、唯一性校验和 Dirty 行为。

## 13. 实现所需最小文件类别

后续实现预计涉及以下类别，具体文件以代码 Review 为准。

Domain：

- 新增 `BayFunction` 枚举；
- 修改 `RingCabinetInterval`；
- 修改 `RingCabinetIntervalDefinition`；
- 修改 `RingCabinetDefinition` 聚合预校验；
- 修改 `RingCabinetIntervalRestoreDefinition`；
- 修改 `RingCabinet.Create/Restore/ValidateStructure`；
- 调整不能显式提供 Function 的便利 Factory 和调用方。

Persistence：

- 修改 `ProjectRingCabinetIntervalDto`；
- 修改 `ProjectDomainMapper` 保存/恢复映射；
- 将 FormatVersion 升级为 3；
- 增加 Version 1 → 2 → 3 的显式迁移链；
- 保持 Layout DTO、Professional DTO 和 Stable ID 合同不变。

Rendering/Inspector：

- 只修改 `PropertyProjector`，增加 BayIndex 与 Function 只读投影；
- 不修改 SelectionReference、Resolver、Symbol 或 RuntimeLayout。

Desktop 创建输入：

- 扩展 RingCabinet creation configuration、Dialog/ViewModel 和 CreationFactory 映射；
- 不在 MainWindow 构造 Domain；
- 不增加 Inspector 编辑入口。

Tests：

- Domain 创建与恢复不变量；
- Persistence Version 3 round trip；
- Version 2 → 3 和 Version 1 → 3 迁移；
- Stable ID 保持；
- Unknown 新建拒绝与恢复允许；
- Inspector 只读投影；
- 现有创建、Undo/Redo、Save/Reload 回归。

## 14. 测试设计要求

### 14.1 Domain 测试

至少覆盖：

- Sequence 仍按集合顺序生成 `1..N`；
- BayIndex 为 0 或负数时拒绝；
- 重复 BayIndex 在任何 Stable ID 生成前拒绝；
- 非连续 BayIndex（如 `1、2、5、7`）允许；
- 新建 Definition 使用 Unknown 时拒绝；
- Restore 使用 Unknown 时允许；
- 未定义 BayFunction 枚举值拒绝；
- BayIndex 与 Function 不改变现有 Switch、Terminal、Node 和 Assembly 拓扑。

### 14.2 Persistence 测试

至少覆盖：

- Version 3 保存并恢复 Sequence、Index、Function；
- Version 2 迁移为 `Index = Sequence`、`Function = Unknown`；
- Version 1 先补 Professional，再完成 Version 3 迁移；
- Version 3 缺少 Index 或 Function 时拒绝；
- 未知 Function 字符串拒绝，不映射为 Unknown；
- 迁移不改变任何 Stable ID；
- 迁移不改变 Layout、Connection、OverheadLine 或 Professional 数据；
- 迁移后保存写出 Version 3；
- DisplayName 中类似“负5间隔”的文本不参与迁移推断。

### 14.3 Command 与 UI 回归

至少覆盖：

- Add/Undo/Redo 保持 BayIndex、Function 和全部 Stable ID；
- Remove/Undo/Redo 保持同一聚合；
- Inspector 可以只读显示 BayIndex 与 Function；
- 新建 Dialog 拒绝重复/非正 BayIndex 和 Unknown Function；
- 非连续 BayIndex 可以正常创建、布局、选择、保存和恢复；
- CommandStack Dirty 语义不因字段增加而改变。

## 15. Builder 前置条件

生产 Template Builder 接入前必须完成：

1. 保留现有 Sequence 语义，不新增重复排序字段；
2. RingCabinetInterval 增加 BayIndex；
3. RingCabinetInterval 增加 Domain BayFunction；
4. Definition、Create、Restore 与聚合校验承载新事实；
5. 新建路径拒绝 Unknown，迁移恢复路径允许 Unknown；
6. ProjectRingCabinetIntervalDto 保存 Index 与 Function；
7. FormatVersion 升级为 3；
8. 实现 Version 1 → 2 → 3 的迁移链；
9. 完成 Domain/Persistence round-trip 与 Stable ID 测试；
10. Inspector 只读显示 BayIndex 与 Function；
11. 现有 Desktop 创建配置能够显式提供 BayIndex 与 Function；
12. 现有创建、Undo/Redo、Dirty、Save/Reload 回归通过。

生产 Template Builder 前暂不需要：

- PT Domain、Layout、Rendering 或 Persistence；
- DTU Domain、Layout、Rendering 或 Persistence；
- JSON Template 文件；
- 厂家模板目录；
- TemplateReference Persistence；
- BayIndex/Function Inspector 编辑；
- 自动命名、自动重编号或完整 Function/Equipment 兼容矩阵；
- 新 SelectionTargetKind；
- 大规模 Rendering 或 CommandStack 重构。

## 16. 最终设计结论

当前 Domain 已完整表达 RingCabinet 的设备与拓扑结构，也已表达物理 Sequence；生产 Template Builder 的最小缺口是 BayIndex 与 BayFunction 两个长期事实。

推荐的最小实现不是重构 RingCabinet 聚合，而是在现有创建/恢复边界增加这两个值，并在聚合层校验 Index 唯一性和新建 Unknown 禁止规则。

Persistence 必须升级到 FormatVersion 3。Version 2 使用 `Index = Sequence`、`Function = Unknown` 迁移；Version 1 必须经过现有 Version 2 语义后再迁移到 Version 3。Domain Mapper 只消费已经迁移的当前 DTO，不承担旧格式猜测。

Selection、RuntimeLayout、Rendering Symbol、现有 Add/Remove Command 和 CommandStack 不需要结构修改。Inspector 第一版只增加只读投影；现有 Desktop 创建输入需要同步承载显式 BayIndex 与 Function，避免以 Unknown 或设备结构猜测补值。

完成这些前置条件后，Template Builder 可以无损生成现有 RingCabinet Domain + RuntimeLayout，并通过一个原子 Command 进入既有 Undo/Redo、Dirty、Selection、Rendering 和 Save/Reload 闭环。
