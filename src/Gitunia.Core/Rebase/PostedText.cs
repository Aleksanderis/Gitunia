using Gitunia.Core.Status;

namespace Gitunia.Core.Rebase;

/// <summary>A status line and how loud it is.</summary>
/// <param name="Text">What to tell the user.</param>
/// <param name="Severity">How loud to tell it.</param>
public sealed record PostedText(string Text, StatusSeverity Severity);
