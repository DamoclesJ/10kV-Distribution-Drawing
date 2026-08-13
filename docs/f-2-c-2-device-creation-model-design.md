# F-2-C-2 Device Creation Model Design

> 状态：设计阶段；本阶段不修改生产代码、测试、Domain、Persistence 或项目文件。
> 基线：`d246a00 docs: define 10kv device catalog design`。
> 目标：定义用户在画布中插入 10kV 设备后，系统如何一次生成完整且可撤销的 Domain/Layout 对象。

## 1. Context

当前项目已经具备：

- `DrawingDocument` 工程聚合；
- `Device`、`Terminal`、`ElectricalNode`、`Connection` 核心模型；
- Command、Undo/Redo 和 Dirty；
- Project FormatVersion 4；
- TemplateLibrary 和 `Conventional10kVRingCabinet`；
- RingCabinet Template → Domain → Layout → Command 创建链；
- Pole、PoleAttachment、CableTermination compatibility model；
- 10kV Device Catalog 和拓扑边界设计。

当前需要冻结的是“创建编排”，不是重新设计 Domain。一次用户插入动作必须先构造完整、未提交候选，再通过一个 Command 原子加入 Project Runtime。

## 2. Goals

第一版创建模型需要：

1. 区分 Built-in Template、手动插入和未来 User Template 三种来源；
2. 为 Pole 定义可审查的创建方案；
3. 自动生成结构必需的 Attachment、SwitchDevice、Terminal 和 ElectricalNode；
4. 保持 Breaker、LoadSwitch、Disconnector 和 Fuse 的 SwitchKind 区别；
5. 明确每个对象的创建者和 owner；
6. 保证一个用户动作对应一个原子 Command history item；
7. Execute、Undo、Redo 使用首次创建的同一组 Stable IDs；
8. 区分持久工程事实和可重建运行时投影；
9. 暴露当前 Domain/Persistence 与目标模型的真实缺口。

## 3. Non-Goals

本设计不：

- 修改 `Device`、`Terminal`、`Connection` 或 `ElectricalNode`；
- 建立自由设备组合器或任意拓扑编辑器；
- 从 Symbol、位置、连线接触或名称推断 Domain；
- 引入 GIS、SCADA、继电保护或潮流计算；
- 引入 BayFunction、Direction、Role、Incoming、Outgoing 或 Tie；
- 实现 Transformer；
- 实现 User Template editor 或 JSON Template；
- 改写 FormatVersion 4；
- 重构 CommandStack；
- 在本阶段实现代码。

## 4. Creation Sources

### 4.1 Built-in Template Creation

Built-in Template 用于结构固定、已批准的设备聚合。当前代表是 RingCabinet：

```text
Template selection
  → TemplateLibrary lookup
  → RingCabinetTemplateBuildRequest
  → Domain Builder
  → RuntimeLayout Builder
  → Full BuildResult
  → AddRingCabinetCommand
```

Template 决定固定结构；用户只提供实例参数，例如 DisplayName 和 Position。Builder 只执行一次，Redo 不重新 Build。

### 4.2 Manual Device Insertion

手动插入用于参数较少、结构由有限创建方案决定的对象，例如 Pole 及其批准的附属能力。

```text
Catalog item / creation recipe
  → validated creation request
  → creation builder/factory
  → Full CreationResult
  → one Add Command
```

“手动”只表示用户选择设备和有限参数，不表示用户可以自由拼装 Terminal、Node 或 Attachment。

### 4.3 Future User Template

未来 User Template 可以成为新的 Template source，但必须输出与 Built-in Template 相同的已验证 Runtime Model：

```text
User Template Source
  → immutable validated template
  → existing builder boundary
```

User Template 不直接创建 Domain entity、Command、Scene 或 Persistence DTO。未经 schema 验证的自由 JSON 不能绕过 Builder。

### 4.4 Source-Agnostic Domain Result

无论来源如何，进入 Command 前都必须得到完整 Domain/Layout 候选。Domain 和 Persistence 不根据“来源”改变拓扑规则。

