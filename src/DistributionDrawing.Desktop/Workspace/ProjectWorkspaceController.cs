using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Rendering;

namespace DistributionDrawing.Desktop.Workspace;

public sealed class ProjectWorkspaceController
{
    private readonly IProjectWorkspaceDialogs _dialogs;
    private readonly DrawingSceneBuilder _sceneBuilder;
    private readonly Func<bool> _prepareTransientEdits;

    public ProjectWorkspaceController(
        IProjectWorkspaceDialogs dialogs,
        DrawingSceneBuilder sceneBuilder,
        Func<bool>? prepareTransientEdits = null)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _sceneBuilder = sceneBuilder ?? throw new ArgumentNullException(nameof(sceneBuilder));
        _prepareTransientEdits = prepareTransientEdits ?? (() => true);
    }

    public ProjectService? CurrentService { get; private set; }

    public ProjectRuntimeSession? CurrentSession { get; private set; }

    public bool IsDirty => CurrentSession?.IsDirty == true;

    public event EventHandler? SessionChanged;

    public bool NewProject()
    {
        if (!PrepareReplacement("新建工程")) return false;
        NewProjectRequest? request = _dialogs.RequestNewProject();
        if (request is null) return false;
        try
        {
            var service = new ProjectService();
            ProjectSession persisted = service.CreateProject(
                request.FilePath,
                request.Title,
                request.Description);
            Replace(service, ProjectRuntimeSession.CreateEmpty(persisted, _sceneBuilder));
            return true;
        }
        catch (Exception exception)
        {
            _dialogs.ShowError("新建工程失败", exception.Message);
            return false;
        }
    }

    public bool OpenProject()
    {
        if (!PrepareReplacement("打开工程")) return false;
        string? path = _dialogs.ChooseOpenProject();
        if (path is null) return false;
        try
        {
            var service = new ProjectService();
            ProjectSession persisted = service.LoadProject(path);
            Replace(service, ProjectRuntimeSession.Create(persisted, _sceneBuilder));
            return true;
        }
        catch (Exception exception)
        {
            _dialogs.ShowError("打开工程失败", exception.Message);
            return false;
        }
    }

    public bool SaveProject()
    {
        if (CurrentService is null || CurrentSession is null) return false;
        if (!_prepareTransientEdits()) return false;
        try
        {
            ProjectLayoutSnapshot layout = ProjectLayoutRuntimeMapper.ToSnapshot(
                CurrentSession.PersistenceSession.Domain,
                CurrentSession.Layout);
            ProjectSession saved = CurrentService.SaveProject(layout);
            CurrentSession.AcceptSavedSession(saved);
            SessionChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception exception)
        {
            _dialogs.ShowError("保存工程失败", exception.Message);
            return false;
        }
    }

    public bool SaveProjectAs()
    {
        if (CurrentService is null || CurrentSession is null) return false;
        if (!_prepareTransientEdits()) return false;
        string? path = _dialogs.ChooseSaveAs(CurrentService.Current?.FilePath);
        if (path is null) return false;
        try
        {
            ProjectLayoutSnapshot layout = ProjectLayoutRuntimeMapper.ToSnapshot(
                CurrentSession.PersistenceSession.Domain,
                CurrentSession.Layout);
            ProjectSession saved = CurrentService.SaveProjectAs(path, layout);
            CurrentSession.AcceptSavedSession(saved);
            SessionChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception exception)
        {
            _dialogs.ShowError("工程另存为失败", exception.Message);
            return false;
        }
    }

    public bool CloseCurrentProject()
    {
        if (!PrepareReplacement("关闭工程")) return false;
        Replace(null, null);
        return true;
    }

    public bool CanCloseApplication() => PrepareReplacement("退出程序");

    private bool PrepareReplacement(string operation)
    {
        if (!_prepareTransientEdits()) return false;
        if (!IsDirty) return true;
        return _dialogs.ConfirmDirty(operation) switch
        {
            DirtyDecision.Save => SaveProject(),
            DirtyDecision.Discard => true,
            _ => false
        };
    }

    private void Replace(ProjectService? service, ProjectRuntimeSession? session)
    {
        CurrentService = service;
        CurrentSession = session;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
