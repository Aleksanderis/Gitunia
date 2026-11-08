using Gitunia.Core.Status;

namespace Gitunia.Core.Rebase;

/// <summary>A status line and how loud it is.</summary>
/// <param name="Text">What to tell the user.</param>
/// <param name="Severity">How loud to tell it.</param>
/// <param name="Detail">The undo command, kept in the message history; null when there is none.</param>
public sealed record PostedText(string Text, StatusSeverity Severity, string? Detail = null);
