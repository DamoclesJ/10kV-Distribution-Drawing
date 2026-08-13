# F-2-C-1 10kV Device Catalog Design

> 状态：设计阶段；本阶段不修改生产代码、测试、Domain、Persistence 或项目文件。
> 基线：`fc06f2a docs: define 10kv work ticket topology model`。
> 目标：冻结支撑第一版 10kV 工作票绘图的设备与连接目录。

## 1. Context

当前项目已经具备：

- `DrawingDocument` 工程聚合；
- `Device`、`Terminal`、`ElectricalNode`、`Connection` 核心模型；
- Command、Undo/Redo 和 Dirty；
- Project FormatVersion 4；
- RingCabinet Template 与 TemplateLibrary；
- `SwitchDevice`、`SwitchKind` 和 `SwitchState`；
- RingCabinet、Pole、CableTermination 和 OverheadLine 的部分运行时能力。

本项目目标是具有基础电气拓扑识别能力的 10kV 工作票绘图工具，不是专业电网仿真或自由 CAD。设备目录的作用是回答：系统第一版承认哪些业务对象、它们属于哪一类、拥有何种拓扑能力，以及哪些能力暂不进入实现。

设备目录不是第二套 Domain 模型，也不是 TemplateLibrary 的替代品。本文完全复用现有 `Device`、`Terminal`、`ElectricalNode` 和 `Connection` 语义。

## 2. Goals

第一版目录需要：

1. 明确 Device、Attachment、Connection 三类对象；
2. 明确 Terminal、SwitchState、Rendering 和 Persistence 责任；
3. 保持 RingCabinet 与 Pole 两种安装/聚合上下文；
4. 保留 Breaker、LoadSwitch、Disconnector 和 Fuse 类型区别；
5. 将 CableTerminal 定位为连接端子能力，而不是独立设备；
6. 将 Transformer 限定为未来扩展；
7. 暴露当前实现与目标模型之间的差异；
8. 为后续拓扑查询、Cable 创建和柱上设备实现提供稳定边界。

## 3. Non-Goals

本设计不引入：

- 新的 Device/Terminal/Connection 基类；
- 复杂设备继承体系；
- GIS、空间网络或地理坐标模型；
- SCADA、遥测、遥信、遥控或事件顺序记录；
- 继电保护、定值、动作原因或故障分析；
- 潮流、短路、负荷或可靠性计算；
- 从图形位置、线段相交或颜色反推拓扑；
- 自由 CAD 拓扑；
- BayFunction、Incoming、Outgoing、Tie 或方向角色；
- Transformer 当前实现；
- 用户自定义设备编辑器。

## 4. Catalog Classification Rules

### 4.1 Device

Device 是具有独立业务身份和生命周期的设备对象。它通常具有：

- Stable Device ID；
- 明确设备类型；
- 名称及必要业务属性；
- 自己拥有或聚合管理的 Terminal；
- 可被 Selection/Inspector 识别；
- 可通过 Command 添加、删除或修改；
- 需要 Persistence 保存并恢复同一身份。

不是所有 Device 都拥有 SwitchState。只有可控制开断的 `SwitchDevice` 拥有 SwitchState。

### 4.2 Attachment

Attachment 表达“什么设备或能力安装/附属于哪个主体设备”。它：

- 有独立关系或能力身份；
- 保存 owner/parent 关系；
- 不等于电气 Connection；
- 不复制附属 Device 的状态；
- 不因视觉重叠自动产生；
- 需要随工程保存。

### 4.3 Connection

Connection 表达两个合法 Terminal endpoint 之间的外部电气连接。它：

- 具有 Stable Connection ID；
- 引用两个 Terminal IDs；
- 具有 `ConnectionType`；
- 不拥有 endpoint Terminal；
- 不复制 endpoint owner；
- 不通过图形线段碰撞生成；
- 需要 Command、Rendering 和 Persistence 闭环。

### 4.4 Structural Aggregate

RingCabinet Interval 等柜内结构属于 Device 聚合内部对象，不作为顶层设备目录项。它们由固定 `IntervalKind` 管理设备、Terminal、ElectricalNode 和 SwitchAssembly，不允许调用方自由拼装。

## 5. First-Version Catalog Matrix

