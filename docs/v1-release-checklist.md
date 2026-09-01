# V1 Release Checklist

正式分发目标：Windows x64、self-contained portable folder，无安装程序、无管理员权限要求、无需预装 .NET、离线解压即用。

发布命令：

```powershell
dotnet restore src/DistributionDrawing.Desktop/DistributionDrawing.Desktop.csproj -r win-x64
dotnet publish src/DistributionDrawing.Desktop/DistributionDrawing.Desktop.csproj -c Release -p:PublishProfile=win-x64-portable
```

输出目录：`artifacts/publish/desktop/win-x64-portable/`。

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
