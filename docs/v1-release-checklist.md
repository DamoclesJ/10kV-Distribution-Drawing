# V1.0 Release Checklist

## Final baseline

- Version: V1.0
- Project format: `FormatVersion = V6`
- Standard package: `win-x64-portable`
- Legacy package: `win-x64-portable-win10-legacy`
- Distribution: Windows x64 self-contained portable folder
- No installer, no .NET prerequisite, no Internet, and no administrator privilege required

The legacy profile sets `CetCompat=false` for selected older or insufficiently serviced Windows 10 environments. It is a compatibility option, not a universal requirement.

The legacy package has been launch-validated on Windows 10 Enterprise 22H2, OS Build 19045.2364. The standard package and both package variants still require final target-machine validation before release sign-off.

## Publish commands

标准 portable：

```powershell
dotnet restore src/DistributionDrawing.Desktop/DistributionDrawing.Desktop.csproj -r win-x64
dotnet publish src/DistributionDrawing.Desktop/DistributionDrawing.Desktop.csproj -c Release -p:PublishProfile=win-x64-portable
```

输出目录：`artifacts/publish/desktop/win-x64-portable/`。

Windows 10 legacy portable：

```powershell
dotnet restore src/DistributionDrawing.Desktop/DistributionDrawing.Desktop.csproj -r win-x64
dotnet publish src/DistributionDrawing.Desktop/DistributionDrawing.Desktop.csproj -c Release -p:PublishProfile=win-x64-portable-win10-legacy
```

Legacy 输出目录：`artifacts/publish/desktop/win-x64-portable-win10-legacy/`。

## Windows 功能回归

- [ ] 双击启动 `DistributionDrawing.Desktop.exe`
- [ ] 新建图纸
- [ ] 创建环网柜
- [ ] 创建杆塔
- [ ] 绘制电缆和架空线
- [ ] 修改间隔并 Apply
- [ ] 验证 PT Left / Right
- [ ] 验证 PT migration
- [ ] 验证自定义间隔数量
- [ ] Copy 后 Ctrl+V 粘贴到鼠标位置
- [ ] 右键 Canvas“粘贴到此处”
- [ ] 框选组合对象并进行基础移动
- [ ] 保存工程
- [ ] 关闭工程
- [ ] 重新打开并核对内容
- [ ] 基础 Undo / Redo 回归
- [ ] 导出 PNG
- [ ] 验证多文档和 Tab 生命周期

## Portable clean-machine 离线验证

- [ ] 使用 Windows x64 clean machine
- [ ] 机器未安装 .NET 10 Desktop Runtime
- [ ] 断开网络
- [ ] 解压 portable folder 或 ZIP
- [ ] 双击 `DistributionDrawing.Desktop.exe`
- [ ] 不运行安装程序
- [ ] 不出现管理员权限提升
- [ ] 应用正常启动
- [ ] 完成新建、编辑、保存、重开和 PNG 导出
- [ ] 必要时重启机器，再次确认可直接启动
