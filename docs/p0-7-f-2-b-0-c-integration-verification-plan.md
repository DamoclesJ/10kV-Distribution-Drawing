# P0-7-F-2-B-0-C Integration Verification Report

## 1. Verification Status

P0-7-F-2-B-0-C Integration Verification 已完成。

验证基线：

```text
2be8d2c refactor: remove bay function and migrate format v4
```

结果摘要：

- Solution build：成功，0 errors，27 个既有 nullable warnings；
- Domain.Tests：64/64 passed；
- Application.Tests：37/37 passed；
- Infrastructure.Tests：34/34 passed；
- Rendering.Wpf.Tests：27/27 passed；
- Desktop.Tests：10/10 passed；
- Windows Desktop Runtime、WPF testhost、XAML、Scene、HitTest、Selection、Inspector、Undo/Redo 已完成验证；
- Domain → Application → Rendering → Desktop → Persistence 全链路验证完成。

因此，B-0-B 集成验证通过，满足进入 P0-7-F-2-B Approved Built-in Templates 的前置条件。

## 2. Purpose

本阶段验证已提交的 B-0-B：

```text
BayFunction removal
+
Project FormatVersion 4 migration
```

目标是确认删除 Function 后，Domain、Application Template Runtime、Rendering、Desktop 和 Persistence 仍形成完整闭环。

本阶段只验证，不重新设计业务模型，不实现 PT，不实现 Built-in Templates，也不修改生产代码。本文档记录计划项目的实际执行结果。

## 3. B-0-B 变更范围回顾

B-0-B 已完成以下变更：

- 删除 Domain `BayFunction` 类型及 RingCabinet interval API 中的 Function 属性、参数和校验；
- 删除 `BayTemplate.Function` 及 Template Builder 的 Function 映射；
- 移除仅由旧 `BayFunction.PT` 产生的 `PTBay` capability 路径；
- 保留 Sequence、BayIndex、IntervalKind、EquipmentConfiguration、GroundingStructureKind、Switch、Terminal、ElectricalNode、SwitchAssembly 和 Stable ID；
- 移除 Rendering/Desktop 手工创建链路中的 Function 透传和 UI 输入；
- 将 Project Persistence 当前版本升级到 V4；
- 新增 V3 → V4 migration，删除历史 interval `function` JSON property；
- 保留 V1 → V2 → V3 → V4 顺序迁移链；
- V4 DTO 不再保存或恢复 Function；
- 修正 `ValidateRestoredAggregate` 中 AllowedConnectionTypes 集合一致性判断。

当前 B-0-B 提交为：

```text
2be8d2c refactor: remove bay function and migrate format v4
```

## 4. Integration Verification Scope

验证范围包括：

1. Domain creation and restore；
2. Application Template construction and Domain Builder；
3. Rendering RuntimeLayout Builder and Full Build Coordinator；
4. Desktop manual creation and Template Creation Controller；
5. Project V4 save, load and migration；
6. Stable ID preservation；
7. Windows-only build and test execution。

不属于本阶段：

- PT Domain implementation；
- DTU capability expansion；
- BayIndex editing command；
- Built-in Template content；
- Template Selection UI；
- Project Persistence redesign；
- Command architecture redesign。

## 5. Domain → Application → Rendering → Desktop 链路检查

### 4.1 Domain

验证：

- `RingCabinetInterval` 和 Definition/RestoreDefinition 不再暴露 Function；
- LoadSwitch 和 IntegratedFeeder Create/Restore API 参数顺序正确；
- `BayIndex = 10, 3, 8` 保持输入值；
- Collection order 仍产生 `Sequence = 1, 2, 3`；
- Duplicate/non-positive BayIndex 仍被拒绝；
- IntervalKind、switch states、grounding structure、terminal/node topology 不变；
- Create 和 Restore 的 Cabinet、Interval、Switch、Terminal、ElectricalNode、SwitchAssembly IDs 保持合同。

### 4.2 Application Template Runtime

验证：

