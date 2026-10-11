namespace Gitunia.Tui;

/// <summary>Result of a copy attempt, with the message to show in the status bar.</summary>
/// <param name="Succeeded">True when the sequence was written to the terminal.</param>
/// <param name="Message">Confirmation or explanation for the user.</param>
public sealed record ClipboardOutcome(bool Succeeded, string Message);
