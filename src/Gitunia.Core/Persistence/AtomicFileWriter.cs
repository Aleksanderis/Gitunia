namespace Gitunia.Core.Persistence;

/// <summary>
/// Replaces a file atomically: write a temp file beside it, flush to disk, then rename over the target, so readers never see a partial file.
/// The rename is retried (5 tries, 25 to 100 ms apart) when another process holds the target open without delete sharing (Windows).
/// </summary>
public static class AtomicFileWriter
{
    private const int ReplaceAttempts = 5;
    private const int GuidDigits = 32;
    private const string TemporarySuffix = ".tmp";
    private const string GuidFormat = "N";
    private static readonly TimeSpan BackOff = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan StaleAge = TimeSpan.FromMinutes(1);

    /// <summary>Writes the bytes to the path, creating the directory when needed. The old file stays intact if anything fails.</summary>
    public static Task WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        return WriteAsync(path, content, null, cancellationToken);
    }

    /// <summary>Like the plain overload, and gives the new file these Unix permissions (ignored on Windows).</summary>
    public static async Task WriteAsync(
        string path,
        ReadOnlyMemory<byte> content,
        UnixFileMode? mode,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $"{Path.GetFileName(path)}.{Guid.NewGuid().ToString(GuidFormat)}{TemporarySuffix}");
        try
        {
            var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (mode is { } permissions && !OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(temporaryPath, permissions);
            }

            await ReplaceAsync(temporaryPath, path, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    /// <summary>
    /// Deletes <c>&lt;name&gt;.&lt;guid&gt;.tmp</c> files in the directory that are older than a minute: what a killed process or a failed
    /// replace left behind. Best effort, never throws; a younger file may belong to a save in progress.
    /// </summary>
    /// <param name="directory">The directory holding Gitunia's own files.</param>
    public static void RemoveStaleTemporaryFiles(string directory)
    {
        try
        {
            var cutoff = DateTime.UtcNow - StaleAge;
            foreach (var file in Directory.EnumerateFiles(directory, $"*{TemporarySuffix}"))
            {
                if (IsTemporaryName(Path.GetFileName(file)) && File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    TryDelete(file);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Best effort: a missing directory or a listing error leaves the files for the next start.
        }
    }

    private static async Task ReplaceAsync(string temporaryPath, string path, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temporaryPath, path, overwrite: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException && attempt < ReplaceAttempts)
            {
                await Task.Delay(BackOff * attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // Matches the "<name>.<32 hex digits>.tmp" names made above, so other .tmp files in the directory stay alone.
    private static bool IsTemporaryName(string fileName)
    {
        var withoutSuffix = fileName[..^TemporarySuffix.Length];
        var dot = withoutSuffix.LastIndexOf('.');

        return dot > 0
            && withoutSuffix.Length - dot - 1 == GuidDigits
            && Guid.TryParseExact(withoutSuffix[(dot + 1)..], GuidFormat, out _);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp file is harmless.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort: a leftover temp file is harmless.
        }
    }
}