| Catalog item | Category | Current/target status | Owns Terminal | Owns SwitchState | Rendering requirement | Persistence requirement |
| --- | --- | --- | --- | --- | --- | --- |
| RingCabinet | Device / aggregate root | Current | 聚合管理 Cabinet/Interval/Switch terminals | No（内部 SwitchDevice 拥有） | Cabinet、Interval、内部 Switch symbols | Required；V4 已支持 |
| Cable | Connection | Core current；专业详情待需求 | No；引用两个 endpoints | No | Connection line/layout；不是 Device symbol | Generic Connection 已由 V4 支持 |
| Pole | Device / attachment owner | Current | 可拥有 overhead anchor terminals | No | Pole symbol、label、anchors | Required；V4 已支持 |
| PoleAttachment | Attachment relationship | Current but device-only | No | No | 关系本身通常无独立 symbol | Required；V4 已支持 device attachment |
| CableTerminal | Attachment capability | Target；当前实现不一致 | Target owns cable/overhead terminals and internal node | No | Terminal/attachment marker；不是 Device symbol | Required；需未来迁移设计 |
| SwitchDevice | Device | Current；柱上闭环不完整 | Yes，两个 terminals | Yes | 按 SwitchKind/State 映射 symbol | 柜内 V4 已支持；顶层柱上尚不支持 |
| OverheadLine | Connection detail | Current | No；引用 Connection endpoints | No | Line geometry、supports、continuation marker | Required；V4 已支持 |
| Transformer | Future Device | Reserved only | Future high/low terminals | No（除非未来另有明确设备状态） | Future symbol | Future format only |

## 6. RingCabinet Catalog Entry

### 6.1 Classification

`RingCabinet` 是 Device，同时是柜内结构的 aggregate root。

```text
RingCabinet Device
  MainBus ElectricalNode
  Interval[Sequence]
    IntervalKind
    BayIndex
    SwitchDevice(s)
    SwitchAssembly
    internal terminals
    external terminal
```

Interval 不是顶层 Device Catalog item。它是 RingCabinet 内部结构 owner，通过 `TopologyOwnerType.InternalAggregate` 管理其 external terminal 和内部节点。

### 6.2 Terminal Ownership

RingCabinet 聚合管理：

- MainBus node；
- 每个 Interval 的 Circuit/Earth/Intermediate node；
- 内部 SwitchDevice terminals；
- 每个 Interval 的 external terminal。

外部 Cable 或 OverheadLine 只能连接合法 external terminal，不直接连接柜内 switch internal terminal。

### 6.3 State

Cabinet 本体没有 SwitchState。柜内 SwitchDevice 各自保存状态，并由固定 SwitchAssembly 执行已确认的有限联锁。

### 6.4 Rendering and Persistence

Rendering.Wpf 需要投影：

- Cabinet outline/symbol；
- Interval symbols；
- SwitchKind 与 SwitchState；
- external terminal anchor；
- Label 和 HitTest。

V4 已保存 Cabinet、Interval、Switch、Terminal、Node、Assembly 和 Stable IDs。Project Restore 不依赖 TemplateLibrary，也不根据 TemplateId 重新 Build。

## 7. Cable Catalog Entry

### 7.1 Classification

Cable 属于 `ConnectionType.Cable`，不是 Device，也不是 Attachment。

第一版最小拓扑事实为：

```text
external terminal A
        |
        | Connection(Type = Cable)
        |
external terminal B
```

现有 `Connection` 已保存 Stable ID、两个 Terminal IDs、DisplayName 和 VoltageLevel，因此创建基础 Cable connection 不需要新的 Device/Terminal/Connection 核心类型。

### 7.2 Terminal and State

Cable 不拥有 Terminal，只引用两个 endpoint Terminal。Cable 不具有 SwitchState，也不产生可操作开断行为。

两端必须：

- 都允许 `ConnectionType.Cable`；
- 使用不同 Terminal IDs；
- 满足电压和连接数量约束；
- 属于当前 DrawingDocument 中存在的 owner；
- 不形成重复 connection。

### 7.3 Optional Future Detail

只有在工作票或 Inspector 确实需要电缆型号、长度、截面等数据时，才增加类似 `OverheadLine` 的 `Cable` detail，并以同一个 `ConnectionId` 关联。该 detail 不得复制 endpoints，也不得升级为 Device。

### 7.4 Rendering and Persistence

Cable 需要 line geometry、selection、HitTest 和 label，但不需要 Device symbol。V4 已能保存 generic Cable Connection；若未来增加 Cable detail 字段，需要明确格式版本评审。

## 8. Pole Catalog Entry

### 8.1 Classification

Pole 是主体 Device 和 Attachment owner。它保存：

