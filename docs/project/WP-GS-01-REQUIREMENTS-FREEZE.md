# WP-GS-01 — Grounding Safety & Energization Conflict

> 状态：OPEN / REQUIREMENTS FROZEN / NOT IMPLEMENTED / WINDOWS ACCEPTANCE PENDING
> 冻结日期：2026-10-02
> 需求来源：产品负责人已确认并正式冻结的 WP-GS-01 Requirements Freeze（下文保留全部 23 项业务需求）。
> 原冻结轮授权：仅文档落盘与治理状态更新，不进入生产代码实现，不升级 FormatVersion。后续用户授权先将该纯文档冻结变更独立提交，再执行只读架构审计。

## 正式基线与本轮只读核对

- 启动分支：`main`。
- `HEAD = origin/main = 2162ed17a0ee264ea3e211c2193fd29e3099af57`；只读 `git ls-remote origin refs/heads/main` 核对远端 `main` 为同一 SHA。
- 启动时 working tree：clean。
- 工作分支：`wp-gs-01`，从上述正式基线创建。
- `FormatVersion = V9`；代码依据为 `ProjectFileFormat.CurrentVersion = Version9`。
- `WP-WTA-01 = CLOSED / ACCEPTED`。
- `WP-EA-01A = CLOSED / ACCEPTED`。
- `WP-EA-01B = CLOSED / ACCEPTED`。
- 启动时 `Current Work Package = None`；本次独立启动 WP-GS-01，不存在正式 WP-EA-01C，不重新打开上述已关闭工作包。

## 文档依据与契约边界

本工作包沿用 [Development Workflow](WORKFLOW.md) 的独立工作包、只读审计、评审后提交及 Windows 验收流程；治理入口为 [Current Status](STATUS.md) 与 [Roadmap](ROADMAP.md)。

已审阅的相关正式文档：

- [WP-EA-01B Closure](WP-EA-01B-CLOSURE.md)：当前权威的 Seed 二态分析契约与正式 EA 生命周期。
- [WP-EA-01B Binary Model Implementation Report](WP-EA-01B-BINARY-MODEL-IMPLEMENTATION-REPORT.md)：成功分析无 Unknown，失败不发布部分点状态，结果不持久化。
- [WP-EA-01A Closure Report](WP-EA-01A-CLOSURE.md)：EA 分层、精确 Boundary / Side、GroundSwitch 不参与普通带电传播与 V9 基础；其中历史 completeness / Unknown 产品语义已由 EA-01B 正式契约取代，不作为 GS-01 新需求。
- [Post-V1 Electrical Model Closure](POST_V1_ELECTRICAL_MODEL_CLOSURE.md) §3.3.5、§3.4–3.8：Terminal / GAP typed target、真实导体 half-edge、柱上设备两侧及接地事实与显示分离。
- [Electrical Connectivity Graph Design](../p0-7-c-4-a-electrical-connectivity-graph-design.md)：Terminal 身份、Node / Connection / Switch 导通关系及只读快照边界。
- [RingCabinet Model Design](../ring-cabinet-design.md)：设备接地结构与原始开关状态、有效接地派生语义及现有联锁边界。
- [Equipment Model](../equipment-model.md) 与 [Professional Object Model Design](../distribution-professional-object-model-design.md) §5、§9.3：Domain 事实、Layout / Rendering 分离和派生结果不持久化；较早的 Terminal-only 描述以闭合 Electrical Model 的 typed GroundingTarget 合同为准。

本轮只读代码核对确认现有 `GroundingTarget` 包含 Terminal / GroundingAccessPoint；GAP 保存 Connection、Pole、typed AdjacentEndpoint、LineSide 与 PlacementSide；当前格式为 V9。这不是 GS 实现完成、候选操作安全性验证或 persisted facts 充分性的完整代码审计。后续实现前应审计精确映射、候选计算与受控操作入口，按以下冻结需求实施，不修改历史闭合文档或发明新的接地模型。

## 本轮交付与后续验收状态

- 本轮交付：本 Requirements Freeze 文档及 STATUS / ROADMAP 的最小治理更新。
- 代码实现：未开始；本轮不修改生产代码、测试、UI 或保存格式。
- 自动化测试与 Windows GUI acceptance：属于冻结后的实现验收范围，尚待完成；本轮文档落盘不构成实现或 Windows 验收通过。
- Windows 验收必须针对已提交并推送的候选版本，按现有 WORKFLOW 执行。
- 完成本轮文档落盘后停止；后续实现授权与验收不由本轮自动启动。

# WP-GS-01 Requirements Freeze

## 一、核心安全不变量

软件以用户指定的 Seed、当前拓扑和当前设备状态确定 Energized 区域。

不得在 Energized 的精确导电位置建立有效接地。

