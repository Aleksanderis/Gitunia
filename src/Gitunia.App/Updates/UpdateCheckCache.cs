using System.Text.Json;
using Gitunia.Core.Persistence;

namespace Gitunia.App.Updates;

/// <summary>
/// The one file the update hint writes: <c>update-check.json</c> in the state directory, holding when the last request
/// went out and the newest version it saw. A missing, unreadable or damaged file reads as "never checked"; a write
/// that fails is ignored (the hint then checks again next start).
/// </summary>
/// <param name="stateDirectory">The state directory (<c>IPlatform.StateDirectory</c>; tests pass a temp directory).</param>
public sealed class UpdateCheckCache(string stateDirectory)
{
    /// <summary>The file's name inside the state directory.</summary>
    public const string FileName = "update-check.json";

    private readonly string _path = Path.Combine(stateDirectory, FileName);

    /// <summary>The remembered check, null when there is none or it cannot be read.</summary>
    public async Task<UpdateCheckRecord?> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            await using var stream = SharedFile.OpenRead(_path, useAsync: true);

            return await JsonSerializer.DeserializeAsync(stream, UpdateJsonContext.Default.UpdateCheckRecord, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Remembers the check; a failure to write is swallowed.</summary>
    public async Task WriteAsync(UpdateCheckRecord record, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(record, UpdateJsonContext.Default.UpdateCheckRecord);
            await AtomicFileWriter.WriteAsync(_path, bytes, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The hint is a convenience: it checks again next start.
        }
    }
}
