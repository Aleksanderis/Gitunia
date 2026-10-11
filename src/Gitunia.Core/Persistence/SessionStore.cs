using System.Text.Json;
using Gitunia.Core.Workspaces;

namespace Gitunia.Core.Persistence;

/// <summary>
/// Loads and saves the session (open tabs, recents, pins, per-tab view state) as <c>session.json</c> in a state directory.
/// Saves are atomic. Loading never throws for bad input: a missing or corrupt file yields an empty workspace. A corrupt file is set aside as <c>session.json.corrupt</c> (never
/// overwriting an earlier one), a file of another schema version as <c>session.json.v&lt;n&gt;.bak</c>; an unreadable file is left alone.
/// </summary>
public sealed class SessionStore
{
    /// <summary>The session file name inside the state directory.</summary>
    public const string FileName = "session.json";

    private const long MaximumFileBytes = 4 * 1024 * 1024;

    private readonly Func<string, bool> directoryExists;

    /// <summary>Creates a store for a state directory (from <c>IPlatform.StateDirectory</c>; tests pass a temp directory).</summary>
    /// <param name="stateDirectory">The directory holding the session file.</param>
    /// <param name="directoryExists">Existence check for repository directories; defaults to the file system.</param>
    public SessionStore(string stateDirectory, Func<string, bool>? directoryExists = null)
    {
        FilePath = Path.Combine(stateDirectory, FileName);
        this.directoryExists = directoryExists ?? Directory.Exists;
    }

    /// <summary>The full path of the session file.</summary>
    public string FilePath { get; }

    /// <summary>Reads the session. Never throws except for cancellation.</summary>
    public async Task<SessionLoadResult> LoadAsync(CancellationToken cancellationToken)
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

            content = await File.ReadAllBytesAsync(FilePath, cancellationToken).ConfigureAwait(false);
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
            return SetAside("the session file is from another version of gitunia", ".v" + document.Version + ".bak", SessionLoadStatus.UnsupportedVersion);
        }

        var update = SessionDocumentMapper.ToWorkspace(document, directoryExists);
        return new SessionLoadResult(update, SessionLoadStatus.Restored, null);
    }

    /// <summary>Writes the session atomically. Returns false when the file could not be written (read-only or full disk); the app keeps running.</summary>
    public async Task<bool> SaveAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        var content = JsonSerializer.SerializeToUtf8Bytes(SessionDocumentMapper.ToDocument(workspace), SessionJsonContext.Default.SessionDocument);
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

    private SessionLoadResult SetAside(string reason, string suffix, SessionLoadStatus status)
    {
        try
        {
            var target = FilePath + suffix;
            if (suffix == ".corrupt" && File.Exists(target))
            {
                target += "." + Guid.NewGuid().ToString("N")[..8];
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