- `BayTemplate` 只包含 Index 和 EquipmentConfiguration；
- `RingCabinetTemplate` 不要求 Function 或 Unknown fallback；
- LoadSwitch、IntegratedFeeder 和 DTU capability 语义保持；
- 不生成 PT capability 的假来源；
- Template Bays 顺序和显式 BayIndex 原样传给 Builder。

### 4.3 Template Domain Builder

验证：

```text
Template Bays order → Domain Sequence
BayTemplate.Index → Domain BayIndex
EquipmentConfiguration → IntervalKind / structural definition
```

确认 Builder 不：

- 读取或推断 Function；
- 根据 BayIndex 排序；
- 注入 Unknown 或默认 Outgoing；
- 生成额外业务角色字段；
- 改变 Domain Stable ID 规则。

### 4.4 Rendering

验证：

- RuntimeLayout Builder 继续消费 Domain BuildResult；
- Layout.CabinetId 等于 Domain Cabinet.Id；
- Position、LayoutRule、Sequence 和 Interval identity 保持；
- Geometry、spacing、bounds、symbol、scene 和 hit-test 不变；
- DTU unsupported guard 仍有效；
- 不存在 Function passthrough 或 PT fake handling。

### 4.5 Desktop

验证手工创建链：

```text
Creation ViewModel
→ RingCabinetCreationConfiguration
→ RingCabinetCreationFactory
→ RingCabinet Domain Create
→ AddRingCabinetCommand
```

验证模板创建链：

```text
RingCabinetTemplate
→ Template Build Coordinator
→ Full BuildResult
→ AddRingCabinetCommand
→ CommandStack
→ Selection / Scene / Undo / Redo
```

确认 Desktop 不再要求 Function，也不引入 Direction、Role、Purpose 或其他替代字段。

## 6. Persistence V4 Migration Verification

### 5.1 Version contract

确认：

```text
Version1 = 1
Version2 = 2
Version3 = 3
Version4 = 4
CurrentVersion = Version4
```

`IsSupportedVersion` 必须接受 1–4。

### 5.2 Migration chain

必须验证：

```text
V1 → V2 → V3 → V4
V2 → V3 → V4
V3 → V4
V4 → no migration
```

V2 → V3 的历史行为仍为：

```json
{
  "bayIndex": "sequence",
  "function": "unknown"
}
```

V3 → V4 只删除 `function`，不解析其值，不修改其他结构字段。

### 5.3 Legacy Function compatibility

V3 测试应覆盖并接受以下 legacy values：

- `unknown`；
- `incoming`、`outgoing`、`tie`；
- `pt`、`metering`、`reserve`；
- arbitrary string；
- number；
- null；
- missing property。

这些值不能进入 V4 DTO 或当前 Domain。

### 5.4 V4 strictness

V4 不要求 Function，但仍必须拒绝真实结构错误：

- BayIndex <= 0；
- duplicate BayIndex；
- invalid IntervalKind；
- malformed switch/node/terminal/assembly data；
- inconsistent topology；
- invalid Stable ID references。

V4 额外携带 legacy `function` 时，应由现有 unmapped-member policy 忽略；重新保存不得写回该字段。

## 7. Stable ID Verification

验证以下 ID 在 V4 Save → Reload 中保持：

- CabinetId；
- MainBusNodeId；
- IntervalId；
- SwitchId；
- TerminalId；
- ElectricalNodeId；
- SwitchAssemblyId。

验证 V1/V2/V3 migration 不生成新 ID，且 V3 → V4 只改变 JSON 结构。

验证 Template Create、Command Execute、Undo、Redo 仍复用第一次 Build 产生的对象和 Stable IDs。

BayFunction 不得参与任何 ID 生成或恢复。

## 8. Test Matrix

实际执行结果：

```bash
dotnet build src/DistributionDrawing.sln
dotnet test tests/DistributionDrawing.Domain.Tests/DistributionDrawing.Domain.Tests.csproj
dotnet test tests/DistributionDrawing.Application.Tests/DistributionDrawing.Application.Tests.csproj
dotnet test tests/DistributionDrawing.Infrastructure.Tests/DistributionDrawing.Infrastructure.Tests.csproj
dotnet test tests/DistributionDrawing.Rendering.Wpf.Tests/DistributionDrawing.Rendering.Wpf.Tests.csproj
dotnet test tests/DistributionDrawing.Desktop.Tests/DistributionDrawing.Desktop.Tests.csproj
```

