# P0-7-A 参数化电气设备模板系统设计

> 状态：架构设计冻结稿；本阶段不包含 Builder、生产代码、持久化或 UI 实现。<br>
> 基线：checkpoint commit `126fc930050cb8de22f95f895df66a90b32f64be`。<br>
> 事实边界：现有 Domain 与 RuntimeLayout 是 Builder 未来必须输出的权威模型；本文定义模板侧目标契约，不声明当前生产代码已经支持 PT 或 DTU。

## 1. 设计目标

模板系统用于把已确认、可重复的电气设备套路表达为参数化创建定义，减少用户逐项配置固定结构时的重复劳动。它应支持：

- 快速生成常见环网柜及其有序间隔；
- 由同一份模板输入同时生成合法的电气对象和匹配的运行时布局；
- 在不改变现有 Rendering 职责的前提下复用既有图元组合能力；
- 通过新增模板、间隔功能和受控设备配置扩展更多设备类型；
- 表达普通、一次二次融合、混合及包含 PT 的组合；
- 不把具体厂家、产品系列、间隔数量或现场常见组合固化为全局领域限制。

模板是生成输入，不是工程事实的第二副本。生成完成后，Domain 保存专业事实和拓扑，RuntimeLayout 保存几何；后续编辑、选择、Undo/Redo 和保存仍针对现有模型工作，而不是持续依赖模板反推工程对象。

## 2. 总体架构

总体数据流为：

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

各层职责如下：

- `Template`：表达用户选定的生成参数、间隔功能、设备配置、二次配置和布局规则引用；不保存生成后的 Stable ID，不保存绘图坐标。
- `Builder`：校验模板输入，调用受控 Domain 工厂创建完整对象，再根据已创建的真实对象生成匹配的 RuntimeLayout；任一步失败时不得发布半成品。
- `Domain Objects`：保存设备、间隔、Terminal、ElectricalNode、SwitchAssembly 及连接关系等专业事实。
- `RuntimeLayout`：保存由 Stable ID 关联的几何，不复制电气功能或拓扑事实。
- `Existing Rendering`：读取 Domain + RuntimeLayout 构建 Scene；不读取模板来补充事实，不由模板直接生成图形。

模板不得直接创建 `SceneElement`、Symbol、WPF Shape 或其他绘图对象。模板也不得根据显示名称猜测间隔类型或设备结构。

## 3. Ring Cabinet Domain Template

模板侧核心模型为 `RingCabinetTemplate`。这是目标模板契约，不是对当前 `RingCabinet` 聚合增加字段。

```text
RingCabinetTemplate
├── CabinetType
├── Bays[]
├── SecondaryConfiguration
└── LayoutRule
```

字段职责：

| 字段 | 职责 | 约束 |
| --- | --- | --- |
| `CabinetType` | 描述模板采用的柜体生成类别，例如普通型或一二次融合型，供模板目录、默认配置和布局策略选择使用 | 不决定全部 Bay 的设备组合，不限制 Bay 数量，不代替实际 `Bays[]` |
| `Bays[]` | 按物理排列顺序保存一次系统间隔模板 | 每个 Bay 独立声明功能和设备配置；数组顺序是物理顺序 |
| `SecondaryConfiguration` | 保存不参与一次拓扑的二次配置，例如 DTU 及其布局侧位置 | 不生成一次 Terminal、ElectricalNode 或 Connection |
| `LayoutRule` | 保存布局生成规则和尺寸策略引用 | 只描述规则，不保存生成后的绝对坐标或 Stable ID |

`CabinetType` 的第一阶段概念值可包括 `Conventional` 与 `PrimarySecondaryIntegrated`。它们是模板分类和默认值来源，不是 Domain 聚合组成分类，也不得覆盖某个 Bay 明确给出的 `EquipmentConfiguration`。混合柜仍由实际 `Bays[]` 决定。

模板身份、版本、厂家元数据和外部序列化格式不在 P0-7-A 冻结范围内。未来即使增加这些元数据，也不得改变本节四个核心生成输入的职责。

## 4. Bay（间隔）模型

`Bay` 冻结为环网柜一次系统的基本配置单元。模板侧模型为：

```text
BayTemplate
├── Index
├── Function
└── EquipmentConfiguration
```

字段职责：

| 字段 | 职责 |
| --- | --- |
| `Index` | 保存现场“负 N 间隔”中的正整数 N；它是现场编号，不是数组下标 |
| `Function` | 表示该 Bay 的电气功能 |
| `EquipmentConfiguration` | 表示该 Bay 采用的受控设备组合和创建参数 |

