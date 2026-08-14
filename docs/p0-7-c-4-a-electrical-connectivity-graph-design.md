# P0-7-C-4-A Electrical Connectivity Graph Design

> 状态：设计文档；本阶段不修改生产代码、测试、Persistence 或 Rendering。
> 目标：为 10kV 工作票绘图提供基于当前设备状态的最小电气连通查询。

## 1. Context and Boundary

当前 Domain 已有：

- `Device`、`RingCabinet`、`Pole`、`PoleAttachment`、`CableTermination`；
- `Terminal`、`ElectricalNode`、`Connection`、`OverheadLine`；
- `SwitchDevice`、`SwitchState` 和 RingCabinet 内部 `SwitchAssembly`。

本设计定义一个只读的 Electrical Connectivity Graph，用于回答工作票绘图需要的结构性问题：

- 两个 Terminal 当前是否连通；
- 某个 Terminal 当前可到达哪些 Terminal；
- 哪些设备之间存在由 Terminal 组成的电气路径；
- SwitchDevice 分合状态如何影响局部连通性。

它不表示潮流、负荷、电压传播或保护动作，不是配网仿真模型，也不根据画布线条、距离、方向或符号外观推断连接。

## 2. Graph Ownership and Construction

### 2.1 Recommended Layer

Graph 推荐作为 Application 层的只读查询模型或查询服务结果，而不是新的可变 Domain Aggregate：

```text
DrawingDocument (Domain source)
        ↓
ElectricalConnectivityGraphBuilder / Query Service (Application)
        ↓
ElectricalConnectivityGraph (read-only query model)
```

理由：

- 连通性是由当前 `DrawingDocument` 状态计算出的派生结果；
- Graph 不拥有 Device、Terminal、Node 或 Connection 的生命周期；
- 查询不应修改 Domain；
- 同一份 Domain 可在状态改变后重新构建新 Graph；
- Application 可以组合 RingCabinet、Pole、外部 Connection 和未来设备，而不让 Domain 依赖查询实现。

若未来某类连通性不变量需要成为创建/恢复时的强制规则，仍应回收到 Domain；本 Graph 不替代 Domain validation。

### 2.2 Snapshot Semantics

一次查询应针对一个固定快照：

1. 读取当前 `DrawingDocument` 的设备、Terminal、ElectricalNode、Connection、SwitchAssembly 和 SwitchState；
2. 构建不可变 Graph；
3. 在该 Graph 上执行查询；
4. Domain 后续发生变化时，旧 Graph 不自动改变。

Graph 构建期间不得生成 Stable ID、创建 Domain 对象、修改 `ElectricalNode`，或调用 Command/Undo/Redo。

## 3. Vertex Decision

### 3.1 Alternatives

#### Option A: ElectricalNode vertices

优点是天然表示等电位关系；缺点是当前部分 Terminal 没有 `ElectricalNodeId`，尤其 Pole Anchor Terminal，且 SwitchDevice 两端必须保持为两个可区分的连接点。

#### Option B: Terminal vertices

每个可查询 Terminal 都能成为顶点，能覆盖无 Node 的端子并直接支持 `IsConnected(TerminalId, TerminalId)`。固定 Node 关系需要额外转换为边或等价关系。

#### Option C: 混合顶点

同时把 Terminal 和 ElectricalNode 作为公开顶点。表达力高，但会让调用方区分两种身份，增加查询 API、路径结果和重复关系的复杂度。

### 3.2 Recommendation: Terminal-Centric Graph

推荐使用 Terminal 作为 Graph 的公开顶点，ElectricalNode 作为固定连通关系的来源，而不是第二类公开顶点：

```text
Terminal vertex A ── fixed-node edge ── Terminal vertex B
Terminal vertex A ── connection edge ── Terminal vertex B
Terminal vertex A ── closed-switch edge ── Terminal vertex B
```

这样可以：

- 保留没有 `ElectricalNodeId` 的 Pole Anchor Terminal；
- 保留 SwitchDevice 两端的独立身份；
- 直接以 TerminalId 作为稳定查询输入；
- 不把 `ElectricalNode.TerminalIds` 反向索引暴露成另一套顶点身份。

