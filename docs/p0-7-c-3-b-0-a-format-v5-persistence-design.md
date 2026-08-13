# P0-7-C-3-B-0-A FormatVersion 5 Persistence Design

## 1. Context

P0-7-C-3-B-0 已确认一个明确的工程持久化缺口：Pole、PoleAttachment 和柱上 SwitchDevice 已能进入 `DrawingDocument`，并支持 Stable ID、Execute、Undo 和 Redo，但当前 Project FormatVersion 4 无法保存顶层 SwitchDevice。

本设计冻结 FormatVersion 5 的最小持久化合同。V5 不重新设计 Domain，也不增加设备行为；它只让当前已经存在的柱上 SwitchDevice 及其状态可以完整 Save、Restore 和 round trip。

本阶段只新增设计文档，不修改生产代码、测试或格式版本。

## 2. V4 Current State

### 2.1 Supported Objects

V4 已支持以下工程事实：

- RingCabinet aggregate；
- RingCabinetInterval；
- RingCabinet 内部 SwitchAssembly 和 SwitchDevice；
- Pole；
- PoleAttachment 关系；
- CableTermination；
- Terminal；
- ElectricalNode；
- Connection；
- OverheadLine；
- Stable IDs 和当前拓扑引用。

RingCabinet 内部开关通过嵌套结构保存：

```text
ProjectRingCabinetDto
  └─ ProjectRingCabinetIntervalDto
       └─ ProjectSwitchDeviceDto[]
```

现有 `ProjectSwitchDeviceDto` 已包含：

- `DeviceId`；
- `SwitchKind`；
- `InstallationType`；
- `FirstTerminalId`；
- `SecondTerminalId`；
- `SwitchState`；
- `DisplayName`；
- `VoltageLevel`；
- `DispatchNumber`。

### 2.2 V4 Gap

柱上开关在 Domain 中是顶层 Device，通过 PoleAttachment 与 Pole 组合：

```text
Pole
  └─ PoleAttachment
       └─ SwitchDevice (InstallationType.Pole)
```

V4 `ProjectDomainMapper.ToDto` 会识别并跳过已嵌套保存的 RingCabinet switches，但对其余 SwitchDevice 明确抛出 `NotSupportedException`。`ProjectPoleAttachmentDto` 只保存 `AttachmentId`、`PoleId` 和 `AttachedDeviceId`，不能替代被引用的 SwitchDevice 数据。

因此 V4 当前缺少：

- top-level SwitchDevice 的 DTO collection；
- top-level SwitchDevice 保存映射；
- top-level SwitchDevice 恢复映射；
- PoleAttachment → SwitchDevice 完整 round trip；
- 柱上 SwitchState 的 Save/Reload 闭环。

## 3. V5 Goals

V5 目标仅为完整持久化已有 Domain 对象：

```text
DrawingDocument
  Device
  SwitchDevice
  Terminal
  ElectricalNode
  PoleAttachment
  Connection
```

V5 保持以下模型不变：

- `Device`；
- `SwitchDevice`；
- `SwitchKind`；
- `SwitchState`；
- `Terminal`；
- `ElectricalNode`；
- `Connection`；
- `Pole`；
- `PoleAttachment`；
- RingCabinet 和 SwitchAssembly。

V5 不改变创建流程、CommandStack、Undo/Redo、Rendering 或 Desktop。Persistence 只保存和恢复最终工程事实。

## 4. V5 DTO Contract

### 4.1 ProjectDomainDto Extension

V5 在 `ProjectDomainDto` 增加独立的 top-level switch collection：

```text
IReadOnlyList<ProjectSwitchDeviceDto>? SwitchDevices
```

该字段应作为可选尾部参数加入 record，以便 V4 payload 在 V4→V5 migration 后和 DTO 反序列化时有明确默认。V5 正常保存必须输出 collection；没有柱上开关时输出空数组，而不是省略业务含义。

### 4.2 Reuse ProjectSwitchDeviceDto

V5 复用现有 `ProjectSwitchDeviceDto`，不新增重复 DTO。字段合同如下：

| Field | V5 contract |
| --- | --- |
| `DeviceId` | SwitchDevice Stable ID |
| `SwitchKind` | Breaker、LoadSwitch、Disconnector、Fuse 等现有枚举映射 |
| `InstallationType` | top-level collection 中必须为 `Pole` |
| `FirstTerminalId` | 第一个稳定 Terminal ID |
| `SecondTerminalId` | 第二个稳定 Terminal ID，且不能与第一个相同 |
| `SwitchState` | `open` 或 `closed` |
| `DisplayName` | 可选显示名称 |
| `VoltageLevel` | 当前设备电压等级，必须满足 Domain 规则 |
| `DispatchNumber` | 可选调度编号 |