TemplateId 或 CreationRecipeId 可以在未来作为可选审计 metadata 设计，但不能成为 Restore、Redo 或拓扑识别的必要输入。

## 5. Creation Request and Result

### 5.1 Request Boundary

创建 Request 只包含用户输入和被选择的稳定定义，例如：

- creation recipe/template；
- DisplayName/PoleNumber；
- physical PoleType；
- SwitchKind；
- initial SwitchState；
- Position 或 attachment offset。

Request 不携带：

- CommandStack；
- DrawingScene；
- SelectionManager；
- Persistence DTO；
- 用户提供的 Stable Entity IDs；
- 自由 Terminal/Node 列表。

### 5.2 Full CreationResult

Builder/Factory 成功时返回完整、只读的候选：

```text
Full CreationResult
  Domain objects
  RuntimeLayout objects
  root selection identity
  required identity invariants
```

复合 Pole 创建结果可包含：

- Pole；
- Pole terminal(s)；
- optional PoleAttachment；
- optional SwitchDevice；
- optional CableTerminal capability；
- capability/switch terminals；
- required ElectricalNode(s)；
- PoleLayout；
- AttachmentLayout 或 Switch layout。

CreationResult 不加入 DrawingDocument、不刷新 Scene、不改变 Selection 或 Dirty。

### 5.3 Failure Boundary

创建失败时：

- 不创建 Command；
- 不进入 History；
- 不改变 DrawingDocument/RuntimeLayout；
- 不改变 Selection/Dirty；
- 不 Rebuild Scene。

类型化输入/结构失败应返回明确 failure；未知编程异常应自然暴露，不伪装成业务失败。

## 6. PoleType and Pole Creation Recipe

### 6.1 Do Not Overload PoleType

当前 Domain `PoleType` 表达杆体物理类型，现有值为 `Cement`。

“普通杆塔、设备杆塔、电缆终端杆塔、变压器杆塔”表达的是创建时需要附带什么能力，不是杆体材质或结构类型。因此不能直接增加为 `PoleType` enum 值。

创建 UI 应区分两个概念：

```text
Pole creation recipe
  OrdinaryPole
  SwitchPole
  CableTerminalPole
  Future TransformerPole

Physical PoleType
  Cement
  future confirmed physical types
```

Creation recipe 属于 Application/Desktop orchestration，不要求持久化为 Pole Domain 属性。恢复工程依赖实际 Pole、Attachment、Device、Terminal 和 Node 事实。

### 6.2 Common Pole Inputs

所有当前可创建 Pole 至少需要：

| Input | Required | Rule |
| --- | --- | --- |
| PoleNumber | Yes | 非空；项目范围内按现有规则生成或验证 |
| DisplayName | Optional | 仅显示文本，不作为 identity |
| PoleType | Yes | 当前为 `Cement` |
| Position | Yes | RuntimeLayout 文档坐标 |
| Creation recipe | Yes | 决定附属结构，不写成 PoleType |

PoleId 由创建 builder 首次生成。调用方不提供 ID。

## 7. Ordinary Pole Creation

### 7.1 Generated Objects

普通杆塔创建：

```text
Pole
  + overhead anchor Terminal
  + PoleLayout
```

第一版延续现有创建行为：生成一个允许 OverheadLine 的 external anchor terminal。若实际线路结构未来需要两个或多个独立 anchor，必须通过明确 recipe/schema 扩展，不能按视觉线段数量自动增加。

### 7.2 Terminal Rule

普通杆塔 anchor terminal：

- owner = Pole；
- role = OverheadAnchor；
- external = true；
- allowed connection = OverheadLine；
- multiple connection policy 由批准 recipe 固定；
- ElectricalNodeId 可以为空，因为 Pole 本体不自动成为导体。

不得为了“看起来连通”自动创建 ElectricalNode 或把多个 Pole anchors 合并。

## 8. Switch Pole Creation

### 8.1 User Inputs

设备杆塔创建需要额外选择：

- SwitchKind；
- initial SwitchState；
- Switch DisplayName；
- optional DispatchNumber；
- 已批准的 attachment offset/layout variant。

允许的第一版 SwitchKind：

