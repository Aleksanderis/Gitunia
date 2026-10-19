namespace Gitunia.Core.Persistence;

/// <summary>Replaces a file atomically: write a temp file beside it, flush to disk, then rename over the target, so readers never see a partial file.</summary>
public static class AtomicFileWriter
{
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
        var temporaryPath = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid().ToString("N")}.tmp");
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

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
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
