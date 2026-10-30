namespace Gitunia.Tui.App.Conflicts;

/// <summary>What a save writes and says, fixed on the UI thread when the user confirmed it.</summary>
/// <param name="Text">The whole file as it is written.</param>
/// <param name="Message">The status line after a successful write.</param>
/// <param name="IsComplete">Whether every conflict was settled, which decides the "mark resolved" question.</param>
internal sealed record SavePlan(string Text, string Message, bool IsComplete);
