using DistributionDrawing.Infrastructure.Persistence;
using DistributionDrawing.Rendering.Wpf.Rendering;
using System.IO;

namespace DistributionDrawing.Desktop.Workspace;

public sealed class ProjectWorkspaceController
{
    private readonly IProjectWorkspaceDialogs _dialogs;
    private readonly DrawingSceneBuilder _sceneBuilder;
    private readonly Func<bool> _prepareTransientEdits;
    private int _untitledSequence;

    public ProjectWorkspaceController(
        IProjectWorkspaceDialogs dialogs,
        DrawingSceneBuilder sceneBuilder,
        Func<bool>? prepareTransientEdits = null)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _sceneBuilder = sceneBuilder ?? throw new ArgumentNullException(nameof(sceneBuilder));
        _prepareTransientEdits = prepareTransientEdits ?? (() => true);
        Workspace = new DesktopWorkspace();
        Workspace.ActiveSessionChanged += OnActiveSessionChanged;
    }

    public DesktopWorkspace Workspace { get; }

    public DocumentSession? ActiveDocumentSession => Workspace.ActiveSession;

    public ProjectService? CurrentService => ActiveDocumentSession?.ProjectService;

    public ProjectRuntimeSession? CurrentSession => ActiveDocumentSession?.RuntimeSession;

    public bool IsDirty => ActiveDocumentSession?.IsDirty == true;

    public event EventHandler? SessionChanged;

    public bool NewProject()
    {
        NewProjectRequest? request = _dialogs.RequestNewProject();
        if (request is null) return false;
        try
        {
            bool isUntitled = string.IsNullOrWhiteSpace(request.FilePath);
            string untitledName = isUntitled
                ? $"未命名 {++_untitledSequence}"
                : request.Title;
            string internalPath = isUntitled
                ? Path.Combine(
                    Path.GetTempPath(),
                    $"distribution-drawing-{Guid.NewGuid():N}.kvdrawing")
                : request.FilePath;
            var service = new ProjectService();
            ProjectSession persisted = service.CreateProject(
                internalPath,
                string.IsNullOrWhiteSpace(request.Title) ? untitledName : request.Title,
                request.Description);
            Workspace.AddSession(new DocumentSession(
                service,
                ProjectRuntimeSession.CreateEmpty(persisted, _sceneBuilder),
                isUntitled: isUntitled,
                untitledName: untitledName));
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
        string? path = _dialogs.ChooseOpenProject();
        if (path is null) return false;
        if (Workspace.FindByCanonicalPath(path) is { } existing)
        {
            Workspace.ActivateSession(existing);
            return true;
        }

        try
        {
            var service = new ProjectService();
            ProjectSession persisted = service.LoadProject(path);
            Workspace.AddSession(new DocumentSession(
                service,
                ProjectRuntimeSession.Create(persisted, _sceneBuilder)));
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
        if (ActiveDocumentSession is not { } documentSession) return false;
        if (documentSession.IsUntitled) return SaveProjectAs();
        if (!_prepareTransientEdits()) return false;
        try
        {
            ProjectLayoutSnapshot layout = ProjectLayoutRuntimeMapper.ToSnapshot(
                documentSession.RuntimeSession.PersistenceSession.Domain,
                documentSession.RuntimeSession.Layout);
            ProjectSession saved = documentSession.ProjectService.SaveProject(layout);
            documentSession.RuntimeSession.AcceptSavedSession(saved);
            documentSession.MarkPersisted();
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
        if (ActiveDocumentSession is not { } documentSession) return false;
        if (!_prepareTransientEdits()) return false;
        string? path = _dialogs.ChooseSaveAs(
            documentSession.IsUntitled
                ? documentSession.DocumentName
                : documentSession.FilePath);
        if (path is null) return false;
        DocumentSession? conflict = Workspace.FindByCanonicalPath(path);
        if (conflict is not null && !ReferenceEquals(conflict, documentSession))
        {
            _dialogs.ShowError("工程另存为失败", "该文件已在另一个工程标签页中打开。");
            return false;
        }

        try
        {
            ProjectLayoutSnapshot layout = ProjectLayoutRuntimeMapper.ToSnapshot(
                documentSession.RuntimeSession.PersistenceSession.Domain,
                documentSession.RuntimeSession.Layout);
            ProjectSession saved = documentSession.ProjectService.SaveProjectAs(path, layout);
            documentSession.RuntimeSession.AcceptSavedSession(saved);
            documentSession.MarkPersisted();
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
        if (ActiveDocumentSession is not { } session) return false;
        if (!PrepareSessionForClose(session, "关闭工程")) return false;
        Workspace.RemoveSession(session);
        return true;
    }

    public bool CanCloseApplication()
    {
        DocumentSession? original = ActiveDocumentSession;
        foreach (DocumentSession session in Workspace.Sessions.ToArray())
        {
            Workspace.ActivateSession(session);
            if (!PrepareSessionForClose(session, "退出程序"))
            {
                if (original is not null && Workspace.Contains(original))
                {
                    Workspace.ActivateSession(original);
                }

                return false;
            }
        }

        return true;
    }

    private bool PrepareSessionForClose(DocumentSession session, string operation)
    {
        if (!ReferenceEquals(ActiveDocumentSession, session))
        {
            Workspace.ActivateSession(session);
        }

        if (!_prepareTransientEdits()) return false;
        if (!session.IsDirty) return true;
        return _dialogs.ConfirmDirtyDocument(session.DocumentName, operation) switch
        {
            DirtyDecision.Save => SaveProject(),
            DirtyDecision.Discard => true,
            _ => false
        };
    }

    private void OnActiveSessionChanged(
        object? sender,
        ActiveDocumentSessionChangedEventArgs e)
    {
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
