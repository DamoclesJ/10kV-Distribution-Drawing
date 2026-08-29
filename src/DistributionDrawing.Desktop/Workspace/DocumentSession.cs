using DistributionDrawing.Desktop.Viewport;
using DistributionDrawing.Infrastructure.Persistence;

namespace DistributionDrawing.Desktop.Workspace;

/// <summary>
/// Owns the persistence service and runtime projection for one open document.
/// Domain, layout, scene, command history, and selection remain owned by the
/// single <see cref="ProjectRuntimeSession"/> instance.
/// </summary>
public sealed class DocumentSession
{
    private bool _lastDirty;

    public DocumentSession(
        ProjectService projectService,
        ProjectRuntimeSession runtimeSession,
        DocumentViewState? viewState = null)
    {
        ProjectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        RuntimeSession = runtimeSession ?? throw new ArgumentNullException(nameof(runtimeSession));
        if (projectService.Current is null ||
            !ReferenceEquals(
                projectService.Current.Domain,
                runtimeSession.PersistenceSession.Domain))
        {
            throw new ArgumentException(
                "The project service and runtime session must represent the same document.",
                nameof(projectService));
        }

        ViewState = viewState ?? DocumentViewState.Default;
        _lastDirty = IsDirty;
        RuntimeSession.CommandStack.StateChanged += OnCommandStackStateChanged;
    }

    public ProjectService ProjectService { get; }

    public ProjectRuntimeSession RuntimeSession { get; }

    public string FilePath => RuntimeSession.PersistenceSession.FilePath;

    public string DisplayTitle => RuntimeSession.PersistenceSession.Domain.Title;

    public bool IsDirty => RuntimeSession.IsDirty;

    public DocumentViewState ViewState { get; private set; }

    public event EventHandler? StateChanged;

    public event EventHandler? DirtyChanged;

    public void UpdateViewState(DocumentViewState viewState)
    {
        ArgumentNullException.ThrowIfNull(viewState);
        if (ViewState == viewState)
        {
            return;
        }

        ViewState = viewState;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnCommandStackStateChanged(object? sender, EventArgs e)
    {
        bool isDirty = IsDirty;
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (_lastDirty != isDirty)
        {
            _lastDirty = isDirty;
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
