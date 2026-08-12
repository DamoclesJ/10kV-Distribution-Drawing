# P0-7-B Template Builder 架构设计

> 状态：Builder 架构设计稿；不包含生产代码、Template Runtime、UI、JSON 或持久化实现。<br>
> 基线：checkpoint commit `c1acd849f164bf71f47d4fd8fe1eb41ae1684283`。<br>
> 上游设计：`docs/template-system-design.md`、`docs/p0-7-a-review.md`。<br>
> 事实边界：当前 Domain 支持 LoadSwitchInterval 与 IntegratedFeederInterval，当前 RuntimeLayout 支持对应 RingCabinetLayout；PTInterval 与 DTU 专用运行时模型尚未实现。

## 1. Builder 总体目标

Template Builder 的目标是把经过校验的模板描述转换成项目可直接使用的真实电气对象和匹配的运行时布局。

```text
Template
    |
    v
Builder
    |
    +--> Domain Objects
    |
    +--> RuntimeLayout
    |
    v
Existing Rendering
```

Builder 负责创建完整对象、建立受控关系、分配首次创建所需的 Stable ID，并返回尚未加入工程的完整创建结果。调用方再通过现有 Command 工作流原子提交该结果。

Builder 不直接生成 `DrawingScene`，也不创建 `SceneElement`、Symbol、WPF Shape 或像素图形。Builder 不负责：

- Rendering；
- Scene rebuild；
- Selection UI 或 SelectionTransition；
- Property Inspector 投影；
- CommandStack 历史、Undo/Redo 或 Dirty；
- Persistence、Save/Load 或 FormatVersion。

Builder 的输出必须能够被现有 Rendering 消费，但 Builder 不调用 Rendering 来补充 Domain 事实。

## 2. Template 与 Builder 边界

Template 是描述，Builder 是执行。

Template 负责描述：

- `CabinetType`：模板分类和默认策略来源；
- `Bays`：有序的一次系统 Bay 描述；
- `BayFunction`：每个 Bay 的电气功能；
- `EquipmentConfiguration`：每个 Bay 的受控设备组合与必要创建参数；
- `SecondaryConfiguration`：DTU 等二次配置描述；
- `LayoutRule`：布局规则或规则引用。

Template 不包含项目对象实例、Stable ID、工程引用、最终坐标、Command、SelectionReference 或 Rendering 类型。

Builder 负责：

1. 校验 Template 的结构完整性；
2. 校验 `BayFunction` 与 `EquipmentConfiguration` 是否属于当前明确支持的映射；
3. 将 Bay 描述映射为 Domain 工厂所需的创建定义；
4. 调用受控 Domain 工厂创建完整聚合；
5. 由已创建的真实聚合及其 Stable ID 生成 RuntimeLayout；
6. 验证 Domain 根对象与 Layout 根 ID 一致；
7. 返回不可变、未提交的创建结果。

Builder 不应直接向 `DrawingDocument` 或 `RuntimeLayoutDocument` Add 对象。提交是 Command 的职责。Builder 也不应依据 `DisplayName`、CabinetType 或 LayoutRule 猜测 Bay 的设备结构。

## 3. Builder 输入模型

建议采用显式的 `TemplateBuildContext`，概念结构如下：

```text
TemplateBuildContext
├── Template
├── RootDisplayName
├── PlacementOrigin
├── StableIdGenerator
├── LayoutRuleResolver
└── CreationOptions
```

### 3.1 必需输入

| 输入 | 是否必需 | 职责 |
| --- | --- | --- |
| `RingCabinetTemplate` | 是 | 提供 CabinetType、Bays、SecondaryConfiguration 与 LayoutRule |
| `RootDisplayName` | 是 | 提供要写入 Domain 根对象的明确名称；不得从模板目录名称隐式推断 |
| `PlacementOrigin` | 是 | 提供本次实例在文档毫米坐标中的放置原点；它是调用参数，不是 Template 内容 |
| `StableIdGenerator` | 是 | 为本次创建分配唯一 Stable ID；生产默认实现可使用随机 GUID |
| `LayoutRuleResolver` | 是 | 将 LayoutRule 引用解析为受控布局策略，不返回 Scene 或 WPF 类型 |
| `CreationOptions` | 条件必需 | 仅容纳 Domain 工厂创建所需、已确认的技术参数，例如合法初始 SwitchState |

