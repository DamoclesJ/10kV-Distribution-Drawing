using DistributionDrawing.Rendering.Wpf.Interaction;

namespace DistributionDrawing.Desktop.Actions;

public sealed class DesktopActionContext
{
    public required Func<ProjectRuntimeSession?> ActiveSession { get; init; }

    public required Func<bool> HasClipboardContent { get; init; }

    public required Func<bool> IsInteractionIdle { get; init; }

    public required Func<bool> CanRotateSelection { get; init; }

    public required Func<bool> CanOperateSwitch { get; init; }

    public required Func<bool> CanReconnectCable { get; init; }

    public required Func<bool> CanAddPoleAttachment { get; init; }
}

public sealed class DesktopUserActionHandlers
{
    public required Action New { get; init; }
    public required Action Open { get; init; }
    public required Action Save { get; init; }
    public required Action SaveAs { get; init; }
    public required Action CloseDocument { get; init; }
    public required Action Exit { get; init; }
    public required Action ExportPng { get; init; }
    public required Action Undo { get; init; }
    public required Action Redo { get; init; }
    public required Action Copy { get; init; }
    public required Action Paste { get; init; }
    public Action? PasteAtCursor { get; init; }
    public required Action SelectAll { get; init; }
    public required Action Delete { get; init; }
    public required Action CancelCurrentOperation { get; init; }
    public required Action Select { get; init; }
    public required Action CreatePole { get; init; }
    public required Action CreateRingCabinet { get; init; }
    public required Action CreateOverheadLine { get; init; }
    public required Action CreateCable { get; init; }
    public required Action AddCableTermination { get; init; }
    public required Action AddPoleSwitch { get; init; }
    public required Action AddGroundingPoint { get; init; }
    public required Action AddWorkScope { get; init; }
    public required Action ZoomIn { get; init; }
    public required Action ZoomOut { get; init; }
    public required Action FitDrawing { get; init; }
    public required Action ToggleGrid { get; init; }
    public required Action TypographySettings { get; init; }
    public required Action RotateLeft { get; init; }
    public required Action RotateRight { get; init; }
    public required Action SwitchOperation { get; init; }
    public required Action ReconnectCableStart { get; init; }
    public required Action ReconnectCableEnd { get; init; }
}

public sealed class DesktopUserActions
{
    private readonly IReadOnlyList<DesktopAction> _all;

    public DesktopUserActions(
        DesktopActionContext context,
        DesktopUserActionHandlers handlers,
        IDesktopMessageService messages)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(messages);

        bool HasSession() => context.ActiveSession() is not null;
        bool HasSelection() => context.ActiveSession()?.SelectionManager.SelectionCount > 0;
        bool CanStartDrawing() => HasSession();

        New = Create("新建工程失败", handlers.New, messages);
        Open = Create("打开工程失败", handlers.Open, messages);
        Save = Create("保存工程失败", handlers.Save, messages, HasSession);
        SaveAs = Create("工程另存为失败", handlers.SaveAs, messages, HasSession);
        CloseDocument = Create("关闭工程失败", handlers.CloseDocument, messages, HasSession);
        Exit = Create("退出程序失败", handlers.Exit, messages);
        ExportPng = Create("导出 PNG 失败", handlers.ExportPng, messages, HasSession);
        Undo = Create(
            "撤销失败",
            handlers.Undo,
            messages,
            () => context.ActiveSession()?.CommandStack.CanUndo == true);
        Redo = Create(
            "重做失败",
            handlers.Redo,
            messages,
            () => context.ActiveSession()?.CommandStack.CanRedo == true);
        Copy = Create(
            "复制对象失败",
            handlers.Copy,
            messages,
            () => HasSelection() && context.IsInteractionIdle());
        Paste = Create(
            "粘贴对象失败",
            handlers.Paste,
            messages,
            () => HasSession() && context.HasClipboardContent() && context.IsInteractionIdle());
        PasteAtCursor = Create(
            "粘贴对象失败",
            handlers.PasteAtCursor ?? handlers.Paste,
            messages,
            () => HasSession() && context.HasClipboardContent() && context.IsInteractionIdle());
        SelectAll = Create(
            "全选失败",
            handlers.SelectAll,
            messages,
            () => HasSession() && context.IsInteractionIdle());
        Delete = Create(
            "删除对象失败",
            handlers.Delete,
            messages,
            () => HasSelection() && context.IsInteractionIdle());
        CancelCurrentOperation = Create(
            "取消当前操作失败",
            handlers.CancelCurrentOperation,
            messages);
        Select = Create("切换选择工具失败", handlers.Select, messages, HasSession);
        CreatePole = Create("无法添加杆塔", handlers.CreatePole, messages, CanStartDrawing);
        CreateRingCabinet = Create(
            "无法添加环网柜",
            handlers.CreateRingCabinet,
            messages,
            CanStartDrawing);
        CreateOverheadLine = Create(
            "无法绘制架空线",
            handlers.CreateOverheadLine,
            messages,
            CanStartDrawing);
        CreateCable = Create("无法绘制电缆", handlers.CreateCable, messages, CanStartDrawing);
        AddCableTermination = Create(
            "无法添加电缆终端",
            handlers.AddCableTermination,
            messages,
            () => HasSession() && context.IsInteractionIdle() && context.CanAddPoleAttachment());
        AddPoleSwitch = Create(
            "无法添加柱上开关",
            handlers.AddPoleSwitch,
            messages,
            () => HasSession() && context.IsInteractionIdle() && context.CanAddPoleAttachment());
        AddGroundingPoint = Create(
            "无法添加工作地线",
            handlers.AddGroundingPoint,
            messages,
            CanStartDrawing);
        AddWorkScope = Create(
            "无法添加工作范围",
            handlers.AddWorkScope,
            messages,
            CanStartDrawing);
        ZoomIn = Create("放大失败", handlers.ZoomIn, messages, HasSession);
        ZoomOut = Create("缩小失败", handlers.ZoomOut, messages, HasSession);
        FitDrawing = Create("适合图形失败", handlers.FitDrawing, messages, HasSession);
        ToggleGrid = Create("网格切换失败", handlers.ToggleGrid, messages);
        TypographySettings = Create("图面字号设置失败", handlers.TypographySettings, messages);
        RotateLeft = Create(
            "旋转失败",
            handlers.RotateLeft,
            messages,
            () => HasSession() && context.IsInteractionIdle() && context.CanRotateSelection());
        RotateRight = Create(
            "旋转失败",
            handlers.RotateRight,
            messages,
            () => HasSession() && context.IsInteractionIdle() && context.CanRotateSelection());
        SwitchOperation = Create(
            "开关操作失败",
            handlers.SwitchOperation,
            messages,
            () => HasSession() && context.IsInteractionIdle() && context.CanOperateSwitch());
        ReconnectCableStart = Create(
            "电缆端点修改失败",
            handlers.ReconnectCableStart,
            messages,
            () => HasSession() && context.IsInteractionIdle() && context.CanReconnectCable());
        ReconnectCableEnd = Create(
            "电缆端点修改失败",
            handlers.ReconnectCableEnd,
            messages,
            () => HasSession() && context.IsInteractionIdle() && context.CanReconnectCable());

