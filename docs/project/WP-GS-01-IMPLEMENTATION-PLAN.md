# WP-GS-01 — Implementation Plan

> 状态：READY FOR IMPLEMENTATION
> 规划日期：2026-10-02。本轮仅制定计划，未授权实施。
> 业务合同：[Requirements Freeze](WP-GS-01-REQUIREMENTS-FREEZE.md)。架构合同：[Architecture Freeze](WP-GS-01-ARCHITECTURE-FREEZE.md)。本文件不修改冻结合同。

## 1. 重新核对的基线

- branch：`wp-gs-01`。
- HEAD / Architecture Freeze：`723f49ea66f6bfc03bf7e16cf01f3d368a6e0f45`。
- Requirements Freeze：`17d39e3e9f6d47d1be0347cb8a0ab8ad7df06dcd`。
- 本地 `origin/main`：`2162ed17a0ee264ea3e211c2193fd29e3099af57`。
- 原始 Implementation Plan 建立于 clean worktree；本次决策更新开始时，worktree 只有该计划文档未提交；`ProjectFileFormat.CurrentVersion = Version9`。
- REQUIREMENTS FROZEN / ARCHITECTURE FROZEN / NOT IMPLEMENTED / WINDOWS ACCEPTANCE PENDING。
- WP-WTA-01 / WP-EA-01A / WP-EA-01B 保持 CLOSED / ACCEPTED。

以下为产品负责人已确认两项决策后的实施交接稿。对应澄清已同步记录于 Architecture Freeze，不改变冻结业务范围或分层方向。

## 2. 已确认的产品决策

### Decision 1 — GAP 无法推导 Larger / Smaller

保留人工 `LineSide`，只拒绝能由真实拓扑可靠证明的矛盾。`ConnectionId + PoleId + AdjacentEndpoint + topology` 是安全 identity；`LineSide` 不决定 Energized / Deenergized。可靠推导相符则通过；可靠推导明确相反则模型完整性失败；Equal / Unresolved / 无法可靠推导时允许并原值保留，GS 不把人工标签当作侧别安全依据。普通 OHL GAP 不要求有安全意义的 Larger / Smaller identity。无新 persisted identity，V9 不变。

证据：[Pole](../../src/DistributionDrawing.Domain/Devices/Pole.cs) 只要求非空杆号；[GAP 创建服务](../../src/DistributionDrawing.Desktop/GroundingAccessPointCreation/GroundingAccessPointCreationService.cs) 允许无法解析时人工选择；现有 `UnsupportedOrEqualPoleNumbers_RequireManualChoice` 覆盖 `东支-甲 / P-11`。EA 的 [PoleNumberComparer](../../src/DistributionDrawing.Application/WorkTickets/PoleNumberComparer.cs) 对无法可靠比较的前缀、相同编号或复杂格式返回 Unresolved / Equal。

### Decision 2 — No Valid EA 时不自动建立结果

No Valid EA 包括未分析、NoSeeds、Analysis Failed 和结果 stale / invalid。此时 Seed 可编辑、开关可按已有非 GS 规则操作、GP 遵循冻结原行为；Seed / Switch 变化不自动发布新有效 EA，也不执行 Energized-based switch guard。须用户显式点击“带电分析”建立或重新建立有效 EA。

用户显式分析时以完整 candidate Seed Set + 当前 topology / switch states 计算，candidate GS 通过才发布 CurrentResult；拒绝不改变既有 Result。已有有效 EA 时，受控 switch mutation 前做 candidate GS；通过并执行真实 mutation 后继续现有实时 EA 重算。若重算进入 No Valid EA（包括 NoSeeds / Failed），后续 Seed / Switch 变化不自动恢复有效 EA，须再次显式分析。

实现证据：[ProjectRuntimeSession.OnCommandStackStateChanged](../../src/DistributionDrawing.Desktop/ProjectRuntimeSession.cs) 当前自动重算按 Freshness / LatestResult 判断；需改为以成功的 `CurrentResult` 决定后续 Seed / Switch 自动重算。现有 [EnergizationRuntimeTests](../../tests/DistributionDrawing.Desktop.Tests/EnergizationRuntimeTests.cs) 需验证 NoSeeds / Failed 后等待显式分析，并验证有效 EA 下 Switch 仍自动重算。

## 3. 实际代码映射