- Stable Pole ID；
- PoleNumber；
- PoleType；
- DisplayName；
- 必要 overhead anchor Terminal IDs。

Pole 本体不是 SwitchDevice，也不因为安装开关而获得 SwitchState。

### 8.2 Terminal Ownership

Pole 可以直接拥有架空锚点 Terminal。锚点用于 OverheadLine endpoint，不代表 Pole 本体是导体或开关。

Pole 上安装的 SwitchDevice 或 CableTerminal capability 拥有各自 terminals；这些 terminals 不应被合并为 Pole 的一个无类型集合来解释设备行为。

### 8.3 Rendering and Persistence

Pole 需要主体 symbol、杆号、attachment placement anchors 和 HitTest。V4 已保存 Pole 及其 overhead anchor IDs。

## 9. PoleAttachment Catalog Entry

### 9.1 Classification

PoleAttachment 是 Attachment relationship，不是 Device、Connection 或 SwitchDevice。

当前实现表达：

```text
AttachmentId
PoleId
AttachedDeviceId
```

它适合表达 Pole 与柱上 SwitchDevice 的安装关系。

### 9.2 Target Capability Boundary

目标目录还要求 PoleAttachment 能承载 CableTerminal capability。当前 `AttachedDeviceId` 只允许引用 Device，无法完整表达非 Device capability。

未来设计必须明确：

- capability identity；
- Pole ownership；
- capability-owned Terminal/Node；
- Selection 和 Persistence identity；
- 从旧 CableTermination Device 的迁移。

Attachment 不拥有 SwitchState，不复制 SwitchKind，不作为 Cable/OverheadLine endpoint。

### 9.3 Rendering and Persistence

Attachment relationship 本身通常不需要独立业务 symbol，但需要布局归属，以便其附属设备或能力跟随 Pole 移动。V4 已保存 device attachment；capability attachment 需要未来兼容设计。

## 10. CableTerminal Catalog Entry

### 10.1 Classification

CableTerminal 是 Pole 的 Attachment capability，不是独立 Device，也不是 SwitchDevice。

目标结构：

```text
Pole
  PoleAttachment
    CableTerminal capability
      cable-side terminal
      overhead-side terminal
      fixed internal ElectricalNode
```

### 10.2 Terminal and State

CableTerminal capability 拥有：

- cable-side terminal，只允许 Cable；
- overhead-side terminal，只允许 OverheadLine；
- 将两侧固定连接的 Intermediate ElectricalNode；
- capability、Attachment、Terminal 和 Node 的 Stable IDs。

它不拥有：

- DeviceType；
- SwitchKind；
- SwitchState；
- 分/合闸 Command；
- 保护或联锁行为。

### 10.3 Current Implementation Gap

当前生产代码仍使用 `CableTermination : Device`，并由 `PoleAttachment.AttachedDeviceId` 引用。该实现可以工作，但不符合冻结目标。

目录设计不在本阶段删除或重新解释它。后续必须通过独立 Domain/Persistence migration：

- 保持旧 V4 文件可读；
- 保持原有 Terminal、Node、Attachment 和 Connection identity；
- 明确旧 Device ID 的映射或保留策略；
- 不通过重新创建对象生成替代 Stable IDs。

### 10.4 Rendering and Persistence

CableTerminal 需要连接端子标记、attachment placement、Cable/OverheadLine anchor 和 HitTest。它不使用独立设备符号语义。目标状态必须持久化 capability ownership 和所有 Stable IDs，但不能静默改变 V4 历史合同。

## 11. SwitchDevice Catalog Entry

### 11.1 Classification

SwitchDevice 是统一的可控制开断 Device，通过 `SwitchKind` 保留设备类型：

| Business type | Current SwitchKind | First-version state |
| --- | --- | --- |
| Breaker | `CircuitBreaker` | Open / Closed |
| LoadSwitch | `LoadSwitch` | Open / Closed |
| Disconnector | `IsolationSwitch` | Open / Closed |
| Fuse | `DropoutFuse` | Open / Closed |

现有 `GroundSwitch` 继续复用 SwitchDevice，但属于固定柜内结构的接地开关，不作为本目录第一版可自由选择的顶层设备项。

### 11.2 Terminal and State

每台 SwitchDevice：

- 拥有两个不同 Terminal IDs；
- 拥有 SwitchState；
- Closed 时两端形成局部导通；
- Open 时两端不导通；
- 不自动修改相邻 Connection 或设备状态。