| Business type | SwitchKind |
| --- | --- |
| Breaker | `CircuitBreaker` |
| LoadSwitch | `LoadSwitch` |
| Disconnector | `IsolationSwitch` |
| Fuse | `DropoutFuse` |

四者使用统一 `SwitchDevice` 数据结构，但必须保持不同 Kind。UI 不能只显示“开关”并丢失类型，也不能因第一版操作行为类似而合并。

### 8.2 Generated Objects

```text
Pole
  + PoleAttachment
      + SwitchDevice(InstallationType = Pole)
          + terminal A
          + terminal B
  + PoleLayout
  + attachment/switch layout
```

一次创建必须生成 Pole、SwitchDevice、两个 switch terminals、PoleAttachment 和所有 Layout。它们组成一个 Full CreationResult。

### 8.3 Terminal and Node Rule

柱上 SwitchDevice 两端 terminal：

- owner = SwitchDevice；
- external = true；
- allowed connection = OverheadLine；
- 默认不允许多重连接，除非未来批准具体分支结构；
- terminal A/B 必须使用不同 Stable IDs；
- 不自动创建同一 ElectricalNode 将两端固定连接。

SwitchDevice 的 Open/Closed 状态决定两端局部导通。若把两端放在同一个 ElectricalNode，Open 状态将失去意义，因此这是禁止结构。

对简单串联设备杆，两个 switch terminals 是线路两侧 endpoint。不要同时自动创建一个绕过 SwitchDevice 的 Pole anchor connection。若杆位还需要独立分支 anchor，必须由另一个明确 recipe 定义。

### 8.4 State Rule

初始状态必须由用户明确选择或由批准 recipe 提供可见默认值。默认值不能从 Symbol、设备名称或左右位置推断。

创建后状态修改必须走 Domain 操作和 Command。Attachment 不保存或复制 SwitchState。

### 8.5 Current Implementation Gap

当前 `SwitchDevice` 构造器为 internal，且柱上 SwitchDevice 缺少完整创建 Factory/Command/Rendering/Persistence 闭环。V4 mapper 明确不支持 top-level SwitchDevice。

因此 SwitchPole recipe 在完成以下工作前只能作为设计：

- Domain-approved pole SwitchDevice factory；
- atomic add/remove operation；
- state Command；
- RuntimeLayout/Symbol；
- new persistence format and V4 migration；
- full Stable ID round-trip tests。

## 9. Cable Terminal Pole Creation

### 9.1 Target Generated Objects

目标模型：

```text
Pole
  + PoleAttachment
      + CableTerminal capability
          + cable-side terminal
          + overhead-side terminal
          + fixed internal ElectricalNode
  + PoleLayout
  + AttachmentLayout
```

CableTerminal 是连接端子能力，不是 Device，不拥有 SwitchState。

### 9.2 Terminal and Node Rule

cable-side terminal：

- owner = CableTerminal capability；
- external = true；
- allowed connection = Cable；
- 默认单连接；
- ElectricalNodeId = capability internal node。

overhead-side terminal：

- owner = CableTerminal capability；
- external = true；
- allowed connection = OverheadLine；
- 默认单连接；
- ElectricalNodeId = 同一 capability internal node。

Internal ElectricalNode 表达电缆侧和架空侧固定导通。两侧之间不创建 `Connection`，也不创建虚拟 Cable。

### 9.3 Current Compatibility Model

当前代码通过 `CableTermination : Device`、两个 terminals、Intermediate node 和 PoleAttachment 创建相同的拓扑效果。这是 V4 历史兼容实现，不是最终目录分类。

F-2-C-2 不授权直接用旧 Device model 实现新的目标 UI，也不授权静默修改 V4。正式 CableTerminalPole 创建应在独立迁移完成后接入。

迁移必须保留：

- existing CableTermination identity 的明确映射；
- AttachmentId；
- both TerminalIds；
- InternalNodeId；
- existing Connection endpoints；
- Layout/Selection identity；
-旧 V4 文件可读性。

## 10. Future Transformer Pole

TransformerPole 只作为未来 creation recipe 名称保留，不进入当前选择列表，也不创建空对象。

