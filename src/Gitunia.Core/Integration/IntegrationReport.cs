using Gitunia.Core.Status;

namespace Gitunia.Core.Integration;

/// <summary>What the status bar says when a reset, merge or operation call ended.</summary>
/// <param name="Text">The message.</param>
/// <param name="Severity">How serious it is.</param>
public sealed record IntegrationReport(string Text, StatusSeverity Severity);
