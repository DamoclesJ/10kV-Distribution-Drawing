# P0-7-C-3-B-0 SwitchDevice Persistence Gap Analysis

## 1. Context

当前项目已经具备 Pole 主体、可组合 `PoleAttachment`、柱上 `SwitchDevice` 创建，以及基于同一对象和 Stable ID 的 Execute、Undo、Redo。P0-7-C-3-B-1 在设计通用开关状态操作时发现：运行时模型已经允许柱上开关进入 `DrawingDocument`，但 Project Persistence 无法保存该对象。

当前阻断不是 `SwitchState` Domain 模型缺失，而是顶层 `SwitchDevice` 尚未进入工程文件合同。保存包含柱上开关的文档时，`ProjectDomainMapper.ToDto` 会抛出：

```text
Top-level SwitchDevice DTO persistence is not implemented in M4-B-6-A.
```

本分析只确定缺口、候选 DTO 形状、恢复顺序和格式版本策略，不修改生产代码、测试或 FormatVersion。

## 2. Current Persistence State

### 2.1 Supported SwitchDevice

RingCabinet 内部开关已完整进入 V4 Persistence：

```text
ProjectRingCabinetDto
  → ProjectRingCabinetIntervalDto
    → ProjectSwitchDeviceDto[]
```

`ProjectSwitchDeviceDto` 已保存：

- `DeviceId`；
- `SwitchKind`；
- `InstallationType`；
- `FirstTerminalId`、`SecondTerminalId`；
- `SwitchState`；
- `DisplayName`；
- `VoltageLevel`；
- `DispatchNumber`。

恢复时，RingCabinet aggregate 使用这些值重建内部 SwitchDevice、SwitchAssembly、Terminal 和 ElectricalNode，并由 Domain aggregate 校验结构与联锁不变量。

### 2.2 Unsupported SwitchDevice

PoleAttachment 下的柱上开关属于 `DrawingDocument.Devices` 中的顶层 `SwitchDevice`：

```text
Pole
  └─ PoleAttachment
       └─ SwitchDevice (InstallationType.Pole)
```

当前保存映射只识别并跳过已经嵌套保存于 RingCabinet DTO 的开关。遇到其他 SwitchDevice 时直接失败。当前 `ProjectDeviceDto` 虽有通用 `SwitchState` 字段，但缺少 SwitchKind、InstallationType、两个 Terminal ID 和 DispatchNumber，不能无损表达柱上开关。

因此当前实际能力为：

| Capability | RingCabinet switch | Pole-attached switch |
| --- | --- | --- |
| Runtime creation | Supported | Supported |
| Stable ID | Supported | Supported |
| Undo/Redo | Supported | Supported |
| SwitchState in memory | Supported | Supported |
| Save | Supported | Not supported |
| Restore | Supported | Not supported |
| State round trip | Supported | Not supported |

### 2.3 Existing Related V4 Data

V4 已独立保存：

- Pole；
- `ProjectPoleAttachmentDto`；
- top-level Terminal；
- ElectricalNode；
- Connection；
- CableTermination attachment aggregate。

缺失的唯一核心对象是 PoleAttachment 所引用的 top-level SwitchDevice。PoleAttachment DTO 只保存关系 ID，不能替代附属设备 DTO。

## 3. DTO Design Options

### 3.1 Required Contract

无论采用哪种容器形状，柱上 SwitchDevice DTO 必须无损保存：

| Field | Reason |
| --- | --- |
| Stable `DeviceId` | Attachment、Terminal、Connection 和工作票边界的引用身份 |
| `SwitchKind` | 保留 Breaker、LoadSwitch、Disconnector、Fuse 的设备语义 |
| `InstallationType` | 明确它是 Pole switch，防止柜内开关误入顶层恢复路径 |
| Two terminal IDs | 与 Terminal DTO、Connection DTO 建立稳定引用 |
| `SwitchState` | 保存 Open/Closed 工程事实 |
| `DisplayName` | 用户可见名称 |
| `VoltageLevel` | Terminal 与连接电压校验所需事实 |
| `DispatchNumber` | 已有 SwitchDevice 业务属性 |

任何方案都不得根据 SwitchKind、Attachment、Terminal 顺序或显示名称重新生成这些字段。

