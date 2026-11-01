using Gitunia.Core.Status;

namespace Gitunia.Core.Effects;

/// <summary>Show a message in the status bar and keep it in the history.</summary>
/// <param name="Text">The message.</param>
/// <param name="Severity">How it is drawn.</param>
public sealed record PostStatusEffect(string Text, StatusSeverity Severity) : AppEffect;
