# 10kV Distribution Drawing

面向 10kV 配电工作票和专业图例绘制的 Windows 桌面应用。

## 当前版本

V1.0

## V1.0 能力

- 创建和编辑 RingCabinet、Pole、Cable、OverheadLine。
- 使用专业的柱上开关、接地、PT 等图元表达配电设备。
- 基于 Terminal 的电气拓扑连接，以及开关状态和现有联锁能力。
- Inspector 属性编辑和 RingCabinet 间隔配置。
- PT Left / Right 创建、PT migration、自定义间隔数量。
- 多选、框选、组合移动。
- Copy / Paste、PasteAtCursor。
- Undo / Redo。
- 多文档工程和 Tab 切换。
- 保存并重新打开 `.kvdrawing` 工程。
- 导出 PNG 图纸。

V1.0 不包含 Annotation、Energization、PDF/JPG 导出或安装程序。

## 架构概览

项目采用模块化单体结构：

- `DistributionDrawing.Domain`：电气领域模型和业务不变量。
- `DistributionDrawing.Application`：应用服务和用例协调。
- `DistributionDrawing.Rendering.Wpf`：WPF 矢量场景、专业图元和交互投影。
- `DistributionDrawing.Infrastructure`：工程文件持久化和基础设施。
- `DistributionDrawing.Desktop`：Windows 桌面 Shell、会话和用户操作入口。

电气模型与 rendering/layout 分离；画布是领域数据的可视化结果，不是事实源。

## 工程文件格式

当前 `FormatVersion = V6`，工程文件扩展名为 `.kvdrawing`。

## Windows 分发

正式目标是 Windows x64 self-contained portable folder：

- 无安装程序
- 无需预装 .NET Desktop Runtime
- 无需网络
- 无需管理员权限
- 解压后直接运行

发布 profiles：

- `win-x64-portable`：标准 Windows x64 portable profile。
- `win-x64-portable-win10-legacy`：设置 `CetCompat=false` 的兼容 profile，用于部分无法升级或 servicing 不足的旧 Windows 10 环境；不是所有 Windows 机器的默认要求。

目标环境的 legacy profile 已在 Windows 10 Enterprise 22H2、OS Build 19045.2364 上完成启动验证。

## 构建与测试

```bash
dotnet build src/DistributionDrawing.sln

dotnet test tests/DistributionDrawing.Domain.Tests/DistributionDrawing.Domain.Tests.csproj
dotnet test tests/DistributionDrawing.Application.Tests/DistributionDrawing.Application.Tests.csproj
dotnet test tests/DistributionDrawing.Infrastructure.Tests/DistributionDrawing.Infrastructure.Tests.csproj
```

Windows 下还应运行：

```powershell
dotnet test tests/DistributionDrawing.Rendering.Wpf.Tests/DistributionDrawing.Rendering.Wpf.Tests.csproj
dotnet test tests/DistributionDrawing.Desktop.Tests/DistributionDrawing.Desktop.Tests.csproj
```

## 发布

标准 portable：

```powershell
dotnet restore src/DistributionDrawing.Desktop/DistributionDrawing.Desktop.csproj -r win-x64
dotnet publish src/DistributionDrawing.Desktop/DistributionDrawing.Desktop.csproj -c Release -p:PublishProfile=win-x64-portable
```

Windows 10 legacy portable：

```powershell
dotnet restore src/DistributionDrawing.Desktop/DistributionDrawing.Desktop.csproj -r win-x64
dotnet publish src/DistributionDrawing.Desktop/DistributionDrawing.Desktop.csproj -c Release -p:PublishProfile=win-x64-portable-win10-legacy
```

## 文档

- [`docs/v1-release-checklist.md`](docs/v1-release-checklist.md)：V1.0 发布、Windows 回归和 clean-machine 验证清单。
- [`docs/requirements.md`](docs/requirements.md)：产品范围和验收边界。
- [`docs/architecture.md`](docs/architecture.md)：高层架构和模块边界。
- [`docs/equipment-model.md`](docs/equipment-model.md)：核心设备和拓扑模型。
- [`docs/ring-cabinet-design.md`](docs/ring-cabinet-design.md)：RingCabinet、间隔和 PT 设计。
- [`docs/drawing-rule.md`](docs/drawing-rule.md)：专业绘图规则和模型映射。

`reference/` 保存用于专业规则和图元核对的原始资料。