Breaker 和 LoadSwitch 第一版操作行为可以一致，但类型、图例和工作票名称必须保持区别。

### 11.3 Installation Context

`SwitchInstallationType` 区分：

- `CabinetInterval`：由 RingCabinet 固定结构拥有；
- `Pole`：通过 PoleAttachment 安装在 Pole 上。

Attachment 只表达安装关系；SwitchDevice 自己保存 identity、Kind、State 和 terminals。

### 11.4 Rendering and Persistence

每种 SwitchKind 需要独立 symbol mapping；Open/Closed 需要状态视觉。Domain 不保存 WPF SymbolKind。

当前 V4 已支持柜内 SwitchDevice，但 Infrastructure 明确不支持顶层/柱上 SwitchDevice DTO round-trip。柱上 SwitchDevice 进入正式目录实现前，必须完成 Persistence 版本设计和状态 Command 闭环。

## 12. OverheadLine Catalog Entry

### 12.1 Classification

OverheadLine 是 `ConnectionType.OverheadLine` 的专业 detail，不是 Device，也不是 Attachment。

它以同一个 `ConnectionId` 关联：

- 两个 endpoint terminals；
- LineModel；
- 可选 Length；
- SupportPoleIds；
- continuation metadata。

### 12.2 Terminal and State

OverheadLine 不拥有 Terminal，只引用 Connection endpoints。Support Pole 不是电气 endpoint；它们只表达物理支撑路径。

OverheadLine 没有 SwitchState。线路是否跨过柱上 SwitchDevice，必须通过连接到 SwitchDevice 两侧 terminals 表达，而不是修改 OverheadLine 状态。

### 12.3 Rendering and Persistence

OverheadLine 需要 line geometry、support path、continuation marker、selection 和 HitTest，不需要 Device symbol。V4 已保存 Connection 和 OverheadLine detail。

## 13. Transformer Future Catalog Entry

Transformer 只保留未来目录位置，不进入第一版 Runtime、UI、Template 或 Persistence。

未来若需求确认，Transformer 应当：

- 是非 SwitchDevice 的专业 Device；
- 通过 PoleAttachment 或其他明确安装关系附属于主体；
- 拥有自己的高压侧/低压侧 terminals；
- 通过专用 ElectricalNode/结构表达内部关系；
- 不拥有 SwitchState，除非未来有完全独立且经过确认的可操作设备；
- 使用专用 Rendering symbol；
- 通过未来格式版本持久化。

当前不新增：

- Transformer Domain 类型；
- TransformerKind、容量、变比、接线组别或分接头字段；
- Transformer Terminal、Symbol、DTO、Command 或 Template；
- 将 Transformer 放入 `SwitchKind`；
- 空占位对象或自由 metadata。

## 14. Terminal Ownership Summary

| Item | Terminal ownership |
| --- | --- |
| RingCabinet | 聚合管理 MainBus/Interval/Switch/External terminals |
| RingCabinet Interval | 内部 aggregate owner；拥有 external terminal，并组织内部结构 |
| SwitchDevice | 两个 switch terminals |
| Pole | overhead anchor terminals |
| PoleAttachment | 不拥有 Terminal；表达安装关系 |
| CableTerminal capability | Target owns cable-side and overhead-side terminals |
| Cable | 不拥有；引用两个 endpoints |
| OverheadLine | 不拥有；引用其 Connection endpoints |
| Transformer | Future owns high/low terminals |

Terminal owner 必须是明确业务对象或内部 aggregate。Rendering element、layout object、symbol 和画布坐标不得成为 Terminal owner。

## 15. SwitchState Summary

只有 SwitchDevice 拥有 SwitchState：

| Item | SwitchState |
| --- | --- |
| RingCabinet | No；内部 switches 各自拥有 |
| Cable | No |
| Pole | No |
| PoleAttachment | No |
| CableTerminal | No |
| Breaker/LoadSwitch/Disconnector/Fuse | Yes |
| OverheadLine | No |
| Transformer | No in reserved boundary |

不得为 Cable、Pole、Attachment、CableTerminal 或 OverheadLine 增加虚假 Open/Closed 状态。

## 16. Rendering Catalog Boundary

Rendering 分为两类：

### 16.1 Device/Capability Symbols

- RingCabinet cabinet/interval symbols；
- Pole symbol；
- SwitchDevice symbols by Kind and State；
- CableTerminal attachment/terminal marker；
- Future Transformer symbol。

### 16.2 Connection Geometry

