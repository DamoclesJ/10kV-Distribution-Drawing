# M5-C-1-A Professional 基础对象实现设计

> 文档状态：实现前设计，仅确定第一阶段 Domain 代码边界，不实现代码<br>
> 编制日期：2026-08-12<br>
> 依据：`docs/distribution-professional-object-model-design.md`、`docs/professional-object-persistence-design.md` 与当前 Domain / Topology / ProjectSession 架构

## 1. 目标与范围

本设计把 M5-A、M5-B 已确认的专业语义收敛为 M5-C-1-B 可直接实施的最小 Domain 架构，范围仅包括：

- `BoundaryPoint`；
- `WorkScope`；
- `GroundingPoint`；
- `DrawingDocument` 对上述对象的集合持有、创建、修改、删除与引用校验。

第一阶段不实现 DTO、工程格式、Rendering、Layout、Desktop、Editor Command 或 WorkTicketData。Professional 对象只引用现有 Device 和 Terminal，不修改设备模型或电气拓扑。

## 2. 现有架构约束

当前代码具有以下边界：

- `DistributionDrawing.Domain` 为纯 .NET Domain，不依赖 WPF、Application 或 Infrastructure；
- `DrawingDocument` 已直接持有 Device、Terminal、ElectricalNode、Connection、PoleAttachment 和 OverheadLine；
- `DrawingDocument.EnsureObjectIdIsAvailable()` 是当前工程级 ID 冲突检查入口；
- Terminal 使用 `TopologyOwnerType + OwnerId` 表达所有者；
- RingCabinet 外部端子可能属于内部 Interval，而不是直接属于 RingCabinet Device；
- `ProjectSession.Domain` 保存恢复后的 DrawingDocument；
- ProjectSession 当前没有独立 Professional 状态，也不应在本阶段增加；
- Dirty 目前由 ProjectService / CommandStack 管理，不由 Domain 对象自行保存。

因此，第一阶段实现必须沿用 DrawingDocument 的工程一致性边界，不能另建可独立存在的 ProfessionalDocument 或把专业集合塞入 ProjectSession。

## 3. 代码落点与命名空间

### 3.1 新增目录

建议新增：

```text
src/DistributionDrawing.Domain/
└── Professional/
    ├── BoundaryPoint.cs
    ├── WorkScope.cs
    └── GroundingPoint.cs
```

统一命名空间：

```text
DistributionDrawing.Domain.Professional
```

理由：

- 三个对象不是 Device，不能放入 `Devices`；
- 它们引用 Topology，但不是 Terminal、ElectricalNode 或 Connection，不能放入 `Topology`；
- `Professional` 与 M5-B 的文件逻辑分区名称一致，但 Domain 不依赖持久化 DTO；
- 第一阶段只有三个对象，不再拆分 `WorkScopes`、`Grounding` 等更细目录。

### 3.2 修改现有文件

生产代码只需修改：

```text
src/DistributionDrawing.Domain/Documents/DrawingDocument.cs
```

SDK 风格项目会自动包含新增 `.cs` 文件，不需要修改 `DistributionDrawing.Domain.csproj`。

## 4. Professional 根集合

### 4.1 持有者

`DrawingDocument` 直接持有两个私有集合：

```text
_workScopes       : List<WorkScope>
_groundingPoints  : List<GroundingPoint>
```

并只暴露只读视图：

```text
WorkScopes      : IReadOnlyList<WorkScope>
GroundingPoints : IReadOnlyList<GroundingPoint>
```

不新增 `ProfessionalState`、`ProfessionalDocument` 或 `ProfessionalCollectionRoot`。这些包装对象在第一阶段没有独立生命周期或行为，只会造成第二份工程根。

### 4.2 与 DrawingDocument 的关系

```text
DrawingDocument（工程聚合根）
├── Device / Topology
├── WorkScope（专业子聚合）
│   ├── StartBoundary
│   └── EndBoundary
└── GroundingPoint（专业实体）
```