### 3.2 Option 1: Reuse ProjectSwitchDeviceDto in a Top-Level Collection

在 `ProjectDomainDto` 增加独立的 top-level collection：

```text
SwitchDevices: ProjectSwitchDeviceDto[]
```

RingCabinet interval 继续嵌套使用同一个 DTO 类型；top-level collection 只保存不属于 RingCabinet aggregate 的柱上 SwitchDevice。

优点：

- 完全复用已经验证的 SwitchDevice DTO 字段和编码规则；
- 不把 Switch 专用字段塞进通用 `ProjectDeviceDto`；
- 不重复 DeviceId、DisplayName、VoltageLevel 等事实；
- 保存和恢复路径可以明确限制 `InstallationType.Pole`；
- 后续 SwitchState round-trip 测试可复用 RingCabinet switch 的断言模式。

缺点：

- `ProjectDomainDto` 会继续采用按 aggregate/type 分组的多集合结构；
- 保存映射必须严格排除 RingCabinet 内部 SwitchDevice，避免重复持久化。

### 3.3 Option 2: Extend ProjectDeviceDto with Switch Details

为 `ProjectDeviceDto` 增加可选的 Switch detail，例如：

```text
ProjectSwitchDeviceDetailsDto? SwitchDevice
```

优点：

- 所有顶层 Device 仍位于 `Devices` collection；
- PoleAttachment 的 AttachedDeviceId 可直接指向同一集合内对象。

缺点：

- 需要新增一个与现有 `ProjectSwitchDeviceDto` 高度重复的 details 类型，或让嵌套 DTO 重复 DeviceId、DisplayName、VoltageLevel；
- `ProjectDeviceDto` 的 nullable specialized fields 继续增长；
- 更容易出现 base fields 与 switch details 不一致的双重事实源。

### 3.4 Recommendation for DTO Shape

推荐 Option 1：在 Domain DTO 顶层增加 `ProjectSwitchDeviceDto` collection，并复用现有 DTO、编码和解析规则。

保存时应建立 RingCabinet 内部 Switch ID 集合，只有不在该集合内、且 `InstallationType == Pole` 的 SwitchDevice 才进入 top-level collection。任何未知 top-level installation type 应明确失败，不能静默保存为柱上开关。

## 4. Restore Order and Aggregate Registration

### 4.1 Ownership Graph

业务所有权关系为：

```text
Pole
  → PoleAttachment
    → SwitchDevice
      → Terminal
        → Connection
```

该图表达引用关系，不是可以直接执行的注册顺序。

### 4.2 Required Restore Order

`DrawingDocument.AddPoleAttachment` 要求 Pole 和 AttachedDevice 已存在；`AddTerminal` 要求 owner Device 已存在；`AddConnection` 要求两端 Terminal 已存在。因此正确恢复顺序应为：

```text
1. Restore and register Pole and other independent top-level devices
2. Restore and register top-level SwitchDevice
3. Restore RingCabinet aggregates
4. Restore ElectricalNode
5. Restore Terminal
6. Restore PoleAttachment
7. Restore Connection
8. Restore OverheadLine and later dependent data
9. Validate the complete topology
```

RingCabinet aggregate 可以继续在自己的恢复边界内一次恢复内部 SwitchDevice、SwitchAssembly、Terminal 和 Node。柱上 SwitchDevice 不得进入 RingCabinet restore path。

### 4.3 Stable ID Preservation

恢复必须直接使用 DTO 中的：

- SwitchDevice ID；
- first/second Terminal ID；
- PoleAttachment ID；
- Pole ID；
- Connection ID；
- 可选 ElectricalNode ID。

Persistence migration 和 restore 禁止调用 `Guid.NewGuid()`。恢复后的 PoleAttachment、Terminal 和 Connection 必须仍引用原 SwitchDevice ID。

### 4.4 Restore API Boundary

Infrastructure 可以根据 DTO 调用明确的 Domain restore/factory API来重建对象，但不能承担业务创建策略。两者的区别是：

- Restore 使用文件中的全部 Stable IDs 和状态；
- Creation 只在首次用户操作中生成 ID；
- Restore 不推断 SwitchKind、Terminal、InstallationType 或 SwitchState；
- Restore 不自动生成 Attachment、Connection 或缺失 Terminal；
- Domain API 继续验证枚举、Terminal ownership、InstallationType 和 aggregate consistency。