未来必须先完成独立设计：

- Transformer 是否为独立 Device；
- high/low-side terminals；
- internal topology；
- PoleAttachment ownership；
- Rendering symbol/layout；
- Command 与 Persistence；
- 10kV/低压侧工作票语义。

当前不得：

- 新增 Transformer Domain placeholder；
- 将 Transformer 放入 SwitchKind；
- 创建没有 Domain owner 的 terminals；
- 将 TransformerPole 保存为一个虚假的 PoleType。

## 11. Automatic Attached-Object Generation

### 11.1 Approved Recipe Only

自动对象只能来自已批准创建方案。创建 builder 不根据 DisplayName、图标、位置或用户先后点击推断附属结构。

| Recipe | Automatically generated domain objects |
| --- | --- |
| OrdinaryPole | Pole + overhead anchor terminal |
| SwitchPole | Pole + PoleAttachment + SwitchDevice + 2 switch terminals |
| CableTerminalPole | Pole + PoleAttachment + CableTerminal capability + 2 terminals + internal node |
| TransformerPole | Future only; no current generation |

### 11.2 No External Connection Generation

插入设备时只创建设备内部必需结构，不自动创建 Cable 或 OverheadLine。

外部 Connection 必须由后续用户操作明确选择两个 endpoints，并经过 `DrawingDocument.AddConnection()` 校验。设备 proximity、同一 Pole ownership 或 attachment relationship 都不能自动产生 Connection。

### 11.3 No Half-State Return

如果任一 ID、owner、Terminal policy、Node 或 Layout 构造失败，CreationResult 整体失败。不得返回“Pole 成功但 Attachment 失败”的半结果。

## 12. Terminal Creation Responsibility

Terminal 由拥有其固定结构知识的 builder/factory 创建：

| Owner/structure | Terminal creator |
| --- | --- |
| RingCabinet | Existing RingCabinet Domain factory via Template/Manual definition |
| Ordinary Pole | Pole creation builder using Pole terminal API |
| Pole SwitchDevice | approved pole-switch Domain factory/builder |
| CableTerminal capability | dedicated capability factory after migration |
| Transformer | future dedicated factory |
| Cable/OverheadLine | Do not create terminals; reference existing endpoints |

Desktop UI 不直接 `new Terminal`，不提供 arbitrary role、owner、AllowedConnectionTypes 或 ElectricalNodeId。

Builder 必须建立并校验双向事实：

- owner 声明 Terminal ID；
- Terminal.OwnerId 指向 owner；
- Terminal.ElectricalNodeId 指向存在的 node（如需要）；
- ElectricalNode.TerminalIds 包含对应 terminal；
- Terminal policy 与结构用途一致。

## 13. ElectricalNode Creation Responsibility

ElectricalNode 只在表达固定内部等电位关系时自动创建。

| Structure | Automatic ElectricalNode |
| --- | --- |
| RingCabinet | Yes；MainBus/Circuit/Earth/Intermediate by fixed IntervalKind |
| Ordinary Pole anchor | No by default |
| Pole SwitchDevice terminals | No shared node between switch sides |
| CableTerminal capability | Yes；one Intermediate node shared by both terminals |
| Cable/OverheadLine | No；external connection is a Connection, not a node |
| Transformer | Future dedicated topology |

SwitchDevice Closed 时两端导通属于动态图边，不通过创建或合并 ElectricalNode 表达。Open/Closed 不应改变 Node identity。

## 14. Stable ID Generation

### 14.1 Generate Once

创建 builder/factory 首次 Build 时生成所有 Domain/Layout Stable IDs：

- Device IDs；
- Attachment IDs；
- Terminal IDs；
- ElectricalNode IDs；
- SwitchAssembly IDs；
- RuntimeLayout owner IDs。

同一次创建中，引用关系必须使用这唯一一组 IDs。

### 14.2 No Deterministic Derivation from UI Data

不得从以下数据派生 ID：

- PoleNumber；
- DisplayName；
- Position；
- list index；
- TemplateId；
- SwitchKind；
-当前时间字符串。