| 项目 / namespace | 主要文件 | 现有事实 / 接入约束 |
| --- | --- | --- |
| Domain.Documents / Professional | [DrawingDocument](../../src/DistributionDrawing.Domain/Documents/DrawingDocument.cs)、[GroundingAccessPoint](../../src/DistributionDrawing.Domain/Professional/GroundingAccessPoint.cs)、[GroundingTarget](../../src/DistributionDrawing.Domain/Professional/GroundingTarget.cs) | 邻接校验未交叉验证 LineSide；typed Target 不变 |
| Domain.Devices.SwitchAssemblies / RingCabinets | [SwitchAssembly](../../src/DistributionDrawing.Domain/Devices/SwitchAssemblies/SwitchAssembly.cs)、[RingCabinet](../../src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinet.cs)、[RingCabinetInterval](../../src/DistributionDrawing.Domain/Devices/RingCabinets/RingCabinetInterval.cs) | 普通间隔已有派生规则；融合间隔已有 circuit-to-Earth 闭合路径；统一入口并复用原实现 |
| Application.Topology / Energization | [GraphBuilder](../../src/DistributionDrawing.Application/Topology/ElectricalConnectivityGraphBuilder.cs)、[Analyzer](../../src/DistributionDrawing.Application/Energization/EnergizationAnalyzer.cs)、[BoundaryPolicy](../../src/DistributionDrawing.Application/Energization/EnergizationBoundaryPolicy.cs)、[Result](../../src/DistributionDrawing.Application/Energization/EnergizationResult.cs) | graph 与 Analyzer.GroundingSwitchConnections 都直接读取 SwitchState，二者都要适配 view |
| Application.Energization / Devices | [AnalysisState](../../src/DistributionDrawing.Application/Energization/EnergizationAnalysisState.cs)、[ScenarioCommand](../../src/DistributionDrawing.Application/Energization/EnergizationScenarioCommand.cs)、[ChangeSwitchStateCommand](../../src/DistributionDrawing.Application/Devices/ChangeSwitchStateCommand.cs) | 分析与发布目前合在 Execute；业务命令直接 mutation，需前置检查 |
| Rendering.Wpf.Interaction.Professional | [Factory](../../src/DistributionDrawing.Rendering.Wpf/Interaction/Professional/ProfessionalCommandFactory.cs)、[Add GP](../../src/DistributionDrawing.Rendering.Wpf/Interaction/Professional/AddGroundingPointCommand.cs)、[Remove GP](../../src/DistributionDrawing.Rendering.Wpf/Interaction/Professional/RemoveGroundingPointCommand.cs)、[GAP / Composite commands](../../src/DistributionDrawing.Rendering.Wpf/Interaction/Professional/GroundingAccessPointCommands.cs) | 业务命令调用 Application；GAP + GP 复合操作在首个子命令前检查 |
| Rendering.Wpf.Interaction | [CommandStack](../../src/DistributionDrawing.Rendering.Wpf/Interaction/CommandStack.cs) | 成功后才推进 cursor / history；不自动保证 command 内部分 mutation 的原子性；不增加 GS 依赖 |
| Desktop | [Runtime](../../src/DistributionDrawing.Desktop/ProjectRuntimeSession.cs)、[SwitchOperationController](../../src/DistributionDrawing.Desktop/SwitchOperation/SwitchOperationController.cs)、[EnergizationPanel](../../src/DistributionDrawing.Desktop/Energization/EnergizationPanel.xaml.cs)、[MainWindow](../../src/DistributionDrawing.Desktop/MainWindow.xaml.cs) | session-scoped Application context、既有 error 通道；保持命令生命周期 / selection 分类 |
| Infrastructure.Persistence | [ProfessionalMapper 所在文件](../../src/DistributionDrawing.Infrastructure/Persistence/ProjectProfessionalDto.cs)、[Format](../../src/DistributionDrawing.Infrastructure/Persistence/ProjectFileFormat.cs)、[Container](../../src/DistributionDrawing.Infrastructure/Persistence/ProjectFileContainer.cs) | restore 经 Domain 创建；无 DTO / schema / 版本变化 |

实现必须注意：