DrawingDocument 负责：

- 专业对象 ID 的工程级唯一性；
- DeviceId、TerminalId 和 GroundingPointId 的跨集合解析；
- 创建、修改、删除前的引用校验；
- 防止删除仍被 WorkScope 引用的 GroundingPoint；
- 保证失败修改不留下半更新状态。

WorkScope 和 GroundingPoint 不直接查找 DrawingDocument，也不持有 Document 引用。

### 4.3 与 ProjectSession 的关系

第一阶段关系保持：

```text
ProjectSession
└── Domain : DrawingDocument
    ├── WorkScopes
    └── GroundingPoints
```

ProjectSession 不增加 `Professional` 属性，原因是：

- Professional 已属于 DrawingDocument 的工程事实；
- 单独保存会产生两个可不同步的事实源；
- M5-B 的 `professional` 是未来 DTO 物理分区，不是运行时第二聚合根；
- 当前 ProjectSession、ProjectService 和工程格式不在 M5-C-1-B 修改范围内。

## 5. BoundaryPoint 设计

### 5.1 类型定位

`BoundaryPoint` 是不可变值对象，属于 WorkScope 内部，不独立加入 DrawingDocument 集合，也不分配独立 ID。

最小属性：

```text
DeviceId   : Guid
TerminalId : Guid
Side       : string
```

### 5.2 构造与本地校验

建议使用只读 sealed record 或等价不可变类型，构造时只执行不依赖文档的本地校验：

- DeviceId 不能为 `Guid.Empty`；
- TerminalId 不能为 `Guid.Empty`；
- Side 不能为空或仅包含空白；
- Side 保存去除首尾空白后的值。

当前 Terminal.Role 使用字符串，专业侧别的正式枚举和映射表尚未确认。因此 M5-C-1-B 不新增未经确认的 `BoundarySide` 枚举，只保存规范化 Side 字符串。未来冻结稳定编码后再通过独立里程碑收敛。

### 5.3 文档级校验

BoundaryPoint 自身不能判断引用是否属于当前工程。DrawingDocument 在创建或修改 WorkScope 前校验：

1. DeviceId 能解析到当前 DrawingDocument 的 Device；
2. TerminalId 能解析到当前 DrawingDocument 的 Terminal；
3. Terminal 所有者与 DeviceId 一致；
4. 对 RingCabinet 外部端子，允许 Terminal.OwnerType 为 InternalAggregate，且 OwnerId 对应的 Interval 属于 DeviceId 指定的 RingCabinet；
5. 其他无法证明所有权关系的组合拒绝；
6. 第一阶段只要求 Side 非空，不尝试从 Terminal.Role 自动推导或纠正 Side。

校验只解析现有关系，不修改 Terminal、Device 或 RingCabinet。

## 6. WorkScope 聚合边界

### 6.1 类型定位

WorkScope 是 DrawingDocument 内的专业子聚合，负责保存自身局部事实：

```text
WorkScope
├── WorkScopeId
├── StartBoundary : BoundaryPoint
├── EndBoundary   : BoundaryPoint
├── Description
└── GroundingPointIds : IReadOnlyList<Guid>
```

BoundaryPoint 不能脱离 WorkScope 存入文档；GroundingPoint 是独立专业实体，WorkScope 只保存稳定 ID 引用。

### 6.2 最小属性

| 属性 | 可变性 | 说明 |
| --- | --- | --- |
| WorkScopeId | 创建后不变 | 工程级稳定 ID |
| StartBoundary | 受控修改 | 起始边界值对象 |
| EndBoundary | 受控修改 | 终止边界值对象 |
| Description | 受控修改 | 去除首尾空白后的人工说明 |
| GroundingPointIds | 受控替换 | 去重、只读的稳定 ID 集合 |

WorkScope 不保存范围内设备集合、拓扑路径、电气状态或 Layout。

### 6.3 创建入口

