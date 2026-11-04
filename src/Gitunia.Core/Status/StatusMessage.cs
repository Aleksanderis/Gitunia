namespace Gitunia.Core.Status;

/// <summary>One entry of the status log.</summary>
/// <param name="Sequence">Position in posting order, starting at 1; unique within a log.</param>
/// <param name="Text">What to tell the user, one line.</param>
/// <param name="Severity">How serious it is.</param>
/// <param name="PostedAt">When it was posted, by the channel's clock.</param>
/// <param name="Detail">Optional long text for the message history (a restore command, a full hash); the status bar
/// shows only <paramref name="Text"/>.</param>
public sealed record StatusMessage(
    long Sequence,
    string Text,
    StatusSeverity Severity,
    DateTimeOffset PostedAt,
    string? Detail = null);