不得通过任何受控操作使已经有效接地的精确导电位置进入 Energized。

## 二、职责分层

1. Energization Analysis
只负责根据 Seed + 当前局部拓扑 + 当前开关状态，确定性计算 Energized / Deenergized。

不推测图外电源。
不引入 Unknown。
不负责接地安全判断。

2. Grounding Facts
保存明确的接地事实和设备原始状态，包括：

- GroundingPoint；
- GroundSwitch state；
- Breaker state；
- 设备接地结构。

3. Grounding Safety
使用 EA Result + Grounding Facts / Effective Grounding 做接地安全检查和操作闭锁。

不得把三层重新合并成一个 Analyzer。

## 三、判断单位

Grounding Safety 判断的是 GroundingTarget 实际对应的精确导电位置，而不是整个设备、整个杆塔或图元颜色。

柱上开关两侧必须分别判断。

一侧 Energized 不自动导致另一侧不能接地。

## 四、GroundingTarget → EA 映射

Terminal：
映射到该 Terminal 对应的准确 electrical identity / side。

GAP：
映射到其实际所在的 OHL conductive segment。

柱上开关附近 GAP：
必须明确属于 LargerNumber / SmallerNumber 等准确电气侧。

合法 GroundingTarget 必须能够唯一映射到 EA 已有 electrical identity。

映射失败属于模型完整性问题，不得自动解释为 Deenergized，也不得重新引入 EA Unknown。

## 五、GroundingPoint 新增

无有效 EA：
保持现有 GroundingPoint 创建能力，不增加带电闭锁。

Valid EA + Target Deenergized：
Allow。

Valid EA + Target Energized：
Reject。

Reject 时：

- No Domain mutation；
- No GroundingPoint created；
- No Undo Stack entry；
- No dirty-state change。

只需要简单明确弹窗，不增加安全报警面板。

## 六、Effective Grounding

Effective Grounding 是根据设备结构和当前设备状态实时推导出的电气事实。

不是新的 persisted fact。

不得为其单独升级 FormatVersion 或新增 V9 保存字段。

该派生能力应属于电气模型可复用能力，Grounding Safety 使用它。

## 七、环网柜电缆接地

环网柜内置接地的业务目标统一为：
给该间隔电缆形成有效接地。

Grounding Safety 最终检查该间隔 CableSide / cable electrical identity。

设备结构规则：

1. 普通负荷开关间隔：
GroundSwitch Closed
→ Cable Effective Grounding = true。

2. 一二次融合，上刀下接地：
GroundSwitch Closed
→ Cable Effective Grounding = true。

3. 一二次融合，下刀下接地：
GroundSwitch Closed
→ Cable Effective Grounding = true。

4. 一二次融合，上刀上接地：
只有
GroundSwitch Closed + Breaker Closed
才有：
Cable Effective Grounding = true。

仅 GroundSwitch Closed + Breaker Open 时，电缆尚未形成有效接地。

## 八、环网柜带电操作

如果 Cable EA = Energized：

普通负荷开关 / 上刀下接地 / 下刀下接地：
GroundSwitch Open → Closed
必须 Reject。

上刀上接地：
如果 GroundSwitch Closed、Breaker Open，当前可以存在。

随后 Breaker Open → Closed 若将形成：
Energized + Effective Grounding
则 Reject。

反过来 Breaker 已 Closed 时再合 GroundSwitch，若形成同样结果，也 Reject。

Grounding Safety 判断候选状态是否形成 Energized + Effective Grounding，而不是简单判断“GroundSwitch 是否 Closed”。

## 九、架空线路工作接地

无论接地点是：

- GAP；
- 验电接地环；
- 开关杆某一侧；
- 电缆杆电缆端子；
- 其他合法 GroundingTarget；

均只判断该实际接地点对应的精确导电位置是否 Energized。

不增加“整个杆塔是否允许登杆”等现场安全推断。

## 十、用户变压器反送电

不新增自动推测用户站反送电能力。

继续遵守 EA Seed 权威原则。

用户侧未配置 Seed：
软件不自行推测反送电。

用户侧被人工明确配置为 Seed：
进入普通 EA + Grounding Safety 规则。

## 十一、已有接地后的送电闭锁

已有：

- GroundingPoint；
或

- 设备内置 Effective Grounding；

任何可能向其送电的受控状态变化，在真实执行前必须进行 candidate / hypothetical EA。

如果候选状态下任意已有效接地位置将变为 Energized：
Reject 整个操作。

不得先执行操作再产生 Hard Conflict。

## 十二、普通开关操作

Open → Closed 等可能扩大 Energized 可达范围的操作，需要候选状态安全检查。

若会使现有 GroundingPoint 或设备内置有效接地点 Energized：
Reject。