建议由 DrawingDocument 提供文档级创建入口：

```text
CreateWorkScope(
    workScopeId,
    startBoundary,
    endBoundary,
    description,
    groundingPointIds)
```

流程：

1. 校验参数与 WorkScopeId；
2. 校验 WorkScopeId 在整个工程内未使用；
3. 校验两个 BoundaryPoint 引用；
4. 拒绝相同 TerminalId 的起止边界；
5. 校验 GroundingPointIds 无重复且全部存在；
6. 构造完整 WorkScope；
7. 最后加入 `_workScopes`。

外部不通过无参构造器创建半成品，也不先创建 WorkScope 后逐项填充。

### 6.4 修改入口

建议由 DrawingDocument 提供原子修改入口：

```text
UpdateWorkScope(
    workScopeId,
    startBoundary,
    endBoundary,
    description,
    groundingPointIds)
```

修改前完成全部新值校验，全部通过后一次性替换 WorkScope 的可变内容。任何校验失败时，原 WorkScope 保持不变。

WorkScope 的内部修改方法应为 `internal`，只允许 DrawingDocument 在完成跨集合校验后调用。外部调用方不能直接改写 BoundaryPoint 或 GroundingPointIds 集合。

### 6.5 删除入口

建议提供：

```text
RemoveWorkScope(workScopeId)
```

删除 WorkScope 只删除该专业对象：

- 不删除 BoundaryPoint 引用的 Device 或 Terminal；
- 不删除关联 GroundingPoint；
- 不修改 SwitchState、ElectricalState 或 Connection；
- WorkTicketData 尚未实现，因此本阶段不处理其反向引用。

## 7. GroundingPoint 设计

### 7.1 类型定位

GroundingPoint 是 DrawingDocument 持有的独立工程专业实体，不是 Device、SwitchDevice、ElectricalNode 或 Connection。

最小属性：

```text
GroundingPointId : Guid
TerminalId       : Guid
Location         : string
Number           : string?
Note             : string?
```

### 7.2 持久引用边界

GroundingPoint 只持久引用 TerminalId：

- 不保存 DeviceId；
- 不保存 Terminal 对象引用；
- 不保存 Terminal.Role 或设备名称副本；
- 不保存 GroundSwitch、Ground ElectricalNode 或有效接地结果；
- 设备归属通过 DrawingDocument 中的 Terminal.OwnerType / OwnerId 解析。

第一阶段 Domain 对象不保存 ProjectId 或 DocumentId。非法跨工程引用通过“目标 ID 无法在当前 DrawingDocument 中解析”拒绝，避免在每个专业对象中复制 DocumentId。

### 7.3 创建入口

建议由 DrawingDocument 提供：

```text
CreateGroundingPoint(
    groundingPointId,
    terminalId,
    location,
    number,
    note)
```

流程：

1. 校验 GroundingPointId 非空且工程级唯一；
2. 校验 TerminalId 非空并存在于当前 DrawingDocument；
3. 校验 Location 非空；
4. 规范化 Number 和 Note 的可选文本；
5. 构造完整 GroundingPoint；
6. 最后加入 `_groundingPoints`。

当前文档用途字段尚未实现，因此 M5-C-1-B 不自行规定 Number 必填或编号唯一范围。该规则等待文档用途与专业规则确认后增加；不能根据文件名猜测工作票/勘察图类型。

### 7.4 修改入口

建议提供：

```text
UpdateGroundingPoint(
    groundingPointId,
    terminalId,
    location,
    number,
    note)
```

先校验全部新值，再通过 GroundingPoint 的 `internal` 方法一次性提交。失败时保留原 TerminalId 和文本属性。

修改 GroundingPoint 的 TerminalId 不自动修改引用它的 WorkScope，因为 WorkScope 关联的是 GroundingPointId；其业务关联保持稳定。

### 7.5 删除入口

建议提供：

```text
RemoveGroundingPoint(groundingPointId)
```