- Cable line；
- OverheadLine path；
- endpoint anchors；
- continuation marker。

Cable 和 OverheadLine 需要图形表示，但不因此成为 Device。PoleAttachment 的关系通常不单独绘制；它影响附属对象的位置和归属。

Rendering 不拥有设备类型、Terminal identity、Connection endpoints 或 SwitchState。

## 17. Persistence Catalog Boundary

所有进入 Project Runtime 的当前目录事实都必须保存：

- Device/Attachment/Connection Stable IDs；
- Terminal 和 ElectricalNode IDs；
- owner 和 parent identity；
- SwitchKind 和 SwitchState；
- ConnectionType 和 endpoint IDs；
- 必要结构和专业详情；
- 对应 RuntimeLayout identity。

当前 V4 状态：

| Capability | V4 status |
| --- | --- |
| RingCabinet aggregate | Supported |
| Generic Cable Connection | Supported |
| Pole and anchors | Supported |
| Device-based PoleAttachment | Supported |
| CableTermination as Device | Supported historical model |
| Cabinet SwitchDevice | Supported |
| Top-level/Pole SwitchDevice | Not supported by current DTO mapper |
| OverheadLine detail | Supported |
| CableTerminal capability target | Not represented |
| Transformer | Not represented by design |

不得通过修改 Current V4 DTO 含义来补齐缺口。柱上 SwitchDevice 和 CableTerminal capability 若一起进入正式实现，应先设计新格式版本和 V4 migration。

## 18. Relationship to TemplateLibrary

TemplateLibrary 管理 Approved RingCabinet Templates；Device Catalog 定义工程能够理解的设备、Attachment 和 Connection 类别。

正确边界：

```text
TemplateLibrary
  → returns RingCabinetTemplate
  → Builder creates approved RingCabinet structure
  → Device Catalog classification describes created Domain objects
```

Device Catalog 不负责：

- 查找 TemplateId；
- 构造 RingCabinet Template；
- 重新 Build Project；
- 保存 UI selection；
- 动态注册任意设备类型。

第一版不需要新增运行时 `DeviceCatalog` 类。只有出现多个真实设备来源或 UI 需要统一、可测试的设备选择数据源时，再评估具体 Application catalog API。

## 19. First-Version Availability

### 19.1 Available Now

- RingCabinet；
- RingCabinet internal SwitchDevice；
- Pole；
- generic Cable Connection；
- OverheadLine；
- current Device-based CableTermination compatibility model。

### 19.2 Requires Implementation Before User Exposure

- generic electrical connectivity query；
- Cable Add/Remove Command、layout、selection 和 rendering；
- Pole SwitchDevice creation/state command/persistence；
- CableTerminal capability migration；
- capability-aware PoleAttachment ownership。

### 19.3 Future Only

- Transformer；
- 低压台区设备；
- PT 专用结构模型；
- 更多经专业确认的设备类型和状态。

## 20. Risks and Guardrails

- 把 Catalog item 全部实现成 Device 会污染 identity 和 Command 语义；
- 把 Cable/OverheadLine 当作 Device 会复制 Connection endpoints；
- 把 PoleAttachment 当作 Connection 会混淆安装与导通；
- 把 CableTerminal 当作 SwitchDevice 会产生虚假操作状态；
- 把 SupportPoleIds 当作线路 endpoint 会产生错误拓扑；
- 把 Transformer 预留实现成空 Domain 类型会污染当前模型和 V4；
- 把符号差异当成 Domain 类型来源会让 Rendering 成为事实源；
- 修改 V4 历史含义会破坏旧工程兼容；
- 设备目录不得演变成自由 metadata 或任意拓扑注册系统。

## 21. Final Decision

第一版 10kV 设备目录冻结为：

```text
Devices
  RingCabinet
  Pole
  SwitchDevice

Attachments
  PoleAttachment
  CableTerminal capability

Connections
  Cable
  OverheadLine

Future Device
  Transformer
```

RingCabinet、Pole 和 SwitchDevice 是具有独立身份的 Device；PoleAttachment 是安装关系；CableTerminal 是不可操作的 Attachment capability；Cable 和 OverheadLine 是两个 Terminal 之间的 Connection；Transformer 仅保留未来专业 Device 边界。

所有电气连接继续使用现有 Terminal、ElectricalNode 和 Connection 模型。目录不重新设计这些核心对象，不引入 BayFunction、GIS、SCADA、继保、潮流计算或自由 CAD 拓扑。
