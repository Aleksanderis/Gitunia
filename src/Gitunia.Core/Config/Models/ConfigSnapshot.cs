namespace Gitunia.Core.Config.Models;

/// <summary>The config file as read from disk, used to detect that it changed before a write.</summary>
/// <param name="Exists">False when there is no file yet.</param>
/// <param name="Text">The decoded text without a byte order mark.</param>
/// <param name="Hash">A hash of the file bytes (empty when the file does not exist).</param>
/// <param name="Error">Why the file cannot be used (unreadable, too large, not UTF-8); null when fine.</param>
/// <param name="HasBom">True when the file starts with a UTF-8 byte order mark.</param>
/// <param name="ReadFailed">True when <paramref name="Error"/> is an I/O failure rather than unusable content.</param>
public sealed record ConfigSnapshot(
    bool Exists,
    string Text,
    string Hash,
    string? Error = null,
    bool HasBom = false,
    bool ReadFailed = false);
