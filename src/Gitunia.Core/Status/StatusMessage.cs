namespace Gitunia.Core.Status;

/// <summary>One entry of the status log.</summary>
/// <param name="Sequence">Position in posting order, starting at 1; unique within a log.</param>
/// <param name="Text">What to tell the user, one line.</param>
/// <param name="Severity">How serious it is.</param>
public sealed record StatusMessage(long Sequence, string Text, StatusSeverity Severity);