若任一 WorkScope.GroundingPointIds 引用目标 ID，删除必须失败。调用方应先通过 UpdateWorkScope 显式解除引用，再删除 GroundingPoint。

删除 GroundingPoint 不修改 Terminal、Topology、设备状态或其他接地对象。

## 8. ID 唯一性与重复定义

### 8.1 工程级 ID

扩展 `DrawingDocument.EnsureObjectIdIsAvailable()`，将以下集合纳入现有工程级 ID 检查：

- `_workScopes` 的 WorkScopeId；
- `_groundingPoints` 的 GroundingPointId。

因此专业对象 ID 不能与 DeviceId、TerminalId、ElectricalNodeId、SwitchAssemblyId、ConnectionId、PoleAttachmentId 或内部 IntervalId 冲突。

### 8.2 重复 BoundaryPoint

第一阶段明确禁止同一 WorkScope 的 StartBoundary 和 EndBoundary 引用同一个 TerminalId。这是“重复 BoundaryPoint”的阻断定义。

不同 WorkScope 是否允许共享同一边界尚无禁止依据，因此不做全工程唯一限制。

### 8.3 重复 GroundingPoint

第一阶段明确禁止：

- 重复 GroundingPointId；
- WorkScope.GroundingPointIds 内出现重复 ID。

“同一 Terminal 是否允许多组工作地线”仍是 M5-A 待确认问题，因此 M5-C-1-B 不把相同 TerminalId 自动视为重复 GroundingPoint。后续专业规则确认后再增加限制或警告。

## 9. 引用校验职责

### 9.1 校验矩阵

| 校验 | 执行者 | 失败处理 |
| --- | --- | --- |
| Guid 非空、文本非空 | 值对象/实体构造或内部更新 | 参数异常，不产生对象 |
| 专业 ID 工程级唯一 | DrawingDocument | 拒绝创建 |
| Terminal 是否存在 | DrawingDocument | 拒绝创建或修改 |
| Device 是否存在 | DrawingDocument | 拒绝 WorkScope 创建或修改 |
| Device 与 Terminal 所有权一致 | DrawingDocument | 拒绝 BoundaryPoint |
| 起止边界 Terminal 不同 | DrawingDocument / WorkScope 本地不变量 | 拒绝创建或修改 |
| GroundingPointIds 存在且不重复 | DrawingDocument | 拒绝 WorkScope 创建或修改 |
| GroundingPoint 是否仍被引用 | DrawingDocument | 拒绝删除 |
| 非法跨工程引用 | DrawingDocument 当前集合解析 | 拒绝创建或修改 |

### 9.2 设备所有权解析

建议在 DrawingDocument 内增加私有校验辅助方法，不修改 Terminal 或设备模型：

```text
ValidateBoundaryPoint(BoundaryPoint boundary)
```

解析规则：

1. `Terminal.OwnerType == Device` 时，Terminal.OwnerId 必须等于 BoundaryPoint.DeviceId；
2. `Terminal.OwnerType == InternalAggregate` 时，当前仅支持 RingCabinetInterval：
   - 找到 OwnerId 对应的 RingCabinetInterval；
   - 该 Interval.ParentCabinetId 必须等于 BoundaryPoint.DeviceId；
3. 其他所有者结构在未设计前拒绝，不使用 ParentId 或名称猜测；
4. 不修改现有 TopologyOwnerType。

此规则允许环网柜间隔外部端子以 RingCabinet.Id 作为用户可见 DeviceId，同时保持 Terminal 的真实内部所有者。

### 9.3 原子修改

所有 Update 方法必须遵循：

```text
读取目标对象
  ↓
校验全部候选值和跨对象引用
  ↓
一次性应用
```

不得在校验过程中逐字段写入目标对象，也不得自动修复缺失引用。

## 10. 创建与恢复入口边界

M5-C-1-B 只实现正常 Domain 创建/修改入口，不实现持久化专用恢复 API。

