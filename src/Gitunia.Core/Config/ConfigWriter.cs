using System.Security.Cryptography;
using System.Text;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Persistence;

namespace Gitunia.Core.Config;

/// <summary>
/// Changes one key of <c>config.json</c> on disk: reads the file, checks it did not change since the caller read it,
/// applies <see cref="ConfigTextEditor"/> and replaces the file atomically (temporary file beside it, then rename).
/// A file that does not parse is never written. A symlinked file is resolved and its target replaced. BOM and line
/// endings of the file are kept. The short window between the stale check and the rename is accepted.
/// </summary>
public sealed class ConfigWriter
{
    private const long MaximumFileBytes = 1024 * 1024;
    private const string ShadowedMessage = "The key is written twice in config.json; the last one was changed";
    private static readonly byte[] ByteOrderMark = [0xEF, 0xBB, 0xBF];

    /// <summary>Creates a writer for the config file of a config directory.</summary>
    /// <param name="configDirectory">The directory holding the config file (tests pass a temp directory).</param>
    public ConfigWriter(string configDirectory)
    {
        FilePath = Path.Combine(configDirectory, ConfigLoader.FileName);
    }

    /// <summary>The full path of the config file (a symlink is not resolved here).</summary>
    public string FilePath { get; }

    /// <summary>Reads the file for a later <see cref="SetAsync"/>; never throws except for cancellation.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<ConfigSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        return await ReadFileAsync(ResolveTarget(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sets one key; see the class remarks. Never throws except for cancellation.</summary>
    /// <param name="path">Property names from the root, for example <c>tabs</c>, <c>showBranch</c>.</param>
    /// <param name="jsonValue">One JSON value (see <see cref="ConfigTextEditor.StringValue"/>).</param>
    /// <param name="expected">The snapshot the caller's view of the file is based on.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task<ConfigWriteResult> SetAsync(
        IReadOnlyList<string> path,
        string jsonValue,
        ConfigSnapshot expected,
        CancellationToken cancellationToken)
    {
        var target = ResolveTarget();
        var current = await ReadFileAsync(target, cancellationToken).ConfigureAwait(false);
        if (current.Error is { } error)
        {
            var status = current.ReadFailed ? ConfigWriteStatus.Failed : ConfigWriteStatus.Refused;

            return new ConfigWriteResult(status, current, error, false);
        }

        if (current.Exists != expected.Exists || current.Hash != expected.Hash)
        {
            const string stale = "config.json changed on disk; reloaded it, repeat the change";

            return new ConfigWriteResult(ConfigWriteStatus.Stale, current, stale, false);
        }

        var edit = ConfigTextEditor.Set(current.Text, path, jsonValue);
        if (!edit.Edited)
        {
            return new ConfigWriteResult(ConfigWriteStatus.Refused, current, edit.Message, false);
        }

        return await WriteAsync(target, current, edit, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ConfigWriteResult> WriteAsync(
        string target,
        ConfigSnapshot current,
        ConfigEditResult edit,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = Encoding.UTF8.GetBytes(edit.Text);
            var bytes = current.HasBom ? [.. ByteOrderMark, .. body] : body;
            UnixFileMode? mode = current.Exists && !OperatingSystem.IsWindows() ? File.GetUnixFileMode(target) : null;
            await AtomicFileWriter.WriteAsync(target, bytes, mode, cancellationToken).ConfigureAwait(false);
            var written = new ConfigSnapshot(true, edit.Text, Hash(bytes), HasBom: current.HasBom);
            var message = edit.Shadowed ? ShadowedMessage : string.Empty;

            return new ConfigWriteResult(ConfigWriteStatus.Written, written, message, edit.Shadowed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var message = $"config.json could not be written: {exception.Message}";

            return new ConfigWriteResult(ConfigWriteStatus.Failed, current, message, false);
        }
    }

    private static async Task<ConfigSnapshot> ReadFileAsync(string target, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(target);
            if (!info.Exists)
            {
                return new ConfigSnapshot(false, string.Empty, string.Empty);
            }

            if (info.Length > MaximumFileBytes)
            {
                return Unusable("config.json is too large to edit");
            }

            var bytes = await SharedFile.ReadAllBytesAsync(target, cancellationToken).ConfigureAwait(false);
            var hasBom = bytes.AsSpan().StartsWith(ByteOrderMark);
            var offset = hasBom ? ByteOrderMark.Length : 0;
            var strict = new UTF8Encoding(false, true);

            var text = strict.GetString(bytes, offset, bytes.Length - offset);

            return new ConfigSnapshot(true, text, Hash(bytes), HasBom: hasBom);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Unusable($"config.json could not be read: {exception.Message}") with { ReadFailed = true };
        }
        catch (DecoderFallbackException)
        {
            return Unusable("config.json is not valid UTF-8");
        }
    }

    private static ConfigSnapshot Unusable(string error)
    {
        return new ConfigSnapshot(true, string.Empty, string.Empty, error);
    }

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    // A symlinked config.json is edited through the link: the temporary file goes next to the real file.
    private string ResolveTarget()
    {
        try
        {
            return File.ResolveLinkTarget(FilePath, returnFinalTarget: true)?.FullName ?? FilePath;
        }
        catch (IOException)
        {
            return FilePath;
        }
    }
}
