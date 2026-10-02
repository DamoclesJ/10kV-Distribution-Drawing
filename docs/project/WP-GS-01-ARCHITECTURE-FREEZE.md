# WP-GS-01 — Architecture Freeze

> 状态：ARCHITECTURE FROZEN / NOT IMPLEMENTED
> 冻结日期：2026-10-02
> 正式业务合同：[WP-GS-01 Requirements Freeze](WP-GS-01-REQUIREMENTS-FREEZE.md)。本文件冻结实现架构与边界，不修改业务需求含义。
> 基线：Requirements Freeze `17d39e3e9f6d47d1be0347cb8a0ab8ad7df06dcd`；工作分支 `wp-gs-01`；`FormatVersion = V9`。

## 1. 总体分层

严格保持以下职责边界。

### Domain

负责 electrical facts、device structure、switch states、GroundingPoint / GroundingTarget、RingCabinet grounding structure，以及 Effective Grounding 的只读派生判断。

Domain 不负责 Energized / Deenergized propagation、GS Allow / Reject 或 UI 提示。

### Application

负责 Energization Analysis、candidate / hypothetical electrical state、GroundingTarget → EA identity resolution、Grounding Safety Guard，以及 Allow / Reject 与拒绝原因。

### Desktop

负责调用 Application，并在 Reject 时复用现有 blocking error / MessageBox 通道显示提示。不新增安全冲突面板。

## 2. Effective Grounding

Effective Grounding 是 Domain 的只读派生能力，不是 persisted fact。优先复用并统一现有 `SwitchAssembly`、`RingCabinet`、`GroundingStructureKind`、interval topology 和 switch states 中的接地计算事实，不复制创造第二套互相独立的接地规则。

Effective Grounding 应能够回答：在给定当前或候选 switch-state view 下，哪些精确 electrical locations 已形成有效接地。

环网柜最终有效接地目标统一落到该间隔的 `CableTerminalId` / CableSide electrical identity。现有结构语义保持：

- 普通 LoadSwitch interval：合法的 GroundSwitch Closed 状态形成 Cable Effective Grounding。
- `UpperLowerGrounding`：GroundSwitch Closed 形成 Cable Effective Grounding。
- `LowerLowerGrounding`：GroundSwitch Closed 形成 Cable Effective Grounding。
- `UpperIsolationGrounding`：GroundSwitch Closed + Breaker Closed 才形成 Cable Effective Grounding。

不得新增 persisted `EffectiveGrounding` 字段。

## 3. OHL Safety Granularity — GS-AF-01

Grounding Safety 对架空线路以现有 `Connection` 为最小 Energization 判断单位。一条包含多个 SupportPole 的同一 Connection，在 WP-GS-01 中不拆成相邻杆跨段级 EA identity；GAP 的实际物理位置仍正常保存，其带电状态继承所属 Connection 的 EA 状态。

PoleSwitch 等真实 electrical interruption 继续通过既有 Terminal、ElectricalNode、Connection 和 ClosedSwitch edge 形成带电区域分界。

## 4. GAP Identity — GS-AF-02

GAP 的安全 electrical identity 以真实拓扑为权威：`ConnectionId`、`PoleId`、`AdjacentEndpoint` 共同确定其所在 Connection / physical side。`LineSide` 必须与依据真实相邻杆号 / endpoint 推导出的实际侧别一致。

现有 Domain 允许 `LineSide` 与 `AdjacentEndpoint` 矛盾，属于模型完整性缺口。WP-GS-01 可增加最小必要 Domain integrity validation，防止新建或恢复出矛盾 GAP。

不得增加新的 persisted GAP identity，也不得依赖单独的 `LineSide` 标签作为 Grounding Safety 的权威来源。

## 5. GroundingTarget → EA Resolver

该映射属于 Application 层。统一提供只读 resolver，使 GS 不需要自行了解不同 Target 的实现细节。冻结映射如下：

- Terminal Target → `TerminalId` → EA Terminal result。
- GAP → `ConnectionId` → EA Connection conducting-edge result。

如果模型无法得到唯一、合法 identity，不得解释为 Deenergized；这属于模型完整性错误，不重新引入 EA Unknown。

## 6. Candidate Electrical State

禁止通过临时修改真实 Domain、Analyze、再恢复的方式实现 candidate EA。Reject 必须在任何真实 Domain mutation 前完成。

需要建立只读 candidate electrical state view / overlay，至少能够表达某个或若干 `SwitchDevice` 的候选 `SwitchState` 以及 candidate Seed Scenario / Seed Set。candidate override 存在时使用 candidate value；没有 override 时使用当前 Domain fact。

EA Graph Builder 和 Effective Grounding evaluator 必须读取同一个候选状态视图，避免二者看到不同状态。candidate state 不得写回 Domain。

## 7. Candidate EA

继续复用现有 `EnergizationAnalyzer` 的纯计算性质，不创建独立的 GS Energization Analyzer。

对于 candidate Seed，可以使用 candidate Scenario，不修改正式 Scenario。对于 candidate SwitchState，通过 candidate state view 让 topology / graph construction 看见候选状态，不修改正式 `SwitchDevice.State`。

Candidate EA 不成为 CurrentResult、不改变 Rendering、不持久化、不进入 Undo，也不造成 project dirty。

## 8. Grounding Safety Guard

Grounding Safety Guard 位于 Application 层，判断 candidate state 中是否存在已经有效接地且同时为 Energized 的精确 electrical location。已接地位置统一包含：