编号规则冻结如下：

- `Index` 必须使用正整数；
- `Index = 5` 显示为“负5间隔”；
- 禁止以 `Index = -5` 表示“负5间隔”；
- `Bays[]` 的集合顺序表示物理排列顺序，不依赖正负号排序；
- 模板不得自动假定编号连续，也不得依据编号推断 `Function` 或设备配置。

“负”是现场显示前缀，不是数值符号。显示名称可由 UI 投影为 `负{Index}间隔`，但模板模型只保存无歧义的正整数编号。

## 5. BayFunction

第一阶段目标枚举 `BayFunction` 包含：

| 值 | 含义 |
| --- | --- |
| `Incoming` | 进线功能 |
| `Outgoing` | 出线功能 |
| `Tie` | 联络功能 |
| `PT` | 电压互感器间隔功能 |
| `Metering` | 计量功能 |
| `Reserve` | 备用功能 |

`BayFunction` 只表达电气用途，不表达具体设备。相同的 `Outgoing` 可以使用不同的设备配置；相同设备组合也不能反向证明其一定是某个 Function。

Builder 必须同时读取 `Function` 与 `EquipmentConfiguration`，并由受控兼容性规则决定能否生成。不能用 `Function` 自动拼装任意设备，也不能从设备名称或 DisplayName 猜测 Function。除本文明确列出的 PT 结构外，Function 与设备配置的完整兼容矩阵留待专业规则确认。

## 6. EquipmentConfiguration

`EquipmentConfiguration` 与 `BayFunction` 分离，负责表达一个 Bay 要采用的受控设备组合。它不是允许调用方自由排列任意设备的列表；每种配置必须对应明确的 Builder 分支和 Domain 工厂。

普通负荷开关间隔示例：

```text
Function: Outgoing
EquipmentConfiguration:
  - LoadSwitch
  - EarthSwitch
```

一二次融合出线示例：

```text
Function: Outgoing
EquipmentConfiguration:
  - CircuitBreaker
  - Isolator
  - EarthSwitch
```

对于现有一二次融合间隔，配置还必须携带当前 Domain 创建所需的接地结构参数；设备清单不能替代 `GroundingStructureKind`，也不能由 Layout 猜测该结构。

建议采用受控的配置变体，而不是一个无约束 `EquipmentKind[]`：

- `LoadSwitchEquipmentConfiguration`：负荷开关 + 接地刀；
- `IntegratedFeederEquipmentConfiguration`：断路器 + 隔离刀 + 接地刀，并包含必要的接地结构参数；
- `PTEquipmentConfiguration`：PT + 一种母线侧控制设备 + PT 专用端子 + 接地刀。

未来增加新的设备组合时，应新增明确配置变体及其校验，不修改 `BayFunction` 来编码厂家产品或具体设备清单。

## 7. PT 间隔模型

PT 是 Bay，不是普通附件，也不是 `CableTermination` 的一种用途。

PT 属于一次系统，原因是：

- 它接入环网柜主母线；
- 它具有一次设备连接关系；
- 它具有固定的 PT 专用端子；
- 它不允许作为普通电缆出口使用。

PT Bay 固定使用：

```text
Function = PT
```

第一阶段支持两种受控设备配置。

### 7.1 PT 隔离刀方案

```text
母线
 |
隔离刀
 |
PT
 |
PT端子
 |
接地刀
```

对应配置语义：

```text
PTEquipmentConfiguration
├── PT
├── PrimaryControl = Isolator
├── DedicatedPTTerminal
└── EarthSwitch
```

### 7.2 PT 断路器方案

```text
母线
 |
断路器
 |
PT
 |
PT端子
 |
接地刀
```

对应配置语义：

```text
PTEquipmentConfiguration
├── PT
├── PrimaryControl = CircuitBreaker
├── DedicatedPTTerminal
└── EarthSwitch
```

PT 专用端子是 PT Bay 固定拓扑的一部分，不能被普通 `CableTermination` 替代，也不能复用普通间隔的对外电缆端子语义。PT 端子的具体 Domain 类型、连接策略、节点归属和联锁规则必须在 PT Domain 实现设计中明确；P0-7-A 不自行补充这些尚未冻结的专业细节。

当前生产 Domain 尚无 `PTInterval`。因此本文冻结的是模板侧目标语义；未来 Builder 在 PT Domain 能力完成前必须拒绝生成 PT Bay，不能把它降级成 `LoadSwitchInterval`、`IntegratedFeederInterval`、普通附件或 CableTermination。

## 8. 间隔数量规则