复用同一 DTO 能确保柜内和柱上 SwitchDevice 使用相同的字段编码，同时由容器位置表达 aggregate boundary。

### 4.3 No ProjectDeviceDto Expansion

V5 不把 SwitchKind、InstallationType 和 Terminal IDs 塞入通用 `ProjectDeviceDto`。这样可以避免：

- 大量 nullable specialized fields；
- base SwitchState 与 switch detail 的重复事实源；
- 不完整的普通 Device 被误恢复为 SwitchDevice；
- RingCabinet nested switch 与 top-level switch 使用不同 DTO 合同。

### 4.4 PoleAttachment Reference

`ProjectPoleAttachmentDto` 保持不变：

```text
AttachmentId
PoleId
AttachedDeviceId
```

当 attachment 表示柱上开关能力时：

- `PoleId` 必须指向 V5 `devices` 中已恢复的 Pole；
- `AttachedDeviceId` 必须指向 V5 `switchDevices` 中已恢复的 SwitchDevice；
- 该 SwitchDevice 的 `InstallationType` 必须为 Pole；
- 同一 SwitchDevice 不能被多个 PoleAttachment 引用。

Attachment 不复制 SwitchKind、SwitchState 或 Terminal ID。SwitchDevice DTO 是这些事实的唯一持久化来源。

## 5. V5 Save Structure

### 5.1 Logical JSON Shape

V5 `document.json` 的 Domain 部分采用现有按 aggregate/type 分组结构：

```json
{
  "domain": {
    "documentId": "...",
    "title": "...",
    "devices": [
      { "deviceKind": "pole" },
      { "deviceKind": "cable-termination" }
    ],
    "switchDevices": [
      {
        "deviceId": "...",
        "switchKind": "load-switch",
        "installationType": "pole",
        "firstTerminalId": "...",
        "secondTerminalId": "...",
        "switchState": "open",
        "displayName": "...",
        "voltageLevel": "10kV",
        "dispatchNumber": null
      }
    ],
    "ringCabinets": [],
    "electricalNodes": [],
    "terminals": [],
    "poleAttachments": [
      {
        "attachmentId": "...",
        "poleId": "...",
        "attachedDeviceId": "..."
      }
    ],
    "connections": [],
    "overheadLines": []
  }
}
```

以上仅展示结构，不冻结 JSON 属性物理排序。引用完整性由 ID 和 Domain validation 决定，不能依赖数组位置。

### 5.2 Save Classification

保存时必须先取得 RingCabinet 内部 SwitchDevice ID 集合，并对 `DrawingDocument.Devices` 中的 SwitchDevice 分类：

```text
RingCabinet internal SwitchDevice
  → only RingCabinet interval DTO

SwitchDevice with InstallationType.Pole
  → only top-level switchDevices collection
```

同一 SwitchDevice 不能同时写入两个位置。任何不属于 RingCabinet、且 InstallationType 不是 Pole 的 SwitchDevice 必须明确失败，不能被静默归类。

### 5.3 Why a Separate Collection

独立 collection 符合当前 V4 结构：

- RingCabinet aggregate 独立保存；
- Pole、CableTermination 等顶层设备已在 `devices`；
- topology objects 独立保存；
- PoleAttachment 独立保存关系。

它让 SwitchDevice 专用字段保持集中，同时不改变 Domain ownership。PoleAttachment 仍是 Pole 与附属设备之间的关系事实，不承担设备数据。

## 6. V5 Restore Contract

### 6.1 Restore Is Not Creation

V5 恢复禁止调用：

- `PoleCreationFactory`；
- 用户创建 recipe；
- Template Builder；
- Rendering Layout Builder；
- Command；
- `Guid.NewGuid()`。

Persistence mapper 可以调用受控 Domain restore/factory API来实例化对象，但必须使用 DTO 中已有的 ID、kind、state、metadata 和 topology references。它不能推断或补造缺失对象。

### 6.2 Frozen Registration Order

所有权图不是注册顺序。根据当前 `DrawingDocument` 验证规则，V5 冻结以下恢复顺序：

```text
1. Restore independent top-level devices
   - Pole
   - CableTermination
   - other supported basic devices

2. Restore top-level SwitchDevice
   - preserve DeviceId
   - require InstallationType.Pole
   - preserve both declared Terminal IDs
   - preserve SwitchState

3. Restore RingCabinet aggregates
   - internal switches remain inside RingCabinet restore
   - preserve existing SwitchAssembly interlock validation

4. Restore top-level ElectricalNode

5. Restore top-level Terminal
   - owner Device must already exist
   - terminal IDs must match the owner SwitchDevice declaration

6. Restore PoleAttachment
   - Pole and attached Device must already exist

7. Restore Connection
   - both Terminal endpoints must already exist

8. Restore OverheadLine and later dependent sections

9. Validate the complete restored aggregate and topology
```