两个独立 Create 产生不同实例 IDs；Undo/Redo 不生成新 IDs。

## 15. Command Atomicity

### 15.1 One User Action, One History Item

复合设备插入必须使用一个 Command 保存完整 CreationResult：

```text
User confirms insertion
  → Build once
  → Create one Add command
  → CommandStack.ExecuteCommand(command)
```

不应依次执行：

```text
AddPoleCommand
AddSwitchCommand
AddAttachmentCommand
AddTerminalCommand
```

否则一次用户动作会产生多个 History 项，并可能留下半状态。

### 15.2 Execute

Execute 应原子加入：

- 所有 Domain objects；
- 所有 required Terminal/Node/Attachment；
- 所有 RuntimeLayout objects。

如果 Layout 添加失败，必须回滚已添加 Domain aggregate。CommandStack 只在 Execute 完成后登记历史和 Dirty。

### 15.3 Undo

Undo 应：

- 拒绝删除仍被 Connection/Professional data 引用的结构，或由更高层提前协调删除；
- 移除完整 Domain aggregate；
- 移除对应 RuntimeLayout；
- 恢复创建前 SelectionTransition；
- 不销毁 Command 内保存的对象。

### 15.4 Redo

Redo 重新加入首次 Build 的同一对象和 Layout：

- 不重新调用 creation builder；
- 不重新生成任何 ID；
- 不根据当前 UI 参数重建；
- 恢复同一 after-selection identity。

## 16. Selection and Scene Boundary

创建编排顺序沿用现有 Template Creation 原则：

```text
capture before selection
  → build full candidate
  → execute actual command
  → record SelectionTransition.ForAdd
  → prune transition history
  → rebuild scene once
  → select root created object
  → SceneChanged
```

root selection：

- RingCabinet recipe → RingCabinet；
- Pole recipes → Pole；
- 在已有 Pole 上单独添加 attachment → attached SwitchDevice 或 CableTerminal capability，按未来 Selection contract 决定。

Build 或 Command 失败时不改变 Selection、Scene 或 Inspector。

## 17. Persistence Boundary

### 17.1 Persisted Project Facts

成功执行后，以下实际工程事实需要持久化：

- Pole、RingCabinet、SwitchDevice Stable IDs；
- physical PoleType、PoleNumber、DisplayName；
- SwitchKind、SwitchInstallationType、SwitchState；
- PoleAttachment/capability ownership；
- Terminal IDs、roles、owners、policies；
- ElectricalNode IDs、types、owners；
- Connection endpoint IDs；
- Domain-specific fixed structure；
- RuntimeLayout positions、offsets 和 identity。

### 17.2 Runtime-Derived Data

以下可在打开工程后重建，不应作为 Domain 事实重复保存：

- DrawingScene elements；
- HitTest index；
- SelectionResolver cache；
- Inspector projection；
- electrical connectivity query graph；
- derived reachable set/path；
- TemplateLibrary instances；
- creation dialog state；
- creation builder/factory object。

### 17.3 Creation Recipe Persistence

Creation recipe 只负责生成实际对象。只要实际 Domain/Layout 结构完整，Project Restore 不需要知道它来自 OrdinaryPole、SwitchPole 或 Built-in Template。

未来若需要审计来源，可以单独设计 optional creation metadata；它不能成为恢复或 Redo 必需数据。

### 17.4 Format Version Impact

当前能力：

- RingCabinet：V4 supported；
- Ordinary Pole：V4 supported；
- current CableTermination-as-Device attachment：V4 supported historical model。

目标能力缺口：

- top-level/Pole SwitchDevice：V4 mapper 不支持；
- CableTerminal capability：V4 没有非 Device owner/attachment contract。

正式实现 SwitchPole 或目标 CableTerminalPole 前，需要独立新格式设计和 V4 migration。不得静默改变 Version4 DTO 含义。

## 18. Built-in Template and Manual Creation Relationship

RingCabinet 的主要创建入口应继续使用 Built-in Template：

```text
TemplateLibrary
  → Build Coordinator
  → Full BuildResult
  → AddRingCabinetCommand
```

