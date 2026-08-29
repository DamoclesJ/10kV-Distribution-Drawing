using DistributionDrawing.Rendering.Wpf.Scene;

namespace DistributionDrawing.Desktop.Clipboard;

public sealed record ClipboardActionResult(
    bool IsSuccess,
    string Message,
    bool HasWarning = false)
{
    public static ClipboardActionResult Success(
        string message,
        bool hasWarning = false) => new(true, message, hasWarning);

    public static ClipboardActionResult Failure(string message) => new(false, message);
}

public sealed class DrawingClipboardService
{
    private const double PasteOffsetMillimeters = 10;
    private readonly SelectionCopyPlanner _copyPlanner;
    private readonly ClipboardFragmentMaterializer _materializer;
    private ClipboardDrawingFragment? _fragment;
    private int _successfulPasteCount;

    public DrawingClipboardService()
        : this(new SelectionCopyPlanner(), new ClipboardFragmentMaterializer())
    {
    }

    internal DrawingClipboardService(
        SelectionCopyPlanner copyPlanner,
        ClipboardFragmentMaterializer materializer)
    {
        _copyPlanner = copyPlanner ?? throw new ArgumentNullException(nameof(copyPlanner));
        _materializer = materializer ?? throw new ArgumentNullException(nameof(materializer));
    }

    public bool HasContent => _fragment is not null;

    public ClipboardActionResult Copy(ProjectRuntimeSession? source)
    {
        if (source is null)
        {
            return ClipboardActionResult.Failure("当前没有打开的工程。");
        }

        CopyPlanResult plan = _copyPlanner.Create(source);
        if (!plan.IsSuccess || plan.Fragment is null)
        {
            return ClipboardActionResult.Failure(
                plan.Warnings.FirstOrDefault() ?? "当前选择中没有可复制的完整业务对象。");
        }

        _fragment = plan.Fragment;
        _successfulPasteCount = 0;
        string message = plan.Warnings.Count == 0
            ? "已复制所选对象。"
            : $"已复制可支持的对象；{string.Join(" ", plan.Warnings)}";
        return ClipboardActionResult.Success(message, plan.Warnings.Count > 0);
    }

    public ClipboardActionResult Paste(ProjectRuntimeSession? target)
    {
        if (target is null)
        {
            return ClipboardActionResult.Failure("当前没有打开的工程。");
        }

        if (_fragment is null)
        {
            return ClipboardActionResult.Failure("剪贴板中没有可粘贴的绘图对象。");
        }

        double distance = PasteOffsetMillimeters * (_successfulPasteCount + 1);
        MaterializedPaste paste = _materializer.Materialize(
            _fragment,
            target,
            new DocumentPoint(distance, distance));
        target.CommandStack.ExecuteCommand(paste.Command, target.RebuildScene);
        _successfulPasteCount++;
        return ClipboardActionResult.Success("已粘贴所选对象。");
    }
}

public sealed class DrawingClipboardController
{
    private readonly Func<ProjectRuntimeSession?> _activeSession;
    private readonly DrawingClipboardService _service;

    public DrawingClipboardController(
        Func<ProjectRuntimeSession?> activeSession,
        DrawingClipboardService? service = null)
    {
        _activeSession = activeSession ?? throw new ArgumentNullException(nameof(activeSession));
        _service = service ?? new DrawingClipboardService();
    }

    public ClipboardActionResult Copy() => _service.Copy(_activeSession());

    public ClipboardActionResult Paste() => _service.Paste(_activeSession());
}