Graph 内部可以保存 Node 元数据和 Terminal 到 Node 的映射，供解释路径和诊断使用，但第一版公开查询以 TerminalId 为主。

### 3.3 Terminal Validity

构建时应拒绝或报告引用错误，而不是猜测修复：

- Connection 引用不存在的 Terminal；
- Terminal 引用不存在的 ElectricalNode；
- Node 的 Terminal 反向索引与 Terminal 的 `ElectricalNodeId` 不一致；
- 同一 Terminal 被不允许的结构重复使用。

这类错误属于 Domain/Restore contract 问题。Graph 不应静默删除坏边或自动创建缺失节点。

## 4. Edge Model

Graph 的边至少分为三类。边应保留来源 ID，便于工作票解释“为什么连通”。

### 4.1 Fixed ElectricalNode Edges

将指向同一个 `ElectricalNodeId` 的 Terminal 视为固定连通。对同一 Node 中的 Terminal 建立逻辑连通关系；实现上可使用 Node 组索引避免物理上生成完全图，但对查询语义等价。

该边表示设备或固定结构内部的等电位关系，不是外部 Cable，也不生成 Connection。典型例子包括：

- CableTermination 的内部 Node 连接其 Cable-side 与 Overhead-side Terminal；
- RingCabinet 固定结构中属于同一内部 Node 的 Terminal。

Node 没有关联 Terminal 时不能被 Graph 当作可达顶点；它仍可作为诊断元数据。

### 4.2 Connection Edges

每个 `Connection` 产生一条连接两个 endpoint Terminal 的固定边：

- `ConnectionType.Cable` 表示电缆外部连接；
- `ConnectionType.OverheadLine` 表示架空线路外部连接；
- `OverheadLine` 是对应 Connection 的专业明细，不是第二条电气边。

边引用 `Connection.Id` 和 `Connection.Type`，不根据 `DisplayName`、LineModel、长度或 SupportPoleIds 推断额外连通关系。架空线路连接的是两个 Terminal；杆塔支撑序列本身不自动把所有杆塔互相连通。

### 4.3 Dynamic Switch Edges

对每个具有两个 Terminal 的 `SwitchDevice`：

- `SwitchState.Closed`：增加一条来源为该 SwitchDevice 的动态边；
- `SwitchState.Open`：不增加该边。

因此同一 Domain 对象可以在不同状态快照生成不同 Graph，而不修改其 Terminal 或 Connection。SwitchState 是查询时的动态因素，但不等于电压或带电状态。

Graph 不允许绕过 `SwitchAssembly` 自己推导柜内合法状态。输入状态必须来自已经通过 Domain 规则的当前对象；若要执行状态变更，调用方仍使用 `DrawingDocument.ChangeSwitchState`，柜内仍由 `SwitchAssembly.ChangeSwitchState` 负责联锁。

## 5. RingCabinet Compatibility

RingCabinet 是固定内部拓扑聚合。Graph 应消费其已经创建和校验的：

- MainBus、Circuit、Earth 等 `ElectricalNode`；
- Interval 的 SwitchDevice 和 Terminal；
- 固定结构的外部 Terminal；
- `SwitchAssembly` 和当前 SwitchState。

Graph 不复制以下规则：

- LoadSwitch 三位置联锁；
- IntegratedFeeder 的隔离、断路器、接地互斥规则；
- GroundingStructureKind 对内部结构的创建规则；
- RingCabinet Restore/Validate 规则。

推荐流程是：Domain 创建/恢复/状态操作先保证聚合有效，Application Graph Builder 再读取有效快照。若 Graph 发现 Assembly 状态无效，应返回明确的 invalid snapshot/diagnostic，而不是自行选择另一组状态或继续推断。

## 6. Query API Design

第一版 API 应保持面向工作票的最小集合。具体类型名可在实现阶段按项目风格确定，建议形状如下：

