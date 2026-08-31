namespace DistributionDrawing.Desktop.Actions;

public enum DesktopContextActionKind
{
    Paste,
    PasteAtCursor,
    SelectAll,
    FitDrawing,
    ToggleGrid,
    Copy,
    Delete,
    RotateLeft,
    RotateRight,
    SwitchOperation,
    ReconnectCableStart,
    ReconnectCableEnd
}

public sealed class DesktopContextMenuResolver
{
    public IReadOnlyList<DesktopContextActionKind> Resolve(
        bool isIdle,
        bool isBlank,
        int selectionCount,
        bool canRotate,
        bool canOperateSwitch,
        bool canReconnectCable)
    {
        if (!isIdle)
        {
            return [];
        }

        if (isBlank)
        {
            return
            [
                DesktopContextActionKind.PasteAtCursor,
                DesktopContextActionKind.SelectAll,
                DesktopContextActionKind.FitDrawing,
                DesktopContextActionKind.ToggleGrid
            ];
        }

        var result = new List<DesktopContextActionKind>
        {
            DesktopContextActionKind.Copy,
            DesktopContextActionKind.Delete
        };
        if (selectionCount != 1)
        {
            return result;
        }

        if (canRotate)
        {
            result.Add(DesktopContextActionKind.RotateLeft);
            result.Add(DesktopContextActionKind.RotateRight);
        }

        if (canOperateSwitch)
        {
            result.Add(DesktopContextActionKind.SwitchOperation);
        }

        if (canReconnectCable)
        {
            result.Add(DesktopContextActionKind.ReconnectCableStart);
            result.Add(DesktopContextActionKind.ReconnectCableEnd);
        }

        return result;
    }
}
