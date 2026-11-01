using Gitunia.Core.Status;

namespace Gitunia.Core.Pick;

/// <summary>The status line that tells how a cherry-pick or revert ended.</summary>
/// <param name="Text">What to tell the user.</param>
/// <param name="Severity">How loud to tell it.</param>
public sealed record PickReport(string Text, StatusSeverity Severity);