因此“Persistence 不创建业务对象”应理解为：Persistence 不发明新的工程事实或执行用户创建 recipe；它仍必须完成 DTO → Domain restore 实例化。

## 5. V4/V5 Strategy Analysis

### 5.1 Option A: Compatible V4 Extension

做法：保持 `CurrentVersion = 4`，为 `ProjectDomainDto` 增加可选 top-level SwitchDevices collection。旧 V4 文件缺少该字段时按空集合读取。

优点：

- 旧 V4 文件无需 migration；
- 修改范围较小；
- 当前 `JsonUnmappedMemberHandling.Skip` 允许新读取器接受旧 payload；
- 不增加版本常量和 migration stage。

缺点：

- 同一个 Version 4 将代表两种能力不同的 schema；
- 旧版本程序会忽略新的 SwitchDevices JSON 字段，但随后 PoleAttachment 引用的 AttachedDevice 不存在，工程仍会恢复失败；
- 无法从 manifest 判断文件是否包含旧程序不支持的柱上开关；
- 测试和故障报告难以区分“早期 V4”与“扩展 V4”；
- 弱化 FormatVersion 作为完整持久化合同版本的意义。

V4 扩展是技术上可行的向后读取方案，但不是可靠的双向兼容方案。

### 5.2 Option B: FormatVersion 5

做法：新增 Version5，V5 Domain DTO 明确包含 top-level SwitchDevices collection；V4 → V5 migration 为旧工程补充空 collection 或依赖 DTO default 后显式更新版本。

优点：

- manifest 清楚表明文件可能包含柱上 SwitchDevice；
- 保持“一个 FormatVersion 对应一个明确 schema contract”；
- 旧程序可明确拒绝 Version5，而不是先忽略字段再产生悬空 Attachment；
- migration、round-trip 和兼容测试边界清晰；
- 为 SwitchState persistence 提供可审计的进入点。

缺点：

- 需要增加 Version5 常量和 V4 → V5 migration；
- 必须补充 V1/V2/V3/V4 → V5 migration chain 测试；
- 修改文件和验证范围大于 V4 扩展。

由于 V4 当前无法保存 top-level SwitchDevice，旧 V4 文件不存在需要转换的柱上开关数据。V4 → V5 migration 可以只建立空 top-level SwitchDevices collection并更新版本，不应生成任何设备或 ID。

### 5.3 Strategy Recommendation

推荐在实施前冻结 Option B（FormatVersion 5）。原因不是字段数量，而是新文件将获得旧 V4 程序无法正确理解的引用结构。显式版本升级能避免“同版本、不同合同”的隐性不兼容。

本分析不执行版本升级。若项目明确接受旧程序无法读取新 V4 文件、且 FormatVersion 只承诺新程序读取旧文件，则 Option A 仍可实施；该兼容政策必须先被明确记录。

## 6. Validation and Strictness

无论选择 V4 扩展还是 V5，恢复必须验证：

- top-level switch 的 `InstallationType` 必须为 Pole；
- `SwitchKind`、`SwitchState` 是已定义值；
- DeviceId、两个 Terminal ID 均非空且互不冲突；
- SwitchDevice 声明的 Terminal ID 与 Terminal DTO 完全对应；
- Terminal owner 是该 SwitchDevice；
- PoleAttachment 的 PoleId 指向 Pole；
- PoleAttachment 的 AttachedDeviceId 指向已恢复的柱上 SwitchDevice；
- 同一 SwitchDevice 不能附着到多个 Pole；
- Connection 只能引用已恢复 Terminal；
- 所有 ID 在文档范围内唯一。

不能因为补充 top-level SwitchDevice 而放宽现有 RingCabinet、CableTermination、Terminal、Connection 或 Stable ID 校验。

## 7. Save and Round-Trip Requirements

实现阶段至少需要证明：