- EA Result 没有 Connection 状态字典。Resolver 按 ConductingEdges 的 Type.Connection + SourceId 查唯一 edge，查询两端 Terminal result 并验证一致；不能把 missing / inconsistent 当 Deenergized。
- 有效 EA 是 CurrentResult（Freshness.Current + IsSuccess）；LatestResult 非空、Freshness.Current、overlay 显示与否都不能单独替代。
- 合法普通 / 融合间隔允许 CableTerminalId 为 null，CircuitNodeId 与内部开关端子仍存在。Domain 派生 location 返回既有 CircuitNodeId 与 nullable CableTerminalId；有外部端子时检查 Terminal，无端子时检查同一 CableSide 的既有 circuit node。必须验证其对应关系，不生成端子或新 persisted identity。
- 普通间隔复用 SwitchAssembly 的 EffectiveGrounding 规则；融合间隔复用已有 HasClosedPathFromExternalTerminalToEarth。当前 ValidateStructure 与 assembly evaluation 读取正式状态，candidate evaluator 不能只验证 current 而漏掉候选组合合法性。
- Runtime 对有效 EA 下的 switch 与经 candidate guard 通过的 Seed 变化保持现有实时重算；其他 history mutation 默认使 EA stale，包括 GP 操作。成功 GP 操作后须显式重建有效 EA 才进入 valid-EA 送电 guard。No Valid EA 时 Seed / Switch 后续变化不自动分析或发布；只有用户显式请求分析会建立结果。
- Domain 不能依赖 Application.WorkTickets.PoleNumberComparer；若共用比较核心下移到 Domain，保留 EA / WTA 的既有行为与调用外观，不改变杆号语法。

## 4. Slice 顺序、依赖与提交

保留 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12 → 13。Slice 2 提前提供最小 Domain 只读 switch-state 契约，Slice 3 的 Application overlay 实现它，避免 Domain 反向依赖 Application。每个 Slice 的相关测试随 Slice 交付，不拖到 Slice 12。

依赖：S1 → S5；S2 → S3；S3 → S4；S2 + S4 + S5 → S6；S6 → S7 / S8 / S9；S7 + S8 + S9 → S10；S7–S10 → S11；S1–S11 → S12 → S13。正式实施串行交付小提交，全部集成前不能宣称已实现或已接受。

下列新增 API 名称为建议，不是已存在的能力；名称可按项目风格调整，职责边界必须保持。

### Slice 1 — GAP Identity Integrity

- **修改范围 / 文件：** Domain.DrawingDocument 的 GAP 校验与最小 Domain 只读侧别 / 杆号比较 helper；必要时 Application.PoleNumberComparer / BoundaryPolicy 仅作共用核心适配；Desktop.GroundingAccessPointCreationService 提示与约束对齐。
- **API：** 只读 GAP topology / side resolution，可由 Create / Add / restore、Application resolver 与 prospective GAP snapshot 共用；区分 Less / Greater / Equal / Unresolved。仅 Less / Greater 与 LineSide 明确相反时失败；Equal / Unresolved 通过且保留原 LineSide，GS resolver 不使用它决定 Energized / Deenergized。TransformerSide / PlacementSide 规则不变。
- **新增 / 修改测试：** Domain.GroundingAccessPointTests；Infrastructure.WpEm04GroundingPersistenceTests；Desktop.WpEm04GroundingWorkflowTests。Larger / Smaller 一致、可证明矛盾拒绝、Equal / 不同前缀 / 中文杆号无法推导仍合法、普通非开关 OHL、反向 SupportPole 顺序、Transformer 双端 PlacementSide、合法 V9 typed / legacy round-trip；更新人工覆盖及双 side V9 restore tests，证明原 LineSide 原样保存。
- **Acceptance：** 新建及恢复均拒绝可证明矛盾；不可推导不拒绝、LineSide 稳定往返且不参与 GS 状态判定；IDs / 邻接 / placement 不变；FormatVersion 9 无新字段。
- **Commit：** `fix(domain): reject provable grounding access side conflicts`。

### Slice 2 — Effective Grounding Domain API

