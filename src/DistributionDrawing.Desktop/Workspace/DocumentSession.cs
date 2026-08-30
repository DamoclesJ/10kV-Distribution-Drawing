using DistributionDrawing.Desktop.Viewport;
using DistributionDrawing.Infrastructure.Persistence;
using System.ComponentModel;
using System.IO;

namespace DistributionDrawing.Desktop.Workspace;

/// <summary>
/// Owns the persistence service and runtime projection for one open document.
/// Domain, layout, scene, command history, and selection remain owned by the
/// single <see cref="ProjectRuntimeSession"/> instance.
/// </summary>
public sealed class DocumentSession : INotifyPropertyChanged, IDisposable
{
    private bool _lastDirty;
    private bool _isUntitled;
    private readonly string? _untitledName;
    private string? _untitledBackingFilePath;
    private bool _disposed;

    public DocumentSession(
        ProjectService projectService,
        ProjectRuntimeSession runtimeSession,
        DocumentViewState? viewState = null,
        bool isUntitled = false,
        string? untitledName = null)
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
        _isUntitled = isUntitled;
        _untitledName = isUntitled
            ? string.IsNullOrWhiteSpace(untitledName) ? "未命名" : untitledName.Trim()
            : null;
        _untitledBackingFilePath = isUntitled ? runtimeSession.PersistenceSession.FilePath : null;
        _lastDirty = IsDirty;
        RuntimeSession.CommandStack.StateChanged += OnCommandStackStateChanged;
    }

    public ProjectService ProjectService { get; }

    public ProjectRuntimeSession RuntimeSession { get; }

    public string FilePath => RuntimeSession.PersistenceSession.FilePath;

    public string DisplayTitle => RuntimeSession.PersistenceSession.Domain.Title;

    public bool IsUntitled => _isUntitled;

    public string DocumentName => IsUntitled
        ? _untitledName!
        : Path.GetFileName(FilePath);

    public string TabTitle => $"{DocumentName}{(IsDirty ? " *" : string.Empty)}";

    public bool IsDirty => RuntimeSession.IsDirty;

    public DocumentViewState ViewState { get; private set; }

    public event EventHandler? StateChanged;

    public event EventHandler? DirtyChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void MarkPersisted()
    {
        if (!_isUntitled)
        {
            NotifyDisplayStateChanged();
            return;
        }

        _isUntitled = false;
        TryDeleteUntitledBackingFile();
        NotifyDisplayStateChanged();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RuntimeSession.CommandStack.StateChanged -= OnCommandStackStateChanged;
        TryDeleteUntitledBackingFile();
        StateChanged = null;
        DirtyChanged = null;
        PropertyChanged = null;
    }

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
        NotifyDisplayStateChanged();
        if (_lastDirty != isDirty)
        {
            _lastDirty = isDirty;
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void NotifyDisplayStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUntitled)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DocumentName)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TabTitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilePath)));
    }

    private void TryDeleteUntitledBackingFile()
    {
        if (_untitledBackingFilePath is null)
        {
            return;
        }

        if (!_isUntitled &&
            StringComparer.OrdinalIgnoreCase.Equals(
                Path.GetFullPath(_untitledBackingFilePath),
                Path.GetFullPath(FilePath)))
        {
            _untitledBackingFilePath = null;
            return;
        }

        try
        {
            File.Delete(_untitledBackingFilePath);
            _untitledBackingFilePath = null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Temporary-file cleanup is best effort and must not block close or save.
        }
    }
}