```csharp
public interface IElectricalConnectivityQuery
{
    bool IsConnected(Guid firstTerminalId, Guid secondTerminalId);

    IReadOnlySet<Guid> FindConnectedTerminalIds(Guid terminalId);

    IReadOnlyList<ElectricalConnectivityPath> FindPaths(
        Guid firstTerminalId,
        Guid secondTerminalId);
}
```

Graph 本身可以直接实现该查询接口，或由查询服务持有 Graph。建议明确区分：

- `IsConnected`：是否存在至少一条当前可用路径；
- `FindConnectedTerminalIds`：从指定 Terminal 可达的 Terminal 集合；
- `FindPaths`：可选的解释能力，返回边来源和边类型，避免只返回布尔值。

查询约定：

- 起点或终点不存在时返回明确的 not-found 结果或抛出参数错误，不能把不存在误报为不连通；
- 同一 Terminal 查询自身可视为连通，但该语义必须在 API 中固定；推荐 `IsConnected(a, a) == true`，前提是 `a` 存在；
- 路径不应重复 Terminal 或边；
- 不因路径数量、环路或 Connection 顺序改变结果；
- 返回集合不可由调用方修改。

第一版不提供“是否带电”“是否停电”“上游/下游”“负荷侧/电源侧”等 API。

## 7. Device-Level Queries

设备级查询是 Terminal 查询的派生便利能力，不建立第二套图：

```csharp
bool IsDeviceConnected(Guid firstDeviceId, Guid secondDeviceId);
```

实现应先解析设备拥有的相关 Terminal，再复用 Terminal 图查询。设备存在多个 Terminal 时，必须明确是“任意端子存在路径”还是“指定端子存在路径”；第一版推荐只提供明确的 Terminal API，设备级 API 延后，避免隐藏路径语义。

PoleAttachment 只表达 Pole 与附属设备的安装/所有权关系，不自动产生电气边。附属 SwitchDevice 的两个 Terminal 只有在 Closed 或通过固定 Node/Connection 可达时才参与相应路径。

## 8. Cable, OverheadLine, and CableTermination

当前外部连接规则保持不变：

- Cable/OverheadLine Connection 连接两个 external Terminal；
- `CableTermination` 是 Device，其内部 Node 将两侧 Terminal 固定连通；
- Pole 的 Overhead Anchor Terminal 是连接端点，不是由每一条线路自动创建的 Node；
- OverheadLine 的 support pole 列表描述支撑物理顺序，不取代 Connection endpoint；
- 未实现的 Cable 或 OverheadLine 不应由 Graph 虚构。

因此典型路径为：

```text
RingCabinet external Terminal
  -- Cable Connection --
CableTermination cable-side Terminal
  -- fixed ElectricalNode edge --
CableTermination overhead-side Terminal
  -- OverheadLine Connection --
Pole overhead-anchor Terminal
```

如果端子不允许该 ConnectionType，构建阶段应暴露 Domain 数据错误，而不是改变端点或自动补连接。

## 9. Switch State and Operations

Graph 只读取 SwitchState。状态变更仍通过已实现的 Domain/Application 操作链：

```text
ChangeSwitchStateCommand
        ↓
DrawingDocument.ChangeSwitchState
        ├─ Pole switch: controlled Domain state change
        └─ Cabinet switch: SwitchAssembly.ChangeSwitchState
        ↓
new connectivity snapshot
```

Execute、Undo、Redo 之后应重新构建或刷新 Graph snapshot。旧 snapshot 不应自动追踪状态变化，避免读者在同一对象上混用旧状态和新路径。

本阶段不增加拓扑传播、带电判断、自动跳闸或保护逻辑。SwitchDevice 的 Open/Closed 只表达其两端是否存在结构性开关边。

## 10. Result Consistency and Diagnostics

Graph Builder 应提供可审查的诊断边界：

- 缺少 endpoint Terminal；
- 非法 Node 引用；
- Connection endpoint 不符合 Terminal policy；
- SwitchDevice 不是两个 Terminal；
- SwitchState 无效；
- RingCabinet Assembly 状态未通过既有 Domain 校验。

