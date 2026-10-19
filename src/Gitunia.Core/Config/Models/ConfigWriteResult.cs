namespace Gitunia.Core.Config.Models;

/// <summary>The outcome of <see cref="ConfigWriter.SetAsync"/>.</summary>
/// <param name="Status">How it ended.</param>
/// <param name="Snapshot">The file after the write, or as found when nothing was written.</param>
/// <param name="Message">A message for the user (empty when written without remarks).</param>
/// <param name="Shadowed">True when the key is written more than once in the file.</param>
public sealed record ConfigWriteResult(
    ConfigWriteStatus Status,
    ConfigSnapshot Snapshot,
    string Message,
    bool Shadowed);