- **修改范围 / 文件：** Domain.Devices.SwitchAssemblies.SwitchAssembly；Domain.Devices.RingCabinets.RingCabinet / RingCabinetInterval；最小只读状态契约与非持久化派生结果。
- **API：** 建议 `ISwitchStateView.GetSwitchState(SwitchDevice)`（保留 nullable current state 语义）、current fact adapter；`SwitchAssembly.Evaluate(view)`、`RingCabinet.EvaluateIntegratedFeederInterval(intervalId, view)`、统一 `GetEffectiveGroundingLocations(view)`；无参旧 API 委托 current view。location 为 CabinetId / IntervalId / CircuitNodeId / CableTerminalId?，不新增 GroundingTarget kind。
- **实现约束：** 复用 EvaluateStates / 既有接地 rule 与闭合路径 helper，让 interlock、operational evaluation、grounding 都读同一 view；不建立第二份 GroundingStructureKind 规则表，不新增 PT / 其他设备产品语义。非法 candidate 返回明确 evaluation failure，不用 false grounding 放行。
- **测试：** Domain.IntegratedFeederIntervalEvaluationTests、SwitchStateOperationTests 及现有 assembly 测试；普通 LS/GS 4 组合、每种融合结构 8 组合、UpperIsolation GS Closed + Breaker Open、非法组合、CableTerminal 有 / 无、current/view parity、多 override、既有 PT 行为回归。
- **Acceptance：** 四种冻结结构规则与既有 interlocks / OperationalState 不变；精确 CableSide location；只读、不持久化。
- **Commit：** `refactor(domain): expose read-only effective grounding evaluation`，含状态读取契约。

### Slice 3 — Candidate Electrical State View

- **修改范围 / namespace：** 新 Application.Energization.CandidateElectricalState；使用 S2 Domain 读取契约，现有 Scenario / Seed 事实模型不变。
- **API：** `Create(drawing, switchOverrides, candidateScenario)`；只读 override 字典与完整 Seed snapshot；override 优先，否则 current fact；复制输入集合，校验 switch ID / state，独立现有 Scenario 或只读快照不写回正式 Scenario。不克隆 Drawing、不 mutate/analyze/restore。
- **测试：** 新 Application.CandidateElectricalStateTests：零 / 单 / 多 override、候选 Seed、输入集合后来被改、非法 identity / state、Domain 与正式 Scenario 事实不变；history / dirty / CurrentResult 在集成层验证。
- **Acceptance：** graph builder 与 Domain evaluator 可收到同一个 view；无 formal command / publication。
- **Commit：** `feat(ea): add read-only candidate electrical state view`。

### Slice 4 — Candidate EA Support

- **修改范围 / 文件：** Application.GraphBuilder、EnergizationAnalyzer；必要时 EnergizationUiService；保留 BoundaryPolicy / Result 的现有契约。
- **API：** `Build(drawing, view)`、`Analyze(drawing, scenario, view)`；旧入口使用 current view。closed switch edges 与 Analyzer.GroundingSwitchConnections 两处都读取 view，复用原传播算法，不引入 GS 到 Analyzer。
- **测试：** Application.ElectricalConnectivityGraphBuilderTests / EnergizationAnalyzerTests：current vs candidate、多个 override、PoleSwitch 两侧、CableTermination、GroundSwitch 不传播、GS connection metadata、invalid topology / seed、0 Seed = NoSeeds、成功无 Unknown；Desktop.EnergizationRuntimeTests 验证缓存 / overlay / dirty 不变。
- **Acceptance：** 正式行为回归通过；failure 无部分 point state；candidate result 不调用 AnalysisState、不替换 CurrentResult。
- **Commit：** `feat(ea): analyze candidate switch states without mutation`。

### Slice 5 — GroundingTarget Energization Resolver

- **修改范围 / namespace：** 新 Application.GroundingSafety.GroundingTargetEnergizationResolver 与最小 resolution result；引用 S1 integrity 与既有 Result / edge。
- **API：** `Resolve(drawing, target, successfulResult)` → resolved identity + point state，或明确 integrity failure。Terminal → TerminalId；GAP → Connection edge。组合新增 GAP 支持经过 Domain 只读验证的 prospective GAP 描述，不能为解析先加入正式 Drawing。
- **测试：** 新 Application.GroundingTargetEnergizationResolverTests：Terminal / GAP Energized / Deenergized、多杆同 Connection、开关杆两侧、Transformer GAP、missing target / Connection / edge / terminal result、不一致 GAP、两端状态矛盾。
- **Acceptance：** 映射唯一且合法；失败从不解释为 Deenergized；不引入 Unknown、不拆 Connection、不读取 Rendering。
- **Commit：** `feat(gs): resolve grounding targets to EA identities`。

### Slice 6 — Grounding Safety Core Guard