间隔数量是生成参数，由 `Bays.Count` 得出，不是 `CabinetType` 或设备类型。

- 普通环网柜模板支持 2、3、4、5、6 及未来更多数量；
- 一二次融合模板可提供 4 间隔、6 间隔作为默认快捷值；
- 4 和 6 不是一二次融合模板模型的数量上限或 Domain 限制；
- PT Bay 作为 `Bays[]` 中的独立一次 Bay 参与物理排列和编号；
- 不得把“4+2”“6+PT”等现场常见实例编码为唯一合法比例。

模板目录或 UI 可以提供常用数量预设，但 Builder 的结构合法性最终仍由实际设备配置和 Domain 工厂校验决定。模板层不得绕过现有 Domain 不变量。

## 9. DTU 模型

DTU 不属于一次 Bay：

- 它是二次设备；
- 不参与一次拓扑；
- 不创建一次 ElectricalNode、Terminal 或 Connection；
- 不与 PT 处于同一模型层级。

DTU 通过 `SecondaryConfiguration` 表达：

```text
SecondaryConfiguration
└── DTU
    └── Position: Left | Right
```

`Position` 只影响 Layout 生成，不改变一次 Bay 顺序和拓扑。模板不得把 DTU 插入 `Bays[]`，也不得把 DTU 位置解释为电气连接关系。

当前 RuntimeLayout 没有已确认的 DTU 专用布局对象。未来实现应先明确对应运行时布局表达；在此之前 Builder 不得伪造一次 Domain Device。DTU 的厂家、型号、通信和自动化参数不属于 P0-7-A。

## 10. Layout 生成原则

模板只提供布局规则，不保存生成结果的具体坐标。`LayoutRule` 可以表达或引用：

- 标准 Bay 宽度；
- Bay 间距；
- PT 特殊宽度；
- 柜体边距和母线相对位置策略；
- DTU 位于左侧或右侧时的排列策略；
- 标签偏移的默认策略。

模板不得保存：

- 某个生成对象的绝对 DocumentPoint；
- 生成后的 DeviceId、IntervalId、SwitchId 或 TerminalId；
- 逐设备的最终像素坐标；
- Rendering 图元或 WPF 类型。

Builder 接收模板和单独的放置位置，在 Domain 对象成功创建后检查其实际 Bay、设备和 Stable ID，再生成匹配的 RuntimeLayout。布局事实以生成后的 RuntimeLayout 为准，后续编辑不回写模板。

当前架构中，环网柜应生成 `RingCabinetLayout` 及其 Interval/Switch 子布局；`AttachmentLayout` 专用于 PoleAttachment。因而“最终生成 AttachmentLayout”不能作为所有模板的通用规则。准确边界是：

- Ring Cabinet Template → `RingCabinetLayout`；
- 未来 Pole Attachment Template → `AttachmentLayout`；
- 两者都作为 `RuntimeLayoutDocument` 的组成部分进入现有 Rendering。

这一类型区分不得为模板系统而抹平，也不得把 `AttachmentLayout` 当作任意设备的通用布局容器。

## 11. 模板示例

以下 Function 分配只用于展示模板表达能力，不构成自动编号、默认用途或全局专业规则。示例中的“负 N 间隔”均由 `Index = N` 派生显示，不是 `BayTemplate` 的额外 `Display` 字段。

### 11.1 示例 1：普通 4 间隔

```text
CabinetType: Conventional
SecondaryConfiguration: None
Bays:
  - Index: 1  Function: Incoming  Equipment: LoadSwitch + EarthSwitch
  - Index: 2  Function: Outgoing  Equipment: LoadSwitch + EarthSwitch
  - Index: 3  Function: Outgoing  Equipment: LoadSwitch + EarthSwitch
  - Index: 4  Function: Reserve   Equipment: LoadSwitch + EarthSwitch
LayoutRule: ConventionalDefault
```

### 11.2 示例 2：普通 4 间隔 + PT

```text
CabinetType: Conventional
SecondaryConfiguration: None
Bays:
  - Index: 1  Function: Incoming  Equipment: LoadSwitch + EarthSwitch
  - Index: 2  Function: Outgoing  Equipment: LoadSwitch + EarthSwitch
  - Index: 3  Function: Outgoing  Equipment: LoadSwitch + EarthSwitch
  - Index: 4  Function: Reserve   Equipment: LoadSwitch + EarthSwitch
  - Index: 5  Function: PT        Equipment: PT + Isolator + DedicatedPTTerminal + EarthSwitch
LayoutRule: ConventionalWithPT
```

