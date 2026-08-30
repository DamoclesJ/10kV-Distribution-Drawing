namespace DistributionDrawing.Desktop.Workspace;

public enum DirtyDecision
{
    Save,
    Discard,
    Cancel
}

public sealed record NewProjectRequest(string FilePath, string Title, string? Description);

public interface IProjectWorkspaceDialogs
{
    NewProjectRequest? RequestNewProject();

    string? ChooseOpenProject();

    string? ChooseSaveAs(string? currentFilePath);

    DirtyDecision ConfirmDirty(string operation);

    DirtyDecision ConfirmDirtyDocument(string documentName, string operation) =>
        ConfirmDirty(operation);

    void ShowError(string title, string message);
}