Terminal 必须在其 owner SwitchDevice 之后恢复；PoleAttachment 可以在 Terminal 前注册，但为了形成统一、易审查的依赖顺序，V5 固定为 Terminal 后、Connection 前。

### 6.3 Stable ID Rules

V5 restore 必须逐项保持：

- Document ID；
- Pole ID；
- SwitchDevice ID；
- both Terminal IDs；
- optional ElectricalNode ID；
- PoleAttachment ID；
- Connection ID；
- RingCabinet、Interval、SwitchAssembly 和内部 Switch IDs。

V5 Restore、migration 和 validation 都不能生成 Stable ID。缺失 ID 或引用不一致时应失败，而不是修复文件。

### 6.4 Strict Validation

恢复和最终 validation 至少检查：

- top-level SwitchDevice 的 InstallationType 为 Pole；
- SwitchKind 和 SwitchState 是受支持值；
- DeviceId 和两个 Terminal ID 非空、互不冲突；
- Terminal DTO owner 是该 SwitchDevice；
- SwitchDevice 声明的两个 Terminal 都存在且没有额外声明不一致；
- PoleAttachment 指向现有 Pole 和现有附属设备；
- SwitchDevice 只附着到一个 Pole；
- Connection 只引用存在且允许该 ConnectionType 的 Terminal；
- 全文档 Stable ID 唯一；
- RingCabinet nested switches 不出现在 top-level collection。

不能因为 V5 增加 SwitchDevice collection 而放宽 V4 已有 Terminal、CableTermination、RingCabinet、Connection 或 topology invariant。

## 7. V4 to V5 Migration

### 7.1 Version Constants

实施阶段目标版本常量为：

```text
Version1 = 1
Version2 = 2
Version3 = 3
Version4 = 4
Version5 = 5
CurrentVersion = Version5
```

历史 migration 必须继续设置明确目标版本，不能用 `CurrentVersion` 代替 Version2、Version3 或 Version4。

### 7.2 Sequential Migration Chain

继续采用顺序迁移：

```text
V1 → V2 → V3 → V4 → V5
V2 → V3 → V4 → V5
V3 → V4 → V5
V4 → V5
V5 → no migration
```

不增加 V1/V2/V3 直接跳到 V5 的 shortcut。

### 7.3 V4ToV5 Operation

旧 V4 从未支持保存 top-level SwitchDevice，因此 migration 没有可恢复的柱上开关数据。V4→V5 只应：

```text
domain.switchDevices = []
version = Version5
```

如果 payload 没有 Domain section，则沿用当前 migration 对可选 Domain 的处理，不创建虚假 Domain。

Migration 禁止：

- 生成 SwitchDevice；
- 生成或改变 Stable ID；
- 从 PoleAttachment、Terminal、名称或布局推断 SwitchDevice；
- 修改 RingCabinet；
- 修改 Pole、CableTermination、Terminal、Connection 或 topology；
- 调用 Domain CreationFactory、Template Builder 或 Command。

### 7.4 Historical Compatibility

V1–V4 文件仍按现有历史规则逐步迁移。V5 实施不能改写：

- V1→V2 Professional section 历史合同；
- V2→V3 BayIndex/legacy function 历史合同；
- V3→V4 BayFunction 删除合同；
- V4 已保存 RingCabinet、Pole、CableTermination、Terminal、Node、Attachment 和 Connection 的含义。

迁移到 V5 后，上述对象的 DTO 值和 Stable IDs 必须保持不变。

## 8. V5 Save and Reload Behavior

V5 Save 必须：

- 写入 `formatVersion = 5`；
- 写入 top-level `switchDevices` array；
- 保存柱上 SwitchDevice 的当前 Open/Closed 状态；
- 保存全部 Switch metadata 和 Terminal IDs；
- 保持 PoleAttachment 引用；
- 不重复写 RingCabinet internal switch；
- 不写任何运行时 Scene、Selection、Command history 或 Template source state。

V5 Reload 必须恢复同一：

- Pole；
- SwitchDevice；
- SwitchKind；
- SwitchState；
- Terminal；
- PoleAttachment；
- Connection；
- Stable ID graph。

再次保存不得改变对象身份或把 top-level switch 移入 RingCabinet DTO。

## 9. Relationship to Later Capabilities

V5 完成后，以下能力获得持久化前置条件：

- 柱上 SwitchDevice 的 Open/Closed 状态保存；
- `ChangeSwitchStateCommand` 的结果跨 Save/Reload 保留；
- PoleAttachment 与 SwitchDevice 的工程恢复；
- 基于 Terminal、Connection 和 SwitchState 的后续只读拓扑查询；
- 工作票绘图中开关状态和连接关系的一致恢复。

V5 本身不实现 ChangeSwitchStateCommand，也不执行拓扑传播。它只保存操作完成后的 Domain 状态。