未来 Professional Rehydrator 可以按 M5-B 顺序调用同一 DrawingDocument 创建入口：

```text
先 CreateGroundingPoint
再 CreateWorkScope
```

这样加载与新建共享相同不变量。若将来性能或迁移需要专用 Restore 入口，也必须保持 `internal`，并在完成候选构造后执行等价完整性校验。

## 11. Command、Undo 与 Dirty 边界

### 11.1 Domain 不负责编辑历史

Professional 对象和 DrawingDocument 不引用：

- `ICommand`；
- `CommandStack`；
- `ProjectSession`；
- `ProjectService`；
- UI 或 WPF 类型。

Domain 方法只负责执行一次有效业务变更或拒绝无效变更。

### 11.2 未来命令流程

```text
用户输入
  ↓
Application / Editor Command
  ↓ Execute
DrawingDocument.Create / Update / Remove Professional Object
  ↓
ProjectSession 标记 Dirty
  ↓
Scene 刷新（后续）
```

未来命令保存 Before/After 值并调用相同 Domain 入口：

- `CreateWorkScopeCommand`；
- `UpdateWorkScopeCommand`；
- `RemoveWorkScopeCommand`；
- `CreateGroundingPointCommand`；
- `UpdateGroundingPointCommand`；
- `RemoveGroundingPointCommand`。

Undo 也必须经过 Domain 校验。若其他对象已经引用待恢复或待删除对象，命令应报告冲突，不能绕过 DrawingDocument 私有集合。

### 11.3 Dirty 状态

- Domain 对象不保存 IsDirty；
- 成功命令由编辑会话推进 CommandStack，并使工程 Dirty；
- 被 Domain 拒绝的命令不进入历史，不改变 Dirty；
- 保存后由 ProjectService / EditorSession 建立新 SavePoint；
- M5-C-1-B 不修改 ProjectSession 或 ProjectService。

## 12. 查询边界

第一阶段只需集合只读访问和按 ID 的内部查找。不要提前增加仓储、查询总线或专业对象服务。

如测试或后续 Application 确实需要按 ID 获取，可在 DrawingDocument 提供明确方法：

```text
GetWorkScope(Guid id)
GetGroundingPoint(Guid id)
```

找不到时应明确失败；不返回来自其他 ProjectSession 的对象，也不按 Description、Location 或 Number 模糊查找。

## 13. 明确禁止

M5-C-1-B 必须保持以下禁止项：

- 不从 Rendering、SceneElement、Symbol、HitTest 或坐标创建 Professional 数据；
- 不从开关状态、OperationalState 或有效接地结果生成 WorkScope；
- 不根据 WorkScope、Topology 或停电状态自动生成 GroundingPoint；
- 不自动计算停电范围、带电传播或范围内设备集合；
- 不自动生成 SafetyMeasure；
- 不实现 WorkTicketData、SafetyMeasure 或 OperationStep；
- 不修改 Device、Terminal、ElectricalNode、Connection 或 RingCabinet 结构；
- 不为 Professional 对象加入 WPF、DTO、JSON 或文件系统依赖；
- 不修改 Persistence 格式、Layout、Rendering 或 Desktop。

## 14. M5-C-1-B 最小实现文件范围

### 14.1 允许的生产文件

新增：

```text
src/DistributionDrawing.Domain/Professional/BoundaryPoint.cs
src/DistributionDrawing.Domain/Professional/WorkScope.cs
src/DistributionDrawing.Domain/Professional/GroundingPoint.cs
```

修改：

```text
src/DistributionDrawing.Domain/Documents/DrawingDocument.cs
```

不需要修改 Domain `.csproj`。

### 14.2 测试建议

若 M5-C-1-B 同时要求测试，测试范围应只新增：

```text
tests/DistributionDrawing.Domain.Tests/ProfessionalObjectTests.cs
```