- **修改范围 / namespace：** 新 Application.GroundingSafety.GroundingSafetyGuard、GroundingSafetyDecision 与受控操作 preflight service。
- **API：** 输入 drawing、successful current/candidate EA、同一 switch view、当前及待增加 / 恢复 grounding descriptors；输出 Allowed，或 Rejected + reason + affected locations。current target 用于 GP 创建；candidate 枚举 GP + 冻结内置 EffectiveGrounding；待恢复 GP 只进入只读检查集合。
- **测试：** 新 Application.GroundingSafetyGuardTests：Terminal/GAP、多个 GP、四种环网柜结构、CableSide 精确 identity、全部 affected locations、无冲突、integrity / analysis failure、重复调用与 facts 不变。
- **Acceptance：** 唯一 Grounded + Energized 判据；错误不当 Allow；无 MessageBox / Rendering / persistence / Undo 引用，Domain 与 EA 保持原职责。
- **Commit：** `feat(gs): add application grounding safety guard`。

### Slice 7 — GroundingPoint Creation Guard

- **修改范围 / 文件：** Rendering.Interaction.Professional.AddGroundingPointCommand / Factory / GAP Composite；Desktop.MainWindow、GAP creation service、Runtime 的 Application context 注入。
- **API / 接线：** 业务 command 转交 target 给 Application preflight，每次执行取实时 CurrentResult，不捕获创建时 Result；NoEA 原行为，valid EA 按 Target state allow/reject。GAP + GP 复合根命令在第一子命令前用 prospective GAP snapshot 检查，禁止等 GAP 已创建再拒绝 GP。
- **测试：** 新 Rendering.GroundingPointSafetyCommandTests、Desktop.GAP workflow：NoEA / stale / failed、valid Energized / Deenergized、picker / 选中 GAP / 杆塔 / Transformer 入口、Composite Reject 时 GAP 也未创建、Add.Redo 同方向检查；比较 Domain / GP count / layout / history / cursor / savepoint / dirty / EA / selection。
- **Acceptance：** Reject 在所有真实 mutation 前；不依赖 Undo 安全回滚。成功操作沿用现有 stale 生命周期。
- **Commit：** `feat(gs): guard grounding point creation commands`；最小可用错误接线随本 Slice，不留到 S11。

### Slice 8 — Switch Operation Guard

- **修改范围 / 文件：** Application.Devices.ChangeSwitchStateCommand；Desktop.SwitchOperationController / Runtime；S6 preflight。保留 adapter 的 ISwitchStateCommand 分类。
- **API / 流程：** 每次 Execute / Undo / Redo 查询实时有效 EA，以目标方向构造 view → candidate EA → 同 view EffectiveGrounding → Guard → 原 Domain.ChangeSwitchState。既有 LoadSwitch / CircuitBreaker / IsolationSwitch / GroundSwitch / Pole 开关及用户站进线开关共用；不新增 interlock。
- **测试：** Application.ChangeSwitchStateCommandTests、Desktop.SwitchOperationControllerTests / runtime：四种结构、UpperIsolation 两个成地方向、已有 GP 的上游合闸、PoleSwitch 两侧、CableTermination、NoEA 原行为、分闸 / 解除接地。普通 LS 原机械互斥与 GS Reject 分开验证，不能拿原 interlock rejection 当 GS 已实现。
- **Acceptance：** Reject 不改变 switches、InitialChange / command 快照、history / dirty / EA / scene；通过后沿用正式重算；降低 reachability / 解除 grounding 不被 GS 阻止，但原 Domain 限制不撤销。
- **Commit：** `feat(gs): preflight switch state operations`。

### Slice 9 — Seed / Analysis Guard

