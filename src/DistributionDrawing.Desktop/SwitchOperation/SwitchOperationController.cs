using DistributionDrawing.Application.Devices;
using DistributionDrawing.Domain.Devices;
using DistributionDrawing.Rendering.Wpf.Interaction;
using DistributionDrawing.Rendering.Wpf.PropertyInspector;

namespace DistributionDrawing.Desktop.SwitchOperation;

public sealed record SwitchOperationResult(bool IsSuccess, string? ErrorMessage = null)
{
    public static SwitchOperationResult Success { get; } = new(true);

    public static SwitchOperationResult Failure(string message) => new(false, message);
}

/// <summary>
/// Coordinates Desktop switch actions without reproducing Domain interlock rules.
/// </summary>
public sealed class SwitchOperationController
{
    private readonly Func<ProjectRuntimeSession?> _getSession;

    public SwitchOperationController(Func<ProjectRuntimeSession?> getSession)
    {
        _getSession = getSession ?? throw new ArgumentNullException(nameof(getSession));
    }

    public event EventHandler? SceneChanged;

    public SwitchOperationResult ToggleSelected()
    {
        ProjectRuntimeSession? session = _getSession();
        if (session is null || session.SelectionManager.Selected is not { } selection)
        {
            return SwitchOperationResult.Failure("请先选择一个开关设备。");
        }

        ResolvedSelection? resolved = session.SelectionResolver.Resolve(selection);
        if (resolved?.SwitchDevice is not { } switchDevice)
        {
            return SwitchOperationResult.Failure("当前选择不是可操作的开关设备。");
        }

        SwitchState targetState = switchDevice.SwitchState switch
        {
            SwitchState.Open => SwitchState.Closed,
            SwitchState.Closed => SwitchState.Open,
            _ => throw new InvalidOperationException("开关状态无效。")
        };

        return SetSelectedState(targetState);
    }

    public SwitchOperationResult SetSelectedState(SwitchState targetState)
    {
        ProjectRuntimeSession? session = _getSession();
        if (session is null || session.SelectionManager.Selected is not { } selection)
        {
            return SwitchOperationResult.Failure("请先选择一个开关设备。");
        }

        ResolvedSelection? resolved = session.SelectionResolver.Resolve(selection);
        if (resolved?.SwitchDevice is not { } switchDevice)
        {
            return SwitchOperationResult.Failure("当前选择不是可操作的开关设备。");
        }

        if (!Enum.IsDefined(targetState))
        {
            return SwitchOperationResult.Failure("目标开关状态无效。");
        }

        if (switchDevice.SwitchState == targetState)
        {
            return SwitchOperationResult.Success;
        }

        try
        {
            var applicationCommand = new ChangeSwitchStateCommand(
                session.PersistenceSession.Domain,
                switchDevice.Id,
                targetState);
            session.CommandStack.ExecuteCommand(
                new ChangeSwitchStateCommandAdapter(applicationCommand));
            session.RebuildScene();
            SceneChanged?.Invoke(this, EventArgs.Empty);
            return SwitchOperationResult.Success;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return SwitchOperationResult.Failure(ToUserMessage(exception));
        }
    }

    private static string ToUserMessage(Exception exception)
    {
        string message = exception.Message;
        if (message.Contains("LS-GS-MUTUAL-EXCLUSION", StringComparison.Ordinal))
        {
            return "负荷开关与接地刀闸不能同时合闸。";
        }

        if (message.Contains("IF-IS-GS-MUTUAL-EXCLUSION", StringComparison.Ordinal))
        {
            return "隔离开关与接地刀闸不能同时合闸。";
        }

        if (message.Contains("violates interlock rule", StringComparison.Ordinal))
        {
            return "当前开关操作不符合设备闭锁条件。";
        }

        return "开关状态操作失败，请检查当前工程状态。";
    }

    private sealed class ChangeSwitchStateCommandAdapter : ICommand
    {
        private readonly ChangeSwitchStateCommand _command;

        public ChangeSwitchStateCommandAdapter(ChangeSwitchStateCommand command)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
        }

        public void Execute() => _command.Execute();

        public void Undo() => _command.Undo();

        public void Redo() => _command.Redo();
    }
}