不建立“最后一个闸”的特殊模型。
最终判据始终是候选操作是否让有效接地点 Energized。

减少 Energized 范围的操作原则上不被 GS 阻止。

## 十三、Seed / 带电分析

用户选择或修改 Seed 后执行带电分析时：

使用完整候选 Seed Set + 当前 topology + 当前 switch states 进行 candidate EA。

如果候选 EA 会使任何已有有效接地点 Energized：
整次带电分析 Reject。

不得部分接受部分 Seed。

提示用户先确认并解除相关接地措施后重新分析。

删除 Seed 属于减少电源范围，Grounding Safety 不阻止。

## 十四、解除危险方向的操作

Grounding Safety 不阻止：

- 删除 GroundingPoint；
- GroundSwitch Closed → Open；
- 普通开关 Closed → Open；
- 删除 Seed；

即不阻止减少 Energized 可达范围或解除接地事实的操作。

## 十五、Undo / Redo

Undo / Redo 不得成为绕过 Grounding Safety 的路径。

如果 Undo / Redo 恢复后的候选状态会形成 Energized + Effective Grounding：
Reject。

例如：

- 恢复一个会向 GroundingPoint 送电的 Closed Switch；
- 在当前 Energized Target 上通过 Undo 恢复已删除 GroundingPoint。

Reject 时：

- Domain state unchanged；
- Undo / Redo cursor unchanged。

## 十六、Candidate / Hypothetical EA

Grounding Safety 的预判计算只服务于安全判断。

candidate EA 不得：

- 修改正式 Seed；
- 修改真实 SwitchState；
- 修改 Grounding facts；
- 修改图形显示；
- 写入 persistence；
- 进入 Undo；
- 造成 project dirty。

检查通过后才执行真实 Command，再按现有正式 EA 生命周期处理结果。

## 十七、项目加载

旧 V9 项目即使保存的数据组合在新 GS 规则下可能存在风险，也不得因此拒绝项目加载或自动修改事实。

正常加载：

- GroundingPoint；
- GroundSwitch；
- Breaker；
- Seed；
- topology。

用户重新执行带电分析时再进行 Grounding Safety 检查。

如现有配置会向接地点送电，则本次分析 Reject，并给出简单提示。

## 十八、UI / UX

第一版只做最小交互。

不新增：

- 安全冲突面板；
- Warning / Hard Conflict 分类；
- 图上报警标记；
- 冲突定位系统；
- 复杂安全可视化。

正常操作通过时不弹额外提示。

危险操作：
Reject + 简单明确提示。

提示至少表达：

- 什么操作被禁止；
- 哪个接地点受影响；
- 为什么被禁止。

## 十九、Grounding Safety 与 Device Interlock 边界

GS-01 只处理与 Energized + Grounded 直接相关的安全闭锁。

不处理：

- 完整五防；
- 隔离刀闸与断路器的标准操作顺序；
- 机械联锁；
- 柜型完整联锁；
- 操作票步骤；
- 其他与接地安全无关的设备状态限制。

## 二十、拓扑编辑

GS-01 不扩大到禁止正常图纸建模行为，例如新增/删除 OHL、Cable、连接或设备。

拓扑变化继续遵守现有 EA 生命周期。

重新建立有效 EA 时再执行 Grounding Safety 检查。

## 二十一、Persistence

优先保持 FormatVersion = V9。

不持久化：

- EA Result；
- Effective Grounding；
- candidate EA；
- Grounding Safety result。

只有代码审计证明当前 persisted facts 无法表达已冻结业务事实时，才允许重新提出 FormatVersion 升级讨论；不得自行升级。

## 二十二、WTA 边界

本工作包不直接生成或修改：

- 6.1；
- 6.3；
- 6.4；
- 16.1。

但 GroundingPoint、EA Result、Effective Grounding 等能力不能阻碍未来：

- GroundingPoint → 6.3；
- GroundingPoint → 16.1；
- EA + WorkScope → 6.4。

## 二十三、当前范围

In Scope：

- GroundingTarget → EA 精确映射；
- Terminal / GAP；
- GroundingPoint Energized Reject；
- Effective Grounding；
- 环网柜各种接地结构；
- 柱上开关两侧判断；
- 已有接地后的送电闭锁；
- Seed candidate safety check；
- Undo / Redo safety guard；
- 最小提示 UI；
- 自动化测试；
- Windows GUI acceptance；
- 优先保持 V9。

Out of Scope：

- WorkScope；
- 工作票自动生成；
- 6.1 / 6.3 / 6.4 / 16.1；
- 完整 Device Interlock；
- 操作票步骤；
- 图外电源推测；
- SourceSetComplete；
- Unknown；
- EA 路径解释；
- EA-aware PNG；
- 安全冲突面板；
- 复杂报警可视化；
- 新 Grounding Model。