- **修改范围 / 文件：** Application.ScenarioCommand、AnalysisState、Application.GroundingSafety analysis submission；Desktop.Runtime / ScenarioCommandAdapter / EnergizationPanel。
- **API：** ScenarioCommand 的 before/after 只读 snapshot；`PrepareAnalysis(drawing, candidateScenario, view)` → EA + GS decision；通过后 `PublishAcceptedResult`。legacy V9 completeness 字段保留存储，不重新赋予业务语义。候选 result 本身不成为 CurrentResult；通过后正式发布可复用该已验证计算，避免无谓重算。
- **流程：** 用户显式 Analyze 始终候选化完整 Seed Set，检查通过才发布；valid EA 下 Add / Replace 等自动重算路径在 Scenario mutation 前检查完整候选。删除 Seed 是减少送电方向，但删除最后 Seed 若重算为 NoSeeds 必须退出 Valid EA。无 Valid EA 下 Seed 可编辑且只更新配置 / history，不调用分析或发布；此后添加 Seed 仍等待显式 Analyze。Undo / Redo 使用目标 Scenario snapshot。不新增 Seed draft UI / Domain。
- **测试：** 新 Application.AnalysisSubmissionTests、EnergizationUiTests；Desktop.EnergizationPanelTests / runtime：多 Seed 整体 Reject、GP / 内置 grounding、首次与重新显式分析、有效 EA 下 Add / Replace mutation 前拒绝并通过后重算、0 Seed 退出有效状态、NoSeeds / Failed 后 Add Seed 不自动计算、有效 EA 下 Switch 自动重算、旧结果不被候选替换。No Transformer extension / No Unknown / No outside inference。
- **Acceptance：** 所有正式 EA 发布路径均受检；Rejected Seed edit 保持 IDs / 顺序 / history / dirty；No Valid EA 时 Seed 可编辑但不会自动建立有效结果；显式 GS Reject 不改 Seed 或旧 Result；有效 EA 下通过 guard 的 switch 仍在真实 mutation 后实时重算；重算进入 NoSeeds / Failed 后后续 Seed / Switch 变化不自动恢复；0 Seed 仍为 NoSeeds，不能假称全图 Deenergized。
- **Commit：** `feat(gs): validate seed scenarios before EA publication`。

### Slice 10 — Undo / Redo Safety Integration

- **修改范围 / 文件：** 复核 S7–S9 三方向接线；GP Remove.Undo；[CompositeDelete / SelectionDeletePlanner](../../src/DistributionDrawing.Desktop/DrawingTools/SelectionDeletePlanner.cs)、[WorkTicketGuardedDeleteCommand](../../src/DistributionDrawing.Desktop/DrawingTools/WorkTicketGuardedDeleteCommand.cs) 与 GAP Composite 仅作受控 GP 根级 preflight / 透传。不把 Guard 加入通用 CommandStack。
- **API：** 根级业务描述一次检查全部目标方向，之后才调用原 inverse；AddGP Undo 是删除，RemoveGP Undo 是恢复，switch / Seed Undo 用 before、Redo 用 after；GP metadata-only change 原本禁止 target rebinding，保持原行为。
- **复合操作：** 不能先恢复 Layout / 引用 / 一个 GP 后才因另一 GP Reject。普通 topology 编辑、设备结构 / 连接恢复、Paste 仍按既有 stale lifecycle，不扩展成通用候选拓扑仿真。
- **测试：** 新 GroundingSafetyUndoRedoTests、Rendering.CommandStackTransactionTests、Desktop复合删除 / session测试；逐项比较 Domain、Layout、History、CurrentIndex、CurrentStateId、SavedStateId、LastAppliedCommand、dirty、事件计数，含saved与已dirty两种起点；原scene-validation回退继续回归。
- **可到达性：** 普通线性 stack 不能越过后来创建的 GP 去 Undo 更早分闸；不新增选择性 Undo。command 单测可设置后来出现的 grounding fact；真实 GUI 使用旧 V9 已有 GP → 无EA分闸 → 显式分析 → Undo分闸，或无EA创建GP → 删除GP → 显式分析 → Undo删除被阻止，以及Undo AddGP后分析再Redo AddGP被阻止。
- **Acceptance：** Reject 在Undo/Redo第一处mutation前；cursor / dirty / events不变；实时context，不捕获历史EA；adapter/wrapper不丢EA或selection分类。
- **Commit：** `fix(gs): enforce grounding safety before undo and redo`。

### Slice 11 — Minimal Desktop Messaging

