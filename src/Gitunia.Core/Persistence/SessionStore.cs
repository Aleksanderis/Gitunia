using System.Text.Json;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Persistence;

/// <summary>
/// Loads and saves the session (open tabs, recents, pins, per-tab view state) as <c>session.json</c> in a state directory.
/// Saves are atomic. Loading never throws for bad input: a missing or corrupt file yields an empty workspace. A corrupt file is set aside as <c>session.json.corrupt</c> (never
/// overwriting an earlier one), a file of another schema version as <c>session.json.v&lt;n&gt;.bak</c>; an unreadable file is left alone.
/// </summary>
public sealed class SessionStore : IDisposable
{
    /// <summary>The session file name inside the state directory.</summary>
    public const string FileName = "session.json";

    /// <summary>The lock file name inside the state directory; held open by the instance that owns the session.</summary>
    public const string LockFileName = "session.lock";

    private const string OwnedByOtherNotice = "Session owned by another Gitunia: tabs opened here are not saved";
    private const long MaximumFileBytes = 4 * 1024 * 1024;

    private readonly Func<string, bool> _directoryExists;
    private readonly string _stateDirectory;
    private FileStream? _lock;
    private bool _ownedByOther;

    /// <summary>Creates a store for a state directory (from <c>IPlatform.StateDirectory</c>; tests pass a temp directory).</summary>
    /// <param name="stateDirectory">The directory holding the session file.</param>
    /// <param name="directoryExists">Existence check for repository directories; defaults to the file system.</param>
    public SessionStore(string stateDirectory, Func<string, bool>? directoryExists = null)
    {
        _stateDirectory = stateDirectory;
        FilePath = Path.Combine(stateDirectory, FileName);
        _directoryExists = directoryExists ?? Directory.Exists;
    }

    /// <summary>The full path of the session file.</summary>
    public string FilePath { get; }

    /// <summary>Reads the session. Never throws except for cancellation.</summary>
    public async Task<SessionLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        AtomicFileWriter.RemoveStaleTemporaryFiles(_stateDirectory);
        TakeOwnership();
        var result = await ReadAsync(cancellationToken).ConfigureAwait(false);

        return _ownedByOther ? result with { Detail = OwnedByOtherNotice } : result;
    }

    /// <summary>Writes the session without drafts; see the overload with drafts.</summary>
    /// <param name="workspace">The workspace to save.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public Task<bool> SaveAsync(Workspace workspace, CancellationToken cancellationToken) =>
        SaveAsync(workspace, null, cancellationToken);

    /// <summary>Writes the session atomically. Returns false when the file could not be written (read-only or full disk); the app keeps running.</summary>
    /// <param name="workspace">The workspace to save.</param>
    /// <param name="drafts">The commit drafts by tab id; null saves none.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task<bool> SaveAsync(
        Workspace workspace,
        IReadOnlyDictionary<TabId, SessionDraft>? drafts,
        CancellationToken cancellationToken)
    {
        if (_ownedByOther)
        {
            // Another instance owns the file: its tabs win and this one saves nothing (not a failure, so no error message).
            return true;
        }

        var content = JsonSerializer.SerializeToUtf8Bytes(SessionDocumentMapper.ToDocument(workspace, drafts), SessionJsonContext.Default.SessionDocument);
        try
        {
            await AtomicFileWriter.WriteAsync(FilePath, content, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Gives up the session lock so another instance can own the session.</summary>
    public void Dispose()
    {
        _lock?.Dispose();
    }

    private async Task<SessionLoadResult> ReadAsync(CancellationToken cancellationToken)
    {
        byte[] content;
        try
        {
            var info = new FileInfo(FilePath);
            if (!info.Exists)
            {
                return new SessionLoadResult(new WorkspaceUpdate(Workspace.Empty, []), SessionLoadStatus.Missing, null);
            }

            if (info.Length > MaximumFileBytes)
            {
                return SetAside("the session file is too large", ".corrupt", SessionLoadStatus.Discarded);
            }

            content = await SharedFile.ReadAllBytesAsync(FilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A read error says nothing about the content (a lock, a permission glitch): leave the file where it is.
            return new SessionLoadResult(new WorkspaceUpdate(Workspace.Empty, []), SessionLoadStatus.Discarded, "the session file could not be read");
        }

        SessionDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(content, SessionJsonContext.Default.SessionDocument);
        }
        catch (JsonException)
        {
            return SetAside("the session file is not valid JSON", ".corrupt", SessionLoadStatus.Discarded);
        }

        if (document is null)
        {
            return SetAside("the session file is empty", ".corrupt", SessionLoadStatus.Discarded);
        }

        if (document.Version != SessionDocument.CurrentVersion)
        {
            return SetAside("the session file is from another version of gitunia", $".v{document.Version}.bak", SessionLoadStatus.UnsupportedVersion);
        }

        var session = SessionDocumentMapper.ToSession(document, _directoryExists);
        return new SessionLoadResult(session.Update, SessionLoadStatus.Restored, null) { Drafts = session.Drafts };
    }

    // The first store to load holds session.lock until the process ends (the OS releases it, also after a kill); a second
    // instance finds it taken and never writes, so two instances cannot overwrite each other's tabs, pins and recents.
    private void TakeOwnership()
    {
        try
        {
            Directory.CreateDirectory(_stateDirectory);
            _lock = new FileStream(
                Path.Combine(_stateDirectory, LockFileName),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.DeleteOnClose);
        }
        catch (IOException)
        {
            _ownedByOther = true;
        }
        catch (UnauthorizedAccessException)
        {
            // A state directory we cannot write to: saves fail and say so; there is no other owner to defer to.
        }
    }

    private SessionLoadResult SetAside(string reason, string suffix, SessionLoadStatus status)
    {
        try
        {
            var target = FilePath + suffix;
            if (suffix == ".corrupt" && File.Exists(target))
            {
                target = $"{target}.{Guid.NewGuid().ToString("N")[..8]}";
            }

            File.Move(FilePath, target, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Setting the file aside is best effort; the next save replaces it anyway.
        }

        return new SessionLoadResult(new WorkspaceUpdate(Workspace.Empty, []), status, reason);
    }
}