`CreationOptions` 必须保持窄小。技术初始化状态不等于用户确认的现场运行状态，不能借此加入操作状态机、自动编号、厂家参数或未经确认的专业规则。

### 3.2 不应进入 Builder 的信息

Builder 不应接收：

- `ProjectRuntimeSession`；
- 当前 `DrawingDocument` 或可写 `RuntimeLayoutDocument`；
- `CommandStack`；
- `SelectionManager` 或当前 Selection；
- `PropertyInspector`；
- Scene、Viewport、WPF Control 或像素坐标；
- Persistence DTO 或工程文件版本；
- 当前 UI Dialog / ViewModel 状态；
- Undo/Redo 历史。

原因是 Builder 应为无会话副作用的创建服务：相同的已冻结输入应创建一个完整候选结果，但不改变当前工程。工程冲突校验（例如根 ID 已存在）由创建 Command 在提交前结合当前 Document/Layout 检查。

### 3.3 ID Generator 与 Layout Generator 的边界

`StableIdGenerator` 是 Builder 的基础依赖，便于测试时提供可预测 ID，但 Template 不持有或选择 ID。

布局生成不建议以可随意替换的“任意 Layout Generator”进入 Template。建议由 Builder 依赖受控 `LayoutRuleResolver`，解析出与模板类型和当前 RuntimeLayout 类型匹配的初始布局策略。策略读取实际 Domain 聚合，不能只读取设备清单。

## 4. Builder 输出模型

Builder 不能返回 `void`，因为调用方需要在不重新执行 Builder 的情况下：

- 创建原子 Add Command；
- 获取根对象和布局；
- 在 Command 成功后选择根对象；
- Undo/Redo 时复用同一对象和 Stable ID；
- 对创建结果执行提交前一致性校验。

建议针对当前 Ring Cabinet 使用类型化输出，而不是无约束对象袋：

```text
TemplateBuildResult<RingCabinet, RingCabinetLayout>
├── RootObject: RingCabinet
├── RootLayout: RingCabinetLayout
└── RootIdentity
    ├── TargetKind: RingCabinet
    └── ObjectId: RingCabinet.Id
```

输出职责建议如下：

| 候选字段 | 建议 | 原因 |
| --- | --- | --- |
| Created Root Object | 必须 | RingCabinet 是当前完整聚合根，也是 Command 提交单位 |
| Created Layout Object | 必须 | Add Command 需要与根对象 ID 匹配的 RingCabinetLayout |
| Created Domain Objects 列表 | 不单独保存 | 内部 Interval、Switch、Terminal、Node 已由 RingCabinet 聚合拥有或可枚举；重复列表易形成第二事实源 |
| Created Layout Objects 列表 | 不单独保存 | Interval/Switch 子布局已经包含在 RingCabinetLayout 中 |
| Created Root Object ID | 可提供只读便利投影 | 便于调用方建立 Selection，但必须从 RootObject 派生 |
| Created Selection Target | 不建议由 Builder 创建 | SelectionReference 属于编辑器层；Builder 只提供根身份，Desktop 在 Command 成功后构造 Selection |

`TemplateBuildResult` 是创建过程的不可变结果包装，不是新的 Domain 聚合，不进入 Persistence，也不在生成后持续参与 Rendering。

## 5. Stable ID 生成策略

模板生成的每个真实对象都必须在首次 Build 时获得 Stable ID，包括 RingCabinetId、IntervalId、SwitchId、TerminalId、ElectricalNodeId、SwitchAssemblyId，以及未来可能出现的 AttachmentId。

### 5.1 方案 A：随机 GUID

每次 Build 使用 ID Generator 生成随机 GUID。优点：

- 与当前项目大量使用 `Guid.NewGuid()` 的事实一致；
- 不依赖 Template 内容、Bay 顺序或实例名称；
- 不同项目和重复实例之间碰撞概率可忽略；
- 模板变化不会改变已经创建对象的 ID。