- **修改范围 / 文件：** MainWindow、DesktopMessageService、SwitchOperationController、EnergizationPanel；GS decision 到既有 blocking 通道的映射。
- **行为：** ToUserMessage 当前将未知错误压成通用失败，需保留GS reason；EA panel的switch错误目前只写文本，GS blocking需接既有ShowError/MessageBox。一次操作只弹一次，正常通过不提示。
- **文案族：** 当前带电不能增加工作接地；合闸将向接地点送电；带电电缆将形成有效接地；Seed配置将向接地点送电。包含操作、GP Number/Location或柜体/间隔CableSide与原因；多位置可放在同一简单消息，不新增conflict list UI。
- **测试：** Desktop.SwitchOperationControllerTests / EnergizationPanelTests、message service fake：GP、switch、Seed、Undo/Redo均blocking，验证操作/位置/原因而非只搜索源码字符串。
- **Acceptance / Commit：** 无panel、分类体系、canvas alarm、locator；`fix(desktop): unify grounding safety blocking messages`。S7–S10已有基本提示，本Slice仅收束一致性。

### Slice 12 — Full Automated Acceptance

- **范围：** 各Slice测试已随逻辑提交；补齐跨层组合与完整五套回归，不新增生产能力。
- **测试矩阵：** Domain GAP/四结构/候选组合/原联锁；Application resolver/view/EA/guard/Seed/switch；Rendering/ Desktop复合拒绝/history/dirty/Undo/Redo/message/EA原生红色回归；Infrastructure合法V9 round-trip、旧shape、风险事实组合正常加载、Decision 1边界、派生结果不落盘。
- **主要文件：** 前述测试及Infrastructure.EnergizationPersistenceTests / WpEm04GroundingPersistenceTests；Rendering.EnergizationNativeRenderingTests / EnergizationOverlayTests；Desktop.EnergizationRuntimeTests；新增GroundingSafety*Tests。
- **Acceptance：** Windows对已提交/已推送candidate运行五套测试，failed=0、skipped=0；solution build通过；记录真实SHA、命令、TRX、parsed counts、diff-check与工作树。EA-01B的1542是历史基线，不是本轮实测或预定总数。
- **Commit：** `test(gs): complete grounding safety integration acceptance`，必要的实现报告与STATUS/ROADMAP按实际状态更新；此时GUI acceptance仍Pending。

建议后续验证命令（本轮未执行）：

```powershell
dotnet restore src/DistributionDrawing.sln
dotnet build src/DistributionDrawing.sln --no-restore -c Release
dotnet test tests/DistributionDrawing.Domain.Tests/DistributionDrawing.Domain.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=domain.trx' --results-directory artifacts/wp-gs-01/windows
dotnet test tests/DistributionDrawing.Application.Tests/DistributionDrawing.Application.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=application.trx' --results-directory artifacts/wp-gs-01/windows
dotnet test tests/DistributionDrawing.Infrastructure.Tests/DistributionDrawing.Infrastructure.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=infrastructure.trx' --results-directory artifacts/wp-gs-01/windows
dotnet test tests/DistributionDrawing.Rendering.Wpf.Tests/DistributionDrawing.Rendering.Wpf.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=rendering.trx' --results-directory artifacts/wp-gs-01/windows
dotnet test tests/DistributionDrawing.Desktop.Tests/DistributionDrawing.Desktop.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=desktop.trx' --results-directory artifacts/wp-gs-01/windows
git diff --check
```

按实机SDK/restore状态核对命令；测试顺序执行，TRX独立目录。macOS可运行三个net10.0项目，WPF cross-build只作编译检查，不能替代两个Windows/WPF项目的运行验收。Windows缺陷修复独立fix commit，复核受影响检查及最终完整suite。

### Slice 13 — Windows GUI Acceptance

前置：完整自动化通过，明确已提交/推送candidate SHA，产品负责人在Windows实机验收。

| 场景 | 通过判据 |
| --- | --- |
| No valid EA | GP创建和普通设备操作保留原能力；Seed / Switch变化不自动发布结果；显式分析建立有效EA |
| Energized Terminal/GAP创建GP | blocking error，facts/history/dirty不变 |
| Deenergized Target / GS-AF-05 | 正常允许；GroundingPoint / GAP 接地措施操作不改变导电事实时保持同一 valid EA 与 overlay；后续送电仍经过 GS guard |
| PoleSwitch两侧 / 同Connection多SupportPole | 两侧独立，同Connection GAP继承Connection状态 |
| 普通LS / UpperLower / LowerLower带电Cable合GS | GS拒绝；原mechanical interlock另行区分 |
| UpperIsolation | GS Closed+Breaker Open允许；带电时再合Breaker或Breaker已Closed再合GS形成危险时拒绝 |
| 已有GP上游合闸 | 先建立valid EA，candidate送电被拒绝 |
| Seed分析与重算生命周期 | 显式分析检查完整Seed；valid EA下Switch通过检查后实时重算；NoSeeds/Failed/失效后变化不自动恢复 |
| Undo/Redo GP/开关/Seed恢复与复合操作 | 可到达序列中前置拒绝，cursor/dirty/facts不变 |
| 删除GP / GS分闸 / 普通开关分闸 / 删除Seed | GS不阻止解除危险方向，原Domain约束仍在 |
| Save/reopen | V9，facts不自动改写，重开无缓存EA，再次分析经过GS |
| UI与Rendering | 简单中文blocking、明确location、红色渲染不回归，无安全panel或额外复杂UI |

