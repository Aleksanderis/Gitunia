namespace Gitunia.Core.Persistence;

/// <summary>
/// Reads files that other processes may replace at any time. Every read shares read, write and delete, so it never blocks the replace
/// (on Windows a plain read denies delete and rename to everyone else for the length of the read).
/// </summary>
public static class SharedFile
{
    /// <summary>Opens a file for reading with <see cref="FileShare.ReadWrite"/> and <see cref="FileShare.Delete"/>.</summary>
    /// <param name="path">The file to read.</param>
    /// <param name="useAsync">True when the stream is read asynchronously.</param>
    public static FileStream OpenRead(string path, bool useAsync = false) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync);

    /// <summary>Reads all bytes of a file without denying others the right to replace or delete it.</summary>
    public static async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
    {
        var stream = OpenRead(path, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            return memory.ToArray();
        }
    }

    /// <summary>Reads a file as UTF-8 text (a byte order mark is honoured) without denying others the right to replace or delete it.</summary>
    public static async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
    {
        var stream = OpenRead(path, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