如果后续里程碑继续沿用“实现与测试分阶段”的项目节奏，则 M5-C-1-B 只实现生产文件，紧接着由 M5-C-1-C 增加上述 Domain 测试。无论采用哪种节奏，都不得为了测试修改 Persistence、Rendering 或 Desktop。

### 14.3 禁止修改文件

M5-C-1-B 不应修改：

- `src/DistributionDrawing.Domain/Devices/**`；
- `src/DistributionDrawing.Domain/Topology/**`；
- `src/DistributionDrawing.Infrastructure/**`；
- `src/DistributionDrawing.Rendering.Wpf/**`；
- `src/DistributionDrawing.Desktop/**`；
- `src/DistributionDrawing.Application/**`；
- `docs/**`；
- 工程文件格式与 Layout 类型。

## 15. M5-C-1-B 验收标准

### 15.1 构造与集合

- 能在包含有效 Device 和 Terminal 的 DrawingDocument 中创建 GroundingPoint；
- 能用两个有效且不同的 BoundaryPoint 创建 WorkScope；
- WorkScope 和 GroundingPoint 只通过 DrawingDocument 进入只读集合；
- BoundaryPoint 没有独立 ID，也不出现在文档根集合；
- GroundingPoint 只保存 TerminalId，不保存 DeviceId 或 Terminal 对象；
- ProjectSession 无需修改即可通过 `Domain` 访问专业集合。

### 15.2 校验

- 空 WorkScopeId、GroundingPointId、DeviceId 或 TerminalId 被拒绝；
- Professional ID 与任何现有工程对象 ID 冲突时被拒绝；
- 不存在的 TerminalId 被拒绝；
- BoundaryPoint.DeviceId 不存在时被拒绝；
- DeviceId 与 Terminal 所有者不一致时被拒绝；
- RingCabinet.Id 与其 Interval 外部 Terminal 的聚合关系可以通过校验；
- 同一 WorkScope 的两个边界引用相同 TerminalId 时被拒绝；
- WorkScope 内重复 GroundingPointId 被拒绝；
- WorkScope 引用不存在的 GroundingPoint 时被拒绝；
- 删除仍被 WorkScope 引用的 GroundingPoint 时被拒绝；
- 引用另一 DrawingDocument 中对象的 ID 时被拒绝。

### 15.3 修改与原子性

- WorkScope 可以通过 DrawingDocument 更新边界、说明和 GroundingPointIds；
- GroundingPoint 可以通过 DrawingDocument 更新 TerminalId、Location、Number 和 Note；
- 更新不会改变 WorkScopeId 或 GroundingPointId；
- 无效更新抛出明确异常且原对象所有属性保持原值；
- 删除 WorkScope 不删除其关联 GroundingPoint；
- 解除引用后可以删除 GroundingPoint；
- 任何操作都不修改 Device、Terminal、Connection 或开关状态。

### 15.4 架构检查

- Domain 项目不新增 WPF、Application、Infrastructure 或 JSON 依赖；
- 不新增 WorkTicketData、SafetyMeasure 或 OperationStep 类；
- 不修改 Topology 和设备模型；
- 不创建 DTO、Layout 或 Rendering 代码；
- `git diff` 只包含第 14.1 节允许的生产文件及明确授权的测试文件。

## 16. 后续阶段衔接

M5-C-1-B 完成后建议按以下顺序推进：

1. M5-C-1-C：增加 Professional Domain 单元测试；
2. M5-C-2-A：设计 Professional DTO 与现有 ProjectService 的具体接入；
3. M5-C-2-B：实现 Professional DTO、格式升级和恢复；
4. M5-D：设计并实现 WorkScope / GroundingPoint Layout 与 Rendering；
5. M5-E：实现选择、属性编辑、CommandStack 和 Dirty 集成；
6. WorkTicketData 继续保持独立里程碑，待业务字段确认后启动。

Professional Domain 完成不代表软件已经能够保存或绘制工作范围和工作地线；这些能力必须分别经过 Persistence、Layout/Rendering 和 Editor 集成验收。