缺点是单元测试需要可注入 ID Generator 才能方便断言完整映射。

### 5.2 方案 B：TemplateInstanceId + 序号

以 TemplateInstanceId 和对象序号派生 ID。优点是可预测；缺点包括：

- Bay 插入、排序或 Builder 遍历顺序变化可能改变所有后续 ID；
- 需要冻结派生算法和对象编号协议；
- TemplateInstanceId 的生命周期与持久化语义会成为新的工程事实；
- 重复使用同一实例标识可能造成工程冲突；
- 容易让 Redo 重新运行 Builder，而不是恢复首次创建对象。

因此不推荐作为生产 Stable ID 策略。

### 5.3 方案 C：外部预分配完整 ID 图

调用方预先提供所有对象 ID。它适合恢复导入或精确测试，但会把聚合内部结构泄露给 Builder 调用方，削弱 Domain 工厂边界，不适合作为普通模板创建入口。

### 5.4 推荐方案

推荐方案 A：使用可注入的 `StableIdGenerator`，生产默认生成随机 GUID，测试可以提供确定序列。

关键不是让 Build 可重复生成相同 ID，而是只 Build 一次并固定结果：

```text
Template + BuildContext
→ Build once
→ TemplateBuildResult (all IDs fixed)
→ Add Command stores same result objects
→ Undo removes them
→ Redo restores the same objects
```

它满足：

- Undo/Redo：Command 持有首次结果，不在 Redo 时重新 Build；
- Save/Load：现有 DTO 保存生成后的 Domain/Layout Stable ID；
- Selection：SelectionReference 指向首次生成的根或子对象 ID；
- 对象引用：Terminal、Node、Switch、Interval 和 Layout 均引用同一批固定 ID。

Builder 失败时，已临时生成但未发布的 ID 可以丢弃；Stable ID 不要求连续，也不应回收。

## 6. Cabinet 聚合边界

### 6.1 方案 A：直接生成 Bay、Device、Terminal、Node

该方案让 Builder 手工按顺序拼装每个内部对象。

优点：表面上容易映射模板中的设备清单。

缺点：

- 重复 RingCabinet Domain 工厂的专业拓扑规则；
- 允许 Builder 创建不完整或不一致聚合；
- 容易遗漏 SwitchAssembly、内部 Owner、GroundingStructureKind 等约束；
- 把聚合内部 ID 和对象注册顺序暴露给模板层；
- 与当前 `RingCabinet.Create(RingCabinetDefinition)` 边界冲突。

不推荐。

### 6.2 方案 B：增加 RingCabinetInstance

若 `RingCabinetInstance` 被设计为新的 Domain 聚合根，会与现有 `RingCabinet` 重复，迫使 Persistence、Selection、Rendering 和 Command 全部理解两个根对象，违反本阶段不修改 Domain 的边界。

如果它只是 Builder 层结果包装，则本质上等价于 `TemplateBuildResult<RingCabinet, RingCabinetLayout>`，不应使用容易被误解为 Domain 实例的新术语。

### 6.3 推荐边界

继续以现有 `RingCabinet` 作为唯一 Domain 聚合根，不新增 `RingCabinetInstance` Domain 类型。

Builder 的职责是：

```text
BayTemplate[]
→ RingCabinetIntervalDefinition[]
→ RingCabinetDefinition
→ RingCabinet.Create
→ validated RingCabinet aggregate
```

RingCabinet 工厂负责创建内部 Bay/Interval、Switch、Terminal、ElectricalNode、SwitchAssembly 和固定拓扑。Builder 只映射已确认输入，不手工注册聚合内部对象。

## 7. Command 边界

### 7.1 方案 A：CreateCabinetFromTemplateCommand

如果 Command 在 `Execute` 或 `Redo` 内部调用 Builder，会产生严重问题：

- Redo 可能生成全新的 Stable ID；
- Builder 或 Layout 失败会把创建逻辑与提交逻辑混合；
- Command 难以在执行前验证完整结果；
- SelectionTransition 无法稳定引用首次创建对象。

只有当该 Command 在构造前已经完成 Build，并仅保存固定 BuildResult 时才安全；此时它与现有 `AddRingCabinetCommand` 职责相同，没有必要增加重复 Command。