1. Pole + SwitchDevice + PoleAttachment 可以保存；
2. 保存内容包含 SwitchKind、InstallationType、SwitchState、Terminal IDs 和 metadata；
3. Open 状态 round trip 保持；
4. Closed 状态 round trip 保持；
5. Breaker、LoadSwitch、Disconnector、Fuse 的 SwitchKind 均保持；
6. SwitchDevice、Terminal、PoleAttachment 和 Connection Stable IDs 全部保持；
7. 一个 Pole 同时拥有 SwitchDevice attachment 和 CableTermination attachment 时均可恢复；
8. RingCabinet 内部 SwitchDevice 只保存一次；
9. 缺失或不一致的 SwitchDevice/Terminal/Attachment 引用严格失败；
10. Undo/Redo 后保存仍使用首次创建的同一 IDs。

## 8. Relationship to Switch State Operation

P0-7-C-3-B-1 的运行时状态修改可以在内存中实现，但当前不能形成工程级闭环：

- 包含柱上 SwitchDevice 的工程本身无法保存；
- Open/Closed 修改无法跨 Save/Reload 保留；
- 用户可能在完成工作票绘制后才遇到保存失败；
- Command Undo/Redo 正确不等于 Persistence 完整。

因此应先完成并验证 top-level SwitchDevice persistence，再实现通用 `ChangeSwitchStateCommand`。完成后，状态 Command 只修改现有 SwitchDevice；Persistence 负责保存最终状态，两者不互相生成对象。

RingCabinet 内部状态操作仍必须委托 `SwitchAssembly.ChangeSwitchState`，不能因新增顶层 DTO 而绕过联锁。

## 9. Proposed Implementation Slices

建议后续拆分为：

### P0-7-C-3-B-0-A Persistence Contract Decision

- 冻结 V4 extension 或 Version5；
- 冻结 top-level `ProjectSwitchDeviceDto` collection；
- 冻结兼容政策和 migration chain。

### P0-7-C-3-B-0-B Persistence Implementation

- 增加 top-level SwitchDevice save/restore；
- 保持 RingCabinet nested switch contract；
- 按正确依赖顺序恢复；
- 增加格式迁移（若采用 V5）。

### P0-7-C-3-B-0-C Integration Verification

- 全格式 migration；
- Pole switch Open/Closed round trip；
- Stable ID；
- mixed attachments；
- RingCabinet persistence regression。

通过后再恢复 P0-7-C-3-B-1 Switch State Domain Operation。

## 10. Risks

- 同一个 SwitchDevice 同时出现在 RingCabinet nested collection 和 top-level collection，造成重复 ID；
- restore 顺序错误，使 Terminal owner 或 PoleAttachment target 尚未注册；
- 只保存 `SwitchState` 而遗漏 SwitchKind、InstallationType 或 Terminal IDs；
- 使用通用 `ProjectDeviceDto.SwitchState` 形成不完整 SwitchDevice；
- migration 或 restore 生成新 ID，破坏 Attachment/Connection 引用；
- 为了兼容而跳过悬空 Attachment 或 Terminal，掩盖损坏工程；
- V4 扩展导致旧程序忽略 SwitchDevice 字段后产生难以解释的恢复错误；
- 新入口绕过 RingCabinet SwitchAssembly 联锁。

## 11. Non-Goals

本阶段不设计或实现：

- 拓扑状态传播；
- 带电判断；
- 开关操作联锁扩展；
- 保护动作和保护定值；
- 潮流或短路计算；
- SCADA；
- GIS；
- Rendering 或 Desktop UI；
- 新设备类型；
- Cable 或 OverheadLine 创建功能。

## 12. Decision Summary

1. RingCabinet 内部 SwitchDevice 的 V4 save/restore 已完整存在。
2. PoleAttachment 下的 top-level SwitchDevice 是当前唯一明确的 Switch persistence 缺口。
3. 现有 `ProjectSwitchDeviceDto` 已具备全部必要字段，推荐在 Domain DTO 顶层复用该类型，而不是扩充通用 ProjectDeviceDto。
4. 所有权图与恢复顺序必须区分；可执行顺序是 Device → Terminal → Attachment → Connection。
5. Restore 复用 DTO Stable IDs，不生成任何 ID，也不推断设备结构。
6. V4 compatible extension 技术上可行，但会形成同版本不同 schema；Version5 是推荐策略，最终需在实施前冻结。
7. top-level SwitchDevice persistence 和 round-trip 验证完成前，P0-7-C-3-B-1 不具备工程级完成条件。