全部通过：

| 项目 | 结果 |
| --- | ---: |
| Domain.Tests | 64/64 passed |
| Application.Tests | 37/37 passed |
| Infrastructure.Tests | 34/34 passed |
| Rendering.Wpf.Tests | 27/27 passed |
| Desktop.Tests | 10/10 passed |

Solution build：successful，0 errors，27 existing nullable warnings。

重点验证结果：

- Domain 结构和 Restore tests；
- Application Template/Builder/Library tests；
- Infrastructure V4 archive round-trip and migration tests；
- Rendering layout identity and capability tests；
- Desktop manual/template creation and CommandStack integration tests。

## 9. Windows-only Validation Items

以下项目必须在具备 Windows Desktop Runtime 的环境验证：

- `net10.0-windows` Desktop project build；
- `net10.0-windows` Rendering.Wpf project build；
- Desktop.Tests testhost startup；
- WPF XAML compile and binding validation；
- Scene rebuild、HitTest、Selection、Inspector 和 Undo/Redo 集成测试。

Windows Runtime 验证已完成。`net10.0-windows` Desktop/Rendering.Wpf 项目成功构建，Desktop.Tests testhost 成功启动，WPF XAML、Scene rebuild、HitTest、Selection、Inspector 和 Undo/Redo 集成测试均已通过。

## 10. Static Boundary Checks

验证生产代码搜索结果：

- `BayFunction` production references = 0；
- `TemplateCapability.PTBay` production/tests references = 0；
- current V4 DTO/save/restore 不包含 Function；
- migration 中只允许历史 V2→V3 写入和 V3→V4 删除 `function`；
- migration 不调用 `Guid.NewGuid`、`RingCabinet.Create`、Template Builder 或 Layout Builder；
- Application 不依赖 Rendering.Wpf、Desktop 或 Infrastructure；
- Domain 不依赖 Application 或 Template Library。

## 11. F-2-B 前置条件检查

进入 Approved Built-in Templates 前置条件检查结果：

1. Domain 完全移除 BayFunction：通过；
2. BayTemplate 完全移除 Function：通过；
3. Template Builder 不再生成或传递 Function：通过；
4. Project CurrentVersion 为 4：通过；
5. V1/V2/V3 migration 可读取并进入 V4：通过；
6. V4 不保存 Function：通过；
7. Stable ID 和 topology compatibility 验证通过：通过；
8. Rendering/Desktop 不再要求 Function：通过；
9. Windows-only build/test 完成：通过；
10. B-0-B 已提交并推送：通过，提交为 `2be8d2c`；
11. PT、普通柜 Function 排列、IntegratedFeeder 专业配置等业务规则已明确：属于 F-2-B 内容边界，已确认不阻断本阶段进入。

## 12. Expected Verification Outcome

通过标准是：

```text
BayFunction removal
→ V4 persistence
→ legacy migration
→ Domain restore
→ Template Builder
→ RuntimeLayout
→ Desktop creation
→ Command/Selection/Undo/Redo
```

所有链路保持结构事实、拓扑、Stable ID 和错误边界一致。

实际结果满足上述通过标准，未发现因 Windows Runtime 缺失导致的未验证项。

## 13. Exit Criteria

P0-7-F-2-B-0-C 完成条件及结果：

- 静态边界检查：通过；
- Domain、Application、Infrastructure 相关测试：通过；
- Rendering/WPF build 和相关测试：通过；
- Windows-only Desktop build/test：通过；
- V1/V2/V3 → V4 和 V4 round-trip Stable ID 验证：通过；
- 无生产 Function 残留：通过；
- 无新增 PT/Direction/Role 语义：通过；
- 本轮文档修改外无生产代码、测试或项目文件修改。

结论：P0-7-F-2-B-0-C Integration Verification 完成，可以进入 F-2-B Approved Built-in Templates。