### 11.3 示例 3：一二次融合 6 间隔 + 负7 PT

```text
CabinetType: PrimarySecondaryIntegrated
SecondaryConfiguration:
  DTU:
    Position: Right
Bays:
  - Index: 1  Function: Incoming  Equipment: CircuitBreaker + Isolator + EarthSwitch
  - Index: 2  Function: Outgoing  Equipment: CircuitBreaker + Isolator + EarthSwitch
  - Index: 3  Function: Outgoing  Equipment: CircuitBreaker + Isolator + EarthSwitch
  - Index: 4  Function: Outgoing  Equipment: CircuitBreaker + Isolator + EarthSwitch
  - Index: 5  Function: Tie       Equipment: CircuitBreaker + Isolator + EarthSwitch
  - Index: 6  Function: Reserve   Equipment: CircuitBreaker + Isolator + EarthSwitch
  - Index: 7  Function: PT        Equipment: PT + Isolator + DedicatedPTTerminal + EarthSwitch
LayoutRule: IntegratedWithPTAndDTU
```

各 IntegratedFeeder 配置在真实生成输入中仍必须分别给出当前 Domain 要求的接地结构；示例省略该值不表示 Builder 可以猜测或采用未经确认的默认值。

### 11.4 示例 4：PT 断路器方案

```text
BayTemplate:
  Index: 5
  Function: PT
  EquipmentConfiguration:
    PT
    PrimaryControl: CircuitBreaker
    DedicatedPTTerminal
    EarthSwitch
```

该配置生成目标是“母线—断路器—PT—PT 专用端子—接地刀”的固定 PT Bay，不是普通断路器出线，也不提供普通 CableTermination 出口。

## 12. 与现有架构的关系

模板系统不会改变现有各层职责：

- Domain 继续保存专业事实和拓扑，并作为最终合法性的权威边界；
- Command 继续管理正式编辑行为、Undo/Redo 和 Dirty；
- Selection 继续通过 Stable ID 解析已生成对象，不选择模板定义；
- Rendering 继续消费 Domain + RuntimeLayout，不消费模板来创建事实；
- Persistence 继续保存工程对象和布局，而不是在本阶段保存模板。

模板系统只新增一条生成路径：

```text
Template
→ Builder
→ Existing Domain Model + RuntimeLayout
→ Existing Command workflow
→ Existing Rendering / Selection / Persistence
```

Builder 未来应返回一个完整创建结果，由现有类型化 Add Command 原子加入 DrawingDocument 与 RuntimeLayout。模板不能直接写 DrawingDocument、绕过 CommandStack，或在 Redo 时重新生成 Stable ID。

对当前已经支持的设备结构，模板生成结果必须与用户通过现有配置器手工创建的结果等价：

- Builder 在首次创建时一次生成并固定全部 Domain 与 Layout Stable ID；
- Add Command 原子加入完整 Domain 聚合和匹配的 RuntimeLayout；
- Undo/Redo 复用同一创建结果，不重新调用 Builder 生成对象；
- Selection 和 SelectionTransition 继续引用生成对象的 Stable ID；
- Property Inspector 继续通过现有 Resolver 和 Projector 读取生成后的 Domain/Layout；
- Rendering 继续只读取 Domain + RuntimeLayout；
- 后续属性编辑继续使用现有类型化 Command，并由 CommandStack 管理 Dirty。

当前代码已经能承接 LoadSwitchInterval、IntegratedFeederInterval 及其 RingCabinetLayout。PT 和 DTU 尚未具备完整生产模型，因此它们在本文中是冻结的目标模板语义，不是当前可执行 Builder 分支。实现顺序必须尊重该能力差异，不得用显示层补齐缺失的 Domain 事实。

## 13. 明确暂不实现

P0-7-A 不实现：

- UI 模板选择器；
- 模板参数编辑器；
- 自动生成生产代码；
- Template Builder；
- JSON、YAML 或其他外部模板格式；
- 模板 Persistence 或工程文件引用；
- FormatVersion 升级；
- 模板目录、厂家库或产品数据库；
- PT Domain、Terminal、拓扑、Command、Layout 或 Rendering；
- DTU Domain/Layout/Rendering 的生产实现；
- 自动编号、自动命名或 Function 推断；
- 对现有 RingCabinet、CommandStack、Selection 或 Rendering 的修改。

下一阶段在实现 Builder 前，应先审查目标模板契约与现有 Domain 工厂的映射，并将“当前可直接生成的配置”与“依赖 PT/DTU 新模型的配置”分开。未经该审查，不应创建虚假的兼容分支。
