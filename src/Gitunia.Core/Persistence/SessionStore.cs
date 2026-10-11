using System.Text.Json;
using Gitunia.Core.Workspaces;

namespace Gitunia.Core.Persistence;

/// <summary>
/// Loads and saves the session (open tabs, recents, pins, per-tab view state) as <c>session.json</c> in a state directory.
/// Saves are atomic. Loading never throws for bad input: a missing or corrupt file yields an empty workspace, and a corrupt
/// file is set aside as <c>session.json.corrupt</c> so it is not silently overwritten without a trace.
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
                return Discard("the session file is too large");
            }

            content = await File.ReadAllBytesAsync(FilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Discard("the session file could not be read");
        }

        SessionDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(content, SessionJsonContext.Default.SessionDocument);
        }
        catch (JsonException)
        {
            return Discard("the session file is not valid JSON");
        }

        if (document is null || document.Version != SessionDocument.CurrentVersion)
        {
            return Discard("the session file has an unsupported version");
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

    private SessionLoadResult Discard(string reason)
    {
        try
        {
            File.Move(FilePath, FilePath + ".corrupt", overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Setting the file aside is best effort; the next save replaces it anyway.
        }

        return new SessionLoadResult(new WorkspaceUpdate(Workspace.Empty, []), SessionLoadStatus.Discarded, reason);
    }
}