现有手工 RingCabinet 创建链可以保持兼容，但不应与 Pole recipe 在 F-2-C-2 中统一重构。

Pole 等有限结构使用 manual creation recipe。二者可以共享“Build before Command、one action one history item、Stable ID once”的执行原则，但不需要提前抽象一个通用万能 Builder interface。

## 19. Future User Template Boundary

未来 User Template 若扩展到 Pole assembly，必须先定义稳定、受限 schema。例如只允许选择批准的 SwitchKind 和 terminal topology；不能上传任意 Node/Connection 图。

进入运行时前必须：

- schema validation；
- capability support validation；
- immutable materialization；
- full Domain/Layout BuildResult；
-同一 Command atomicity。

用户模板不允许：

- 指定 Domain Stable IDs；
- 直接写 Persistence DTO；
- 执行 Command；
- 注入 WPF symbol types；
- 绕过 Terminal owner/connection policy；
- 创建自由 CAD 拓扑。

## 20. Implementation Slices

建议后续按编译闭环拆分：

### F-2-C-2-A Ordinary Pole Creation Alignment

- 明确 Pole request/result；
- 保留现有 Pole + anchor terminal + layout；
- 验证 atomic Command、Selection 和 Stable ID。

### F-2-C-2-B Pole Switch Domain Creation Design

- 冻结 pole SwitchDevice factory；
- terminal policies；
- state operation；
- layout/symbol contract；
- 不先改生产代码。

### F-2-C-2-C Persistence V5 Design

- top-level SwitchDevice；
- capability-aware PoleAttachment；
- CableTerminal migration；
- V4 compatibility 和 Stable IDs。

### F-2-C-2-D Pole Device Atomic Implementation

- Full CreationResult；
- one Add command；
- SwitchPole create/undo/redo；
- Scene/Selection/Inspector；
- V5 round-trip。

### F-2-C-2-E CableTerminal Capability Migration

- 旧 CableTermination Device 兼容迁移；
- target capability implementation；
- connection/layout/selection preservation。

不要在一个切片同时实现 Transformer、User Template、Cable UI 和自由设备组合。

## 21. Risks and Guardrails

- 把 creation recipe 写入 PoleType 会混淆物理类型与附属结构；
- 用多个 Command 实现一次复合插入会造成半状态和错误 Undo；
- Redo 重新 Build 会改变 Stable IDs；
- 为 Switch 两端创建同一 Node 会绕过 Open 状态；
- Attachment 被当作 Connection 会混淆安装和导通；
- 插入设备时自动创建外部线路会产生未经用户确认的拓扑；
- UI 直接构造 Terminal/Node 会复制 Domain invariant；
- 继续扩展 `CableTermination : Device` 会加深与冻结目录的差异；
- 未设计迁移就保存柱上 SwitchDevice 会破坏 V4；
- Transformer 空占位会污染当前 Domain 和 Persistence。

## 22. Final Decision

第一版设备创建模型采用：

```text
validated source
  → approved template or creation recipe
  → build complete Domain + Layout candidate once
  → one atomic Add command
  → CommandStack / SelectionTransition / Scene
```

Pole 的 `PoleType` 只表达物理杆型；OrdinaryPole、SwitchPole、CableTerminalPole 和未来 TransformerPole 是创建方案，不是 Domain PoleType。

普通杆自动生成 Pole、anchor Terminal 和 Layout。设备杆自动生成 Pole、PoleAttachment、保留明确 SwitchKind 的 SwitchDevice、两个 terminals 和 Layout。电缆终端杆目标上生成 Pole、PoleAttachment、CableTerminal capability、两个 terminals、一个固定内部 ElectricalNode 和 Layout，但必须等待目标能力及 Persistence migration 完成。TransformerPole 仅为未来边界，不进入当前实现。

所有 Stable IDs 在首次 Build 时生成一次；Execute、Undo、Redo 使用同一对象和 ID。外部 Cable/OverheadLine 不随设备插入自动生成。设计继续复用现有 Device、Terminal、Connection 和 ElectricalNode，不引入 GIS、SCADA、继保、潮流或自由 CAD 拓扑。