1. `GroundingPoint` 的 Terminal Target 与 GAP Target。
2. 设备内置 Effective Grounding，当前主要为 RingCabinet CableSide。

安全原则为 `Grounded + Energized` → Reject candidate operation。Guard 不修改任何 Domain facts。结果只表达 Allowed，或 Rejected + blocking reason + affected grounding location(s)。

## 9. GroundingPoint Creation

存在有效 EA 时，GroundingPoint Add Command 执行前先 Resolve Target 并查询当前有效 EA：Energized → Reject；Deenergized → Allow。Reject 必须发生在 Command mutation 前，因此不得有 Domain mutation、history entry 或 dirty-state change。

没有有效 EA 时，保持现有 GroundingPoint 创建行为。

## 10. Switch Operation Guard

已有有效 EA 时，对可能形成或扩大 Energized reachability 的受控 switch-state operation，在真实 mutation 前依次构造 candidate switch state、candidate EA、candidate Effective Grounding，并检查所有 GroundingPoint 和所有设备内置 Effective Grounding。任一 Grounded location 在 candidate EA 下 Energized 即 Reject。

通过后才调用现有真实 switch-state Command。减少 Energized 范围或解除接地事实的操作，不由 GS 阻止。

## 11. Seed / Analysis Guard

Seed Set 作为整体 candidate Scenario 判断。用户显式执行带电分析时，使用完整 candidate Seed Set、当前 topology / switch states 计算 candidate EA，再检查现有 GroundingPoint 和当前 Effective Grounding。任一 grounding location 将 Energized，整次分析 Reject；不得部分接受 Seed。通过后才接受 / 发布正式分析结果。

WP-GS-01 不新增 Transformer / Terminal Seed 类型。

## 12. Transformer Seed Boundary — GS-AF-03

WP-GS-01 不扩展当前 Seed model，当前 Seed 支持范围保持现状。用户 Transformer 反送电不自动推测，不增加 Transformer HV Terminal Seed UI，不增加 typed Terminal Seed persistence。

未来如独立 EA 工作包正式扩展用户侧 Seed，Grounding Safety 再复用同一 Guard。因此本工作包不得因为 Transformer Seed 主动升级 FormatVersion。

## 13. EA Availability Boundary — GS-AF-04

没有有效 EA 时，Grounding Safety 不自行启动隐式 Energization Analysis 来阻止普通设备操作。保持：

- no valid EA → no Energized-based GS switch guard；
- no valid EA → GroundingPoint 保持现有创建能力。

软件不得在用户没有建立有效 EA 的情况下自行推测电源。唯一特殊入口是用户显式点击“带电分析”：此时正在尝试建立新的有效 EA，必须先进行 candidate EA + Grounding Safety validation。

已有有效 EA 时，相关 GroundingPoint / switch operation 才执行 Energized-based safety guard。

## 14. Undo / Redo

不得把 Grounding Safety 业务规则放入通用 `CommandStack`。`CommandStack` 继续负责 history、cursor、dirty state 和 command sequencing。

所有受 GS 管控、且可能通过 Undo / Redo 恢复危险状态的业务 Command，必须在真实 `Undo()` / `Redo()` mutation 前调用同一 Application Grounding Safety Guard。Reject 时 Domain、cursor 和 dirty state 均保持不变。

不得依赖“执行逆操作以后再验证并回滚”的时序实现 GS。

## 15. Topology Editing

WP-GS-01 不把 Grounding Safety Guard 扩大到所有绘图建模操作。例如 create / delete OHL、create / delete Cable、reconnect、device structure edit 继续属于建模行为。

拓扑变化按现有 EA lifecycle 使结果 stale / invalid。用户下一次显式建立有效 EA 时再经过 candidate EA Grounding Safety validation。

## 16. UI

不新增 Safety panel、Warning / Hard Conflict 列表、canvas alarm 或 conflict locator。复用现有 `ShowCommandError` / `IDesktopMessageService.ShowError` 等 blocking error mechanism。

Reject message 至少说明：什么操作不被允许、哪个有效接地点受到影响，以及该操作为什么会造成带地送电或带电接地。

## 17. Persistence

Architecture Freeze 保持 `FormatVersion = V9`。不持久化 EA Result、Candidate State、Candidate EA、Effective Grounding 或 Grounding Safety result。

本轮 Architecture Audit 未证明 V9 必须升级。如实现审计未来发现真正缺失的 persisted business fact，必须停止并返回产品负责人重新讨论，不得自行升级。

## 18. 明确禁止的实现捷径

不得：

1. 先修改真实 SwitchState，再 Analyze，然后恢复。
2. 在失败操作后依赖 Undo 做安全回滚。
3. 把 Grounding Safety 放入 Rendering。
4. 把 GS 逻辑塞进 `EnergizationAnalyzer`。
5. 让 `CommandStack` 直接依赖 EA / Grounding domain。
6. 新增第二套 RingCabinet grounding semantics。
7. 将 Rendering 红色作为 Energized 判断依据。
8. 将 GAP `LineSide` 标签作为唯一权威 identity。
9. 因 GS-01 主动扩展 Transformer Seed。
10. 主动升级 FormatVersion。

## 冻结状态

本文件记录架构与实施边界，不授权本轮以外的生产代码、测试或实现计划工作。WP-GS-01 仍为 NOT IMPLEMENTED；自动化与 Windows acceptance 尚待后续授权的实现阶段。
