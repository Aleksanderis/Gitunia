using Gitunia.Core.Status;

namespace Gitunia.Tui.App.Stash;

/// <summary>What the status bar says when a stash operation ended.</summary>
/// <param name="Text">The message.</param>
/// <param name="Severity">How serious it is.</param>
public sealed record StashReport(string Text, StatusSeverity Severity);