诊断应保留对象类型和 Stable ID。不得以默认 Open、默认无 Node 或自动连接来隐藏错误。

构建结果只读，至少保证：

- 顶点集合不可修改；
- 边集合不可修改；
- Path 结果不可修改；
- 不暴露 Domain 内部可变集合以供调用方写入。

## 11. Persistence and Command Boundary

Electrical Connectivity Graph 是派生查询结果，第一版不新增 DTO、不新增保存字段、不新增 FormatVersion，也不把 Graph 序列化到 V5 工程文件。

Persistence 继续保存 Domain 事实：Device、Terminal、ElectricalNode、Connection、SwitchState 以及各自 Stable IDs。加载后从恢复的 `DrawingDocument` 重新构建 Graph。

Command、Undo/Redo 和 Dirty 继续作用于 Domain 事实。Graph 不进入 CommandStack，不拥有独立 Undo/Redo，也不直接设置 Dirty。

## 12. Rendering and Desktop Boundary

Rendering/WPF 可以消费查询结果显示连通路径或诊断，但不能成为 Graph 的构造者，也不能从符号相交或坐标接触推断电气连接。

Desktop 后续可在工作票操作前读取：

- 目标 Terminal 是否连通；
- 开关操作前后路径是否变化；
- 连接缺失或结构不完整的诊断。

本设计不实现点击开关、路径高亮、拓扑面板或任何 UI。

## 13. Stable IDs and Lifecycle

Graph 不生成 ID。所有顶点和边均引用现有稳定身份：

- TerminalId；
- ElectricalNodeId；
- ConnectionId；
- SwitchDeviceId。

同一个 Domain 快照重建 Graph 时，引用必须保持一致。Command、Undo/Redo、V5 Save/Reload 后，只要 Domain Stable IDs 不变，Graph 查询语义应可重建且不依赖对象引用地址。

## 14. Testing Strategy

实现阶段至少覆盖：

1. 两个同一 ElectricalNode 的 Terminal 连通；
2. 无 ElectricalNode 的 Pole Anchor Terminal 仍可作为顶点；
3. Cable Connection 连接两个 Terminal；
4. OverheadLine 只通过 Connection endpoint 形成边；
5. CableTermination 两侧 Terminal 通过内部 Node 连通；
6. Open Switch 不形成动态边；
7. Closed Switch 形成动态边；
8. Open→Closed 后新 Graph 可达，Undo 后恢复原结果；
9. RingCabinet 状态变化仍受既有 SwitchAssembly 联锁约束；
10. 多个 PoleAttachment 不因安装关系自动连通；
11. Save/Reload 后基于 Stable IDs 得到相同连通查询结果；
12. 非法 Terminal、Node、Connection 引用被报告而不是静默修复。

测试不应把带电传播、潮流方向或保护动作当作 Graph 通过条件。

## 15. Non-Goals

当前不实现：

- 配网仿真或潮流计算；
- 电压、电流、带电范围传播；
- 继电保护、自动跳闸或故障计算；
- SCADA、GIS 或实时遥测；
- Incoming/Outgoing/Tie/SourceSide/LoadSide 推断；
- 自由 CAD 拓扑；
- 自动生成缺失的 Cable、OverheadLine、Terminal 或 Node；
- PT、DTU 等尚未实现的设备结构。

## 16. Final Architecture Decision

1. Graph 属于 Application 查询/只读模型，由 `DrawingDocument` 临时构建。
2. Graph 以 Terminal 为公开顶点，以 ElectricalNode 形成固定 Terminal-to-Terminal 关系。
3. Connection 形成外部固定边；Closed SwitchDevice 形成动态边；Open SwitchDevice 不形成边。
4. RingCabinet 的联锁和状态合法性继续由 `SwitchAssembly` 负责，Graph 不复制该算法。
5. PoleAttachment 只表达安装组合，不自动表达电气连接。
6. Graph 不修改 Domain、不生成 Stable ID、不进入 Persistence 或 CommandStack。
7. 第一版只回答结构性连通问题，不回答潮流、带电、方向或保护问题。
