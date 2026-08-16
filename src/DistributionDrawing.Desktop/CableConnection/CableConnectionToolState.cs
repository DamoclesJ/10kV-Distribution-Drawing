namespace DistributionDrawing.Desktop.CableConnection;

public enum CableConnectionToolState
{
    Idle,
    PickingStartTerminal,
    PickingEndTerminal,
    AwaitingParameters
}

public enum CableConnectionToolOutcome
{
    None,
    StartPicked,
    EndPicked,
    Committed,
    Cancelled,
    InvalidTarget
}