### 7.2 方案 B：多个 AddCommand

逐 Bay、设备、Terminal、Node 和 Layout 建立多个 AddCommand 会导致：

- 一次用户操作产生多条 History；
- Undo 需要多次操作才能移除完整柜体；
- 中途失败留下半聚合或半布局；
- Dirty 与 Selection 时机不清晰；
- Redo 顺序和引用依赖复杂。

不推荐。

### 7.3 推荐方案

一次模板实例化对应一个原子 Add Command。对于当前 RingCabinet，优先直接复用现有 `AddRingCabinetCommand`：

```text
Builder.Build(template, context)
→ TemplateBuildResult(RingCabinet, RingCabinetLayout)
→ DeviceCommandFactory creates AddRingCabinetCommand
→ CommandStack.ExecuteCommand
```

Command 负责把完整聚合和布局原子加入工程；Builder 不进入 CommandStack。Undo 完整移除同一聚合和 Layout，Redo 恢复同一对象。

只有未来模板输出跨越现有单一聚合与布局边界，而且无法由现有 Add Command 原子管理时，才设计新的类型化 Command。也必须是一个用户动作一个 History 项，而不是内部对象多个 AddCommand。

## 8. Builder 创建顺序

根据当前项目架构，推荐顺序不是手工创建 Bay、设备、Terminal、Node 和 Connection，而是先形成 Domain 创建定义，再委托聚合工厂：

1. 验证 BuildContext：Template、RootDisplayName、PlacementOrigin、ID Generator 和 LayoutRuleResolver 均存在。
2. 验证 Template：Bays 非空、Index 为正数且满足模板层唯一性要求、EquipmentConfiguration 完整。
3. 检查能力矩阵：确认每个 Bay 的 Function/EquipmentConfiguration 当前可映射；PT/DTU 若前置模型未完成则明确拒绝。
4. 按 `Bays[]` 物理顺序创建 `RingCabinetIntervalDefinition[]`。
5. 由 ID Generator 分配 CabinetId；内部对象 ID 由受控 Domain 创建边界一次生成。若未来 Domain 工厂支持注入完整 ID Source，应由工厂内部消费，不由 Builder手工拼装。
6. 创建 `RingCabinetDefinition` 并调用 `RingCabinet.Create`。
7. Domain 聚合工厂创建 Bay/Interval、Primary Equipment、Terminal、ElectricalNode、SwitchAssembly 与固定内部拓扑。
8. 校验返回的 RingCabinet 聚合与模板映射一致；Builder 不新增普通外部 Connection。
9. 解析 LayoutRule，并根据真实 RingCabinet 的 Interval、Switch 和 Stable ID 创建 `RingCabinetLayout`。
10. 校验 `RingCabinetLayout.CabinetId == RingCabinet.Id`，且每个实际 Interval/Switch 恰有匹配子布局。
11. 返回不可变 `TemplateBuildResult`。
12. 调用方根据 BuildResult 创建一个 Add Command；只有 Command Execute 成功后，工程、Selection 和 Dirty 才变化。

当前 RingCabinet 内部导通通过 Terminal 绑定 ElectricalNode 和 Switch 聚合表达，不要求 Builder 为每个相邻内部对象创建通用 Connection。Builder 不应发明新的连接事实。

未来 PT Domain 就绪后，其专用 Terminal、Node 和内部关系也应由 PT Interval 的受控 Domain 工厂创建，Builder 仍只负责选择合法定义。

## 9. Layout 生成策略

Template 的 LayoutRule 保存或引用规则，例如：

- 标准 Bay 宽度；
- Bay 间距；
- 柜体边距；
- 主母线相对位置；
- PT 特殊宽度；
- DTU 左右位置与相对排列；
- 标签偏移默认策略。

Template 不保存任何具体实例的绝对坐标。`PlacementOrigin` 由本次 BuildContext 提供，布局策略据此计算实例几何。

对于 Ring Cabinet，Builder 最终生成 `RingCabinetLayout`，不是 `AttachmentLayout`：