## 10. Testing Design

### 10.1 Version and Migration

至少覆盖：

1. `CurrentVersion == 5`；
2. V4 文件成功打开并迁移到 V5；
3. V4→V5 只增加空 `switchDevices`，不改变其他字段；
4. V1、V2、V3 完整 migration chain 继续到 V5；
5. V5 不重复执行 migration；
6. unsupported future version 继续拒绝。

### 10.2 V5 Round Trip

至少覆盖：

1. Pole + SwitchDevice + PoleAttachment 保存恢复；
2. SwitchDevice ID 保持；
3. first/second Terminal IDs 保持；
4. PoleAttachment ID、PoleId、AttachedDeviceId 保持；
5. Open state round trip；
6. Closed state round trip；
7. SwitchKind、InstallationType、DisplayName、VoltageLevel、DispatchNumber 保持；
8. Connection endpoint IDs 保持；
9. 一个 Pole 同时包含 SwitchDevice 和 CableTermination attachment 时完整恢复；
10. RingCabinet internal switches 仍只保存一次且 Stable IDs 保持。

### 10.3 Strict Failure Cases

至少覆盖：

- top-level switch 使用 CabinetInterval installation type；
- SwitchKind 或 SwitchState 非法；
- 两个 Terminal ID 相同；
- 缺失 declared Terminal；
- Terminal owner 与 SwitchDevice 不一致；
- PoleAttachment 指向不存在的 Pole 或 SwitchDevice；
- 同一 SwitchDevice 被重复 attachment；
- SwitchDevice 同时出现在 nested 和 top-level collection；
- Connection 引用不存在的 Terminal；
- duplicate Stable ID。

测试应通过真实 archive/DTO/Domain round trip 验证，不只测试独立 JSON helper。

## 11. Implementation Scope Plan

未来实现建议保持一个可编译闭环，修改范围预计为：

- `ProjectFileFormat`：增加 Version5；
- `ProjectFormatMigration`：增加 V4→V5；
- `ProjectDomainDto`：增加 top-level SwitchDevices collection；
- `ProjectDomainMapper`：top-level SwitchDevice save/restore、顺序和 validation；
- 必要的 Domain restore入口：仅当当前 public/internal API 无法无损恢复柱上 SwitchDevice；
- Infrastructure.Tests：migration、round trip、strictness、Stable ID；
- 直接受 CurrentVersion 影响的现有测试。

不计划修改 Rendering.Wpf、Desktop、Template、CommandStack 或 Project Layout DTO。

## 12. Non-Goals

V5 不包含：

- ChangeSwitchStateCommand 实现；
- 开关操作联锁扩展；
- 拓扑状态传播或带电判断；
- 自动配网分析；
- SCADA；
- GIS；
- 继电保护；
- 潮流或短路计算；
- 自由 CAD；
- 新设备类型；
- UI 或 Rendering symbol；
- Template persistence；
- Command history persistence。

## 13. Risks

- RingCabinet internal switch 被重复写入 top-level collection；
- restore 顺序错误导致 Terminal owner、Attachment target 或 Connection endpoint 不存在；
- 复用 DTO 时未限制 top-level InstallationType；
- migration 生成对象或 ID，掩盖旧文件损坏；
- V5 只保存 SwitchState，却遗漏 SwitchKind、Terminal IDs 或 metadata；
- optional collection 被保存为 null，导致 V5 schema 表达不稳定；
- 为兼容错误 payload 而跳过悬空 Attachment；
- 新 restore入口绕过 Domain invariant；
- 后续 ChangeSwitchState 直接修改 RingCabinet switch，绕过 SwitchAssembly interlock。

## 14. Decision Summary

1. FormatVersion 5 被选为 top-level SwitchDevice persistence 的明确 schema boundary。
2. V5 不改变 Device、Terminal、Connection、ElectricalNode、SwitchDevice 或 PoleAttachment Domain 模型。
3. V5 在 ProjectDomainDto 增加 top-level `ProjectSwitchDeviceDto` collection，并复用现有 DTO。
4. RingCabinet internal switches 继续只嵌套保存在 interval DTO；Pole switches 只保存在 top-level collection。
5. PoleAttachment 继续只通过 Stable IDs 引用 Pole 和 SwitchDevice。
6. 冻结恢复顺序为 Device → top-level SwitchDevice → RingCabinet → Node → Terminal → PoleAttachment → Connection → dependent data → validation。
7. V4→V5 只补空 switch collection，不生成 ID、不推断设备、不改变拓扑。
8. V1–V4 文件继续通过顺序 migration chain 读取。
9. V5 round trip 必须保持 SwitchState 和完整 Stable ID graph。
10. V5 实现与验证完成后，才恢复 P0-7-C-3-B-1 Switch State Domain Operation。
