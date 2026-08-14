# P0-8-C Pole Rendering Design

## 1. Context and Goals

本阶段为 10kV 工作票绘图定义杆塔类设备的第一版 Rendering 模型。

目标是把已经存在的 Pole、PoleAttachment、SwitchDevice、CableTermination 投影为可读的图形场景，支持普通杆、杆上开关、电缆终端以及多 Attachment 组合。

本阶段只设计 Rendering 边界，不实现符号、布局、选择或 UI。

## 2. Rendering Boundary

Domain 保存电气事实和安装关系：

- `Pole`：杆塔主体及其稳定身份；
- `PoleAttachment`：附属对象与 Pole 的安装关系；
- `SwitchDevice`：具有 `SwitchKind` 和 `SwitchState` 的开断设备；
- `CableTermination`：电缆终端相关的拓扑端点设备事实。

Rendering 消费这些对象并生成：

- Pole Symbol；
- Attachment Layout；
- Switch Symbol；
- Cable Terminal Symbol；
- Terminal Anchor 和 SceneElement。

Rendering 不得创建、修改或删除 Domain 对象，不得生成 Stable ID，也不得通过坐标推断电气连接或设备类型。未来用户操作必须经过 Application/Command 边界。

## 3. Pole Template Model

第一版使用以下显示层概念：

- `PoleTemplate`：定义一类 Pole 的显示结构；
- `PoleSymbol`：定义杆塔主体图形；
- `AttachmentTemplate`：定义某类附属能力的显示组合；
- `AttachmentLayout`：定义 Attachment 相对于 Pole 的位置、方向和尺寸；
- `TerminalAnchorLayout`：定义端子在图形中的连接锚点。

这些对象属于 Rendering 层，不替代 Domain 的 Pole 或 PoleAttachment，也不参与工程事实持久化。

一个 Pole 可以拥有零个、一个或多个 Attachment。Rendering 应逐个读取 Attachment 并组合显示，不能把 Attachment 组合折叠成新的 Domain 设备类型。

## 4. Supported Pole Templates

### 4.1 普通杆

结构：

```text
Pole
```

只显示 Pole 主体、编号和基础端子锚点。

### 4.2 电缆终端杆

结构：

```text
Pole
└── CableTermination Attachment
```

Pole Symbol 表示杆塔主体，CableTermination Symbol 表示电缆终端能力及其连接锚点。Attachment 是安装关系，不自动创建新的电气 Connection。

### 4.3 隔离刀闸杆

结构：

```text
Pole
└── Disconnector SwitchDevice Attachment
```

Rendering 根据附属 `SwitchDevice.SwitchKind` 选择隔离刀闸符号，并根据 `SwitchState` 显示合、分状态。

### 4.4 断路器杆

结构：

```text
Pole
└── CircuitBreaker SwitchDevice Attachment
```

Rendering 显示断路器符号和当前状态，不实现保护动作、定值或跳闸原因。

### 4.5 隔离刀闸 + 电缆终端杆

结构：

```text
Pole
├── Disconnector SwitchDevice Attachment
└── CableTermination Attachment
```

这是组合场景，而不是新的“隔离刀闸电缆终端杆” Domain 类型。两个 Attachment 各自保留 Stable ID、安装关系和自己的显示布局；一个 Pole 可以同时展示开关符号与电缆终端符号。

## 5. Switch Rendering

Rendering 继续复用统一的 Switch Symbol Mapping：

| SwitchKind | Symbol |
| --- | --- |
| `CircuitBreaker` | 断路器符号 |
| `Disconnector` | 隔离刀闸符号 |
| `LoadSwitch` | 负荷开关符号 |
| `EarthSwitch` | 接地刀符号 |

状态显示规则：

- `SwitchState.Closed`：显示“合”；
- `SwitchState.Open`：显示“分”。

Rendering 只读取状态，不修改状态，也不执行联锁或操作顺序校验。隔离刀闸的带负荷操作约束仍由 Domain/Application 操作边界负责。

## 6. CableTermination Rendering

在杆塔 Rendering 语境中，CableTermination 表示 Attachment 对应的拓扑端点能力。

它不是：