```text
RingCabinet + PlacementOrigin + resolved LayoutRule
→ RingCabinetLayout
  ├── RingCabinetIntervalLayout[]
  └── RingCabinetSwitchLayout[]
```

布局策略必须读取已创建 Domain 聚合的实际 IntervalKind、GroundingStructureKind、Switch 集合和 Stable ID。它不能根据 Bay DisplayName 或 Function 猜测 Domain 结构，也不能把 BayFunction、PT 类型或 DTU 专业语义复制到 Layout 作为第二事实源。

当前 `RingCabinetLayoutFactory` 只支持现有两类 Interval。PT 特殊宽度需要 PT 专用 Interval Layout 与 Rendering 能力；DTU 位置需要明确的 DTU RuntimeLayout 表达。在这些前置能力完成前，Builder 必须拒绝相应模板，而不是生成缺失布局的 Domain 半成品。

## 10. Selection 行为

模板生成完成后建议自动选择整个 RingCabinet，而不是第一个 Bay，也不是不选择。

理由：

- 一次模板实例化是创建一个完整柜体的用户动作；
- RingCabinet 是现有聚合根和 Add Command 根对象；
- 根对象在 Rendering、Selection Resolver 和 Inspector 中已有稳定身份；
- 自动选择第一个 Bay 会把模板数组顺序变成未经确认的 UI 业务规则；
- 不选择会弱化创建成功反馈，并与现有设备创建体验不一致。

Builder 不返回 Desktop `SelectionReference`，只返回可派生的根身份。Desktop 在 Add Command 成功并 RebuildScene 后创建：

```text
SelectionTargetKind.RingCabinet
ObjectId = RingCabinet.Id
```

并登记：

```text
SelectionTransition.ForAdd(selectionBefore, ringCabinetSelection)
```

如果未来现有枚举对 RingCabinet 使用统一 Device Target，应服从当时真实 Selection API，不为模板增加新的 SelectionTargetKind。

## 11. Undo/Redo 行为

一次模板创建只产生一个 CommandStack 历史项。

Execute 后应存在：

- 完整 RingCabinet 聚合；
- 全部内部 Stable ID；
- 完整 RingCabinetLayout；
- 创建后的根 Selection；
- Dirty 状态。

Undo 应完整移除：

- 同一 RingCabinet 聚合及其内部对象注册；
- 同一 RingCabinetLayout；
- 创建后的 Selection，并按 SelectionTransition 恢复创建前 Selection。

Redo 应恢复：

- 首次 Build 产生的同一 RingCabinet 对象；
- 相同 CabinetId、IntervalId、SwitchId、TerminalId、ElectricalNodeId 和 SwitchAssemblyId；
- 相同 RingCabinetLayout 与布局引用 ID；
- 创建后的根 Selection。

Redo 不得重新调用 Builder、重新解析 Template、重新分配 ID 或重新计算初始 Layout。模板在 Execute 后发生变化也不得影响该 Command 已固定的 Before/After 结果。

## 12. 与现有 P0-6 架构关系

Builder 是现有创建配置与聚合工厂之前的一层参数映射，不改变 P0-6 的事实边界：

```text
Template description
→ Builder mapping
→ RingCabinetDefinition
→ RingCabinet.Create
→ RingCabinetLayoutFactory / resolved layout strategy
→ AddRingCabinetCommand
→ CommandStack
→ Scene rebuild
→ SelectionTransition / Selection
→ Inspector / Existing Rendering
```

Builder 输出的 RingCabinet 与 RingCabinetLayout 等价于用户通过现有配置器手工创建的结果，因此可以直接进入：

- 现有类型化 Add Command；
- CommandStack、Undo/Redo 与 Dirty；
- Stable-ID-based Selection；
- SelectionTransition；
- Property Inspector Resolver / Projector；
- Existing Rendering；
- 现有 Persistence 合同（仅限当前 DTO 已支持的 Domain/Layout 类型）。

Builder 不修改 Domain 边界，不替代 Command，不进入 Selection，不要求 Persistence 保存 Template。当前已支持的 LoadSwitch/IntegratedFeeder 结果继续使用现有 FormatVersion；PT/DTU 未来若增加新的持久化事实，必须由其独立设计决定，不能由 Builder 私自扩展 DTO。

