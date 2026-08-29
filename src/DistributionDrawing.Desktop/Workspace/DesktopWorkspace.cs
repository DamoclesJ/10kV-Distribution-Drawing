using System.IO;

namespace DistributionDrawing.Desktop.Workspace;

public sealed class ActiveDocumentSessionChangedEventArgs : EventArgs
{
    public ActiveDocumentSessionChangedEventArgs(
        DocumentSession? previous,
        DocumentSession? current)
    {
        Previous = previous;
        Current = current;
    }

    public DocumentSession? Previous { get; }

    public DocumentSession? Current { get; }
}

/// <summary>
/// Process-local collection of open documents. It has no UI or dialog
/// dependencies; the shell decides whether dirty sessions may be removed.
/// </summary>
public sealed class DesktopWorkspace
{
    private readonly List<DocumentSession> _sessions = [];
    private readonly IReadOnlyList<DocumentSession> _sessionsView;

    public DesktopWorkspace()
    {
        _sessionsView = _sessions.AsReadOnly();
    }

    public IReadOnlyList<DocumentSession> Sessions => _sessionsView;

    public DocumentSession? ActiveSession { get; private set; }

    public event EventHandler? SessionsChanged;

    public event EventHandler<ActiveDocumentSessionChangedEventArgs>? ActiveSessionChanging;

    public event EventHandler<ActiveDocumentSessionChangedEventArgs>? ActiveSessionChanged;

    public void AddSession(DocumentSession session, bool activate = true)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (_sessions.Contains(session))
        {
            throw new InvalidOperationException("The document session is already open.");
        }

        if (FindByCanonicalPath(session.FilePath) is not null)
        {
            throw new InvalidOperationException(
                $"The document '{session.FilePath}' is already open.");
        }

        _sessions.Add(session);
        SessionsChanged?.Invoke(this, EventArgs.Empty);
        if (ActiveSession is null || activate)
        {
            ActivateSession(session);
        }
    }

    public bool Contains(DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return _sessions.Contains(session);
    }

    public void ActivateSession(DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!_sessions.Contains(session))
        {
            throw new InvalidOperationException(
                "The document session does not belong to this workspace.");
        }

        SetActiveSession(session);
    }

    public bool RemoveSession(DocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        int index = _sessions.IndexOf(session);
        if (index < 0)
        {
            return false;
        }

        if (!ReferenceEquals(ActiveSession, session))
        {
            _sessions.RemoveAt(index);
            SessionsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        DocumentSession? next = _sessions.Count == 1
            ? null
            : _sessions[index == _sessions.Count - 1 ? index - 1 : index + 1];
        SetActiveSession(next);
        _sessions.RemoveAt(index);
        SessionsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public DocumentSession? FindByCanonicalPath(string path)
    {
        string canonicalPath = CanonicalizePath(path);
        return _sessions.FirstOrDefault(session =>
            StringComparer.OrdinalIgnoreCase.Equals(
                CanonicalizePath(session.FilePath),
                canonicalPath));
    }

    public void ReplaceAllWith(DocumentSession? session)
    {
        if (ReferenceEquals(ActiveSession, session) &&
            _sessions.Count == (session is null ? 0 : 1))
        {
            return;
        }

        DocumentSession? previous = ActiveSession;
        var args = new ActiveDocumentSessionChangedEventArgs(previous, session);
        ActiveSessionChanging?.Invoke(this, args);
        _sessions.Clear();
        if (session is not null)
        {
            _sessions.Add(session);
        }

        ActiveSession = session;
        SessionsChanged?.Invoke(this, EventArgs.Empty);
        ActiveSessionChanged?.Invoke(this, args);
    }

    private void SetActiveSession(DocumentSession? session)
    {
        if (ReferenceEquals(ActiveSession, session))
        {
            return;
        }

        DocumentSession? previous = ActiveSession;
        var args = new ActiveDocumentSessionChangedEventArgs(previous, session);
        ActiveSessionChanging?.Invoke(this, args);
        ActiveSession = session;
        ActiveSessionChanged?.Invoke(this, args);
    }

    private static string CanonicalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return Path.TrimEndingDirectorySeparator(fullPath);
    }
}