- Pole；
- Pole 的替代类型；
- 新的杆塔主体；
- 用于表达电缆路径的独立图形设备。

CableTermination Symbol 应显示终端本体、连接方向和 Terminal Anchor。它不创建 Terminal、Connection 或 ElectricalNode；这些对象由 Domain 提供，Rendering 只根据既有身份生成图形锚点。

如果当前 Domain 中 `CableTermination` 仍作为可持久化 Device 表达，Rendering 应尊重现有 API，同时保持显示层语义：它代表 Pole 上的电缆终端能力，而不是另一个 Pole。

## 7. Layout Design

第一版布局分为三层：

- `PoleLayout`：杆体位置、宽度、高度、编号位置和主体端子锚点；
- `AttachmentLayout`：Attachment 相对于 Pole 的偏移、方向、尺寸和显示顺序；
- `TerminalAnchorLayout`：CableTermination、SwitchDevice 等端子的图形连接锚点。

布局规则：

1. 先确定 Pole 主体边界和主坐标系；
2. 再根据 Attachment 类型分配局部位置；
3. 多个 Attachment 使用稳定的注册/计算顺序；
4. 最后生成 Terminal Anchor，供未来 Cable/OverheadLine Rendering 使用；
5. 所有坐标、尺寸和锚点只存在于 Rendering/Layout，不回写 Domain。

布局不能根据左右位置、Sequence 或坐标推断 Incoming、Outgoing、Tie、Direction 或其他运行语义。

## 8. Selection Preparation

后续 Selection 需要能够识别：

- Pole；
- PoleAttachment；
- SwitchDevice；
- CableTermination；
- Cable/OverheadLine 的连接锚点。

每个可选图形应携带对应 Stable ID 和对象类别。Selection 本阶段不实现，也不在 Rendering 中创建替代 Domain 对象。

## 9. Layer Mapping

| Layer | Responsibility |
| --- | --- |
| Domain | Pole、Attachment、SwitchDevice、CableTermination、电气事实和稳定身份 |
| Application | 创建、操作、查询和未来 Selection 命令编排 |
| Rendering.Wpf | Pole Symbol、Attachment Symbol、Layout、Terminal Anchor、Scene |
| Desktop | Window、画布交互和未来选择入口 |
| Infrastructure | 工程事实的保存/恢复，不保存临时 Rendering Layout |

## 10. Non-Goals

本阶段不设计或实现：

- Rendering 创建 Domain 对象；
- 自由 CAD 编辑；
- 任意拓扑生成；
- GIS 空间分析；
- SCADA；
- 继电保护；
- 潮流或短路计算；
- PoleAttachment 编辑器；
- 鼠标点击开关操作；
- 用户自定义 Rendering 模板持久化。

## 11. Follow-up Slices

### P0-8-C-1 Pole Symbol Runtime

实现普通 Pole 的主体符号、编号和基础布局。

### P0-8-C-2 Switch Attachment Rendering

实现 CircuitBreaker、Disconnector、LoadSwitch、EarthSwitch Attachment 的符号映射和状态显示。

### P0-8-C-3 CableTermination Rendering

实现 CableTermination Symbol、端子锚点和与 Cable/OverheadLine 的显示连接边界。

### P0-8-C-4 Mixed Pole Rendering

实现一个 Pole 同时拥有 SwitchDevice Attachment 和 CableTermination Attachment 的组合布局与测试。

## 12. Final Decision

第一版 Pole Rendering 采用“Pole 主体 + 零个或多个 Attachment + 独立 Layout/Symbol”的模型：

- Pole 是 Domain 主体；
- PoleAttachment 是安装关系和能力组合；
- SwitchDevice 保持独立设备类型和状态；
- CableTermination 表示电缆终端能力及拓扑端点；
- Rendering 只投影 Domain 事实；
- Layout、Symbol 和 Anchor 不进入 Domain；
- 多 Attachment 组合不产生新的互斥 Pole 类型。

该边界可支持普通杆、电缆终端杆、柱上开关杆以及隔离刀闸与电缆终端组合杆，并为后续电缆、架空线和 Selection Rendering 保留稳定接口。
