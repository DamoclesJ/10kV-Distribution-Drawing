namespace DistributionDrawing.Desktop.ConnectionEditing;

public enum OverheadLineToolState
{
    Idle,
    PickingStartTerminal,
    PickingEndTerminal
}

public enum OverheadLineToolOutcome
{
    None,
    StartPicked,
    Committed,
    Cancelled,
    InvalidTarget
}