验收结论和candidate SHA由产品负责人确认后，独立 `docs: close WP-GS-01` 记录证据；只有全部通过才CLOSED/ACCEPTED。不预先创建closure或声称Windows验收完成。

## 5. Commit boundaries 与门槛

C1–C12各对应S1–S12小提交，S13为独立acceptance/closure文档提交。resolver、guard、三种业务集成保持独立，不合并成大提交。每个逻辑提交自带直接相关测试；S12不是首次补测试。

每个Slice：核对scope → 直接相关自动化 → 对照冻结条款 → diff --check → 报告文件/结果/git status。按WORKFLOW默认报告评审后提交；本轮没有生产实现提交授权。完整集成前的中间提交不作为已接受安全版本交付。

## 6. 风险与rollback strategy

| 风险 | 触发位置与预防 | 回退策略 |
| --- | --- | --- |
| GAP旧V9加载兼容 | restore调用Create；Decision 1已确认；可证明矛盾与不可推导分开；旧断言逐项处理 | revert S1及依赖，保留原文件，不静默重写LineSide |
| Domain反向依赖 | shared契约置Domain、overlay置Application，无新persisted model | revert API与consumers，不保留第二套evaluator |
| RingCabinet抽取改变语义 | 原EvaluateStates、ValidateStructure与closed-path需candidate-aware；全组合parity | revert S2，修复抽取，不新增规则表掩盖差异 |
| EA候选状态读取不一致 | graph edge与GS connection metadata、Domain evaluator共用同view | revert S3/S4及依赖；不采用真实mutation模拟 |
| EA回归 | Earth/GS排除、BoundaryPolicy、NoSeeds/failure/binary、CableTermination | revert输入适配，保持原Analyzer，不造GS analyzer |
| CommandStack被破坏 | 命令失败前partial mutation不会自动原子恢复；cursor仅成功后改变 | 根级业务preflight；revert接线，通用stack无GS依赖 |
| Reject太晚 | GAP+GP、Scenario.Apply、CompositeUndo、ticket wrapper | 第一mutation前验证全部candidate；原scene回退仅用于原事务失败 |
| 无有效EA自动发布bypass | Runtime用LatestResult而非CurrentResult，Decision 2已确认 | revert S9；不在StateChanged之后才Reject已mutation命令 |
| wrapper丢分类 | LastAppliedCommand类型匹配、ISwitchStateCommand、selection transitions | 优先注入已有业务命令；adapter保留标记；revert wrapper |
| Undo/Redo用历史EA | command寿命长，必须每次读当前session有效结果 | revert context，不捕获创建时Result放行 |
| optional cable terminal / PT扩展 | CircuitNode与CableSide对应测试；不套新PT产品规则 | 无新terminal/model；扩大接地语义返回产品确认 |
| GUI场景不可到达 | 线性history不能越过较新GP命令 | 修正验收步骤，不新增选择性Undo框架 |
| macOS冒充Windows | 两个WPF测试须Windows执行，正式GUI须committed/pushed SHA | 保持Windows Pending，独立fix与复验 |

回退用独立revert保留评审历史，不reset发布分支、不把derived state写回用户文件、不升级V9。如真正缺失persisted business fact，按Architecture Freeze停止并返回产品负责人。

## 7. 本轮输出边界

本轮只新增此计划文档；不修改生产代码、测试、冻结合同、STATUS/ROADMAP；不运行build/test；不创建commit。进行文档链接/范围检查与git diff --check。

**READY FOR IMPLEMENTATION**

产品已确认 Decision 1 / 2，计划中无待决 Product / Architecture blocker。此状态表示后续实现可按本计划开始；本轮仍仅完成文档更新与 planning commit，不开始实现。