        _all =
        [
            New, Open, Save, SaveAs, CloseDocument, ExportPng, Exit,
            Undo, Redo, Copy, Paste, PasteAtCursor, SelectAll, Delete, CancelCurrentOperation,
            Select, CreatePole, CreateRingCabinet, CreateOverheadLine, CreateCable,
            AddCableTermination, AddPoleSwitch, AddGroundingPoint, AddWorkScope,
            ZoomIn, ZoomOut, FitDrawing, ToggleGrid, TypographySettings,
            RotateLeft, RotateRight, SwitchOperation,
            ReconnectCableStart, ReconnectCableEnd
        ];
    }

    public DesktopAction New { get; }
    public DesktopAction Open { get; }
    public DesktopAction Save { get; }
    public DesktopAction SaveAs { get; }
    public DesktopAction CloseDocument { get; }
    public DesktopAction Exit { get; }
    public DesktopAction ExportPng { get; }
    public DesktopAction Undo { get; }
    public DesktopAction Redo { get; }
    public DesktopAction Copy { get; }
    public DesktopAction Paste { get; }
    public DesktopAction PasteAtCursor { get; }
    public DesktopAction SelectAll { get; }
    public DesktopAction Delete { get; }
    public DesktopAction CancelCurrentOperation { get; }
    public DesktopAction Select { get; }
    public DesktopAction CreatePole { get; }
    public DesktopAction CreateRingCabinet { get; }
    public DesktopAction CreateOverheadLine { get; }
    public DesktopAction CreateCable { get; }
    public DesktopAction AddCableTermination { get; }
    public DesktopAction AddPoleSwitch { get; }
    public DesktopAction AddGroundingPoint { get; }
    public DesktopAction AddWorkScope { get; }
    public DesktopAction ZoomIn { get; }
    public DesktopAction ZoomOut { get; }
    public DesktopAction FitDrawing { get; }
    public DesktopAction ToggleGrid { get; }
    public DesktopAction TypographySettings { get; }
    public DesktopAction RotateLeft { get; }
    public DesktopAction RotateRight { get; }
    public DesktopAction SwitchOperation { get; }
    public DesktopAction ReconnectCableStart { get; }
    public DesktopAction ReconnectCableEnd { get; }

    public void RefreshCanExecute()
    {
        foreach (DesktopAction action in _all)
        {
            action.Refresh();
        }
    }

    private DesktopAction Create(
        string errorTitle,
        Action execute,
        IDesktopMessageService messages,
        Func<bool>? canExecute = null)
    {
        return new DesktopAction(
            () =>
            {
                try
                {
                    execute();
                }
                catch (Exception exception) when (
                    exception is ArgumentException or InvalidOperationException)
                {
                    messages.ShowError(errorTitle, exception.Message);
                }
                finally
                {
                    RefreshCanExecute();
                }
            },
            canExecute);
    }
}