## 13. 第一版实现范围建议

### 13.1 目标范围

P0-7 Builder 第一版目标契约覆盖：

- `RingCabinetTemplate`；
- 普通负荷开关环网柜；
- 一二次融合环网柜；
- 混合 Bay 生成；
- BayFunction 与 EquipmentConfiguration 显式映射；
- PT Bay 的两种目标配置；
- RingCabinet RuntimeLayout 生成；
- 原子 Command、Stable ID、SelectionTransition 与 Undo/Redo 接入。

### 13.2 当前可直接实现的执行子集

基于 checkpoint `c1acd84` 的实际代码，Builder-only 第一笔生产实现只能生成：

- LoadSwitchInterval；
- IntegratedFeederInterval；
- 两者组成的现有合法混合 RingCabinet；
- 对应 RingCabinetLayout。

这些配置仍必须通过当前 RingCabinet Domain 的实际数量和聚合校验。模板模型支持更开放的 Bay 数量，不等于当前 Domain 已支持所有数量。

### 13.3 PT 前置门槛

PT Bay 是第一版目标契约的一部分，但当前不存在 PTInterval Domain、专用 Terminal/Node 语义、Layout、Rendering 与 Persistence 合同。因此“第一版可执行 Builder 支持 PT Bay”必须以前置能力完成为条件：

1. PT Domain 模型及两种受控工厂；
2. PT 专用 Terminal、Node、连接策略和删除约束；
3. PT Interval Layout 与类型感知布局策略；
4. PT Rendering、Selection 和 Inspector 投影；
5. 必要的 Persistence/FormatVersion 独立决策。

前置能力未完成时，Builder 可以识别 PT 模板并返回明确的 UnsupportedCapability 错误，但不能返回伪造结果，也不能宣称 PT 生成已支持。

### 13.4 DTU 边界

DTU 不在本次 Builder 第一笔生产实现内。只有 DTU RuntimeLayout 与二次配置表现边界完成后，Builder 才能消费 SecondaryConfiguration。DTU 不得被创建为一次 Bay 或伪造 Domain Device。

### 13.5 暂不支持

第一版暂不支持：

- 所有厂家差异和产品数据库；
- 自动电气计算；
- 网络拓扑、潮流或停电范围分析；
- 高级模板参数编辑；
- JSON/YAML 模板加载；
- Template Persistence；
- 自动编号、自动命名或 Function 推断；
- 已存在柜体的模板重套或结构重配置。

## 14. 设计结论与后续门槛

推荐的 Builder 架构为：

```text
immutable Template + narrow BuildContext
→ capability validation
→ RingCabinetDefinition mapping
→ existing RingCabinet aggregate factory
→ type-aware RingCabinetLayout generation
→ immutable TemplateBuildResult
→ existing AddRingCabinetCommand
```

关键决策：

- Builder 不接收 ProjectRuntimeSession，不修改工程；
- Builder 不手工拼装 RingCabinet 内部对象；
- RingCabinet 继续是唯一 Domain 聚合根，不新增 RingCabinetInstance；
- Stable ID 使用可注入生成器，生产默认随机 GUID，并在首次 Build 后固定；
- Builder 返回根聚合和根布局，不返回 void 或无约束对象列表；
- 一次模板创建使用一个原子 Add Command；
- Redo 复用首次 BuildResult，不重新 Build；
- Builder 不创建 SelectionReference，只提供根身份；
- Ring Cabinet 生成 RingCabinetLayout，不生成 AttachmentLayout；
- PT 是目标模板能力，但在 PT Domain/Layout 等前置能力完成前不得执行生成。

进入生产实现前，还需要冻结两个接口级问题：

1. 当前 RingCabinet Domain 工厂内部直接生成 Guid；若要严格由 Builder 注入 StableIdGenerator，需要先设计不破坏聚合封装的 ID Source 注入点，不能退回 Builder 手工拼装内部对象。
2. LayoutRuleResolver 的最小规则集合必须与现有 RingCabinetLayoutFactory 对齐，避免同时存在两套默认几何事实。

上述问题不阻断本架构设计，但必须在 P0-7-B 实现审查中解决后才能编码。
