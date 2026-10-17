using Gitunia.Core.Status;

namespace Gitunia.Tui.App.Operations;

/// <summary>What the status bar says when an operation call ended.</summary>
/// <param name="Text">The message.</param>
/// <param name="Severity">How serious it is.</param>
public sealed record OperationReport(string Text, StatusSeverity Severity);
