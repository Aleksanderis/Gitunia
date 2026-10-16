namespace Gitunia.Core.Overlays;

/// <summary>
/// What a confirm dialog says before a destructive action (ux.md, "Confirm mode"): what will happen, what is lost or how to get it back,
/// and what the <c>y</c> answer is called. Plain data, so the text of a dialog is testable without a terminal.
/// </summary>
/// <param name="Title">The dialog title, such as <c>Discard changes</c>.</param>
/// <param name="Lines">What will happen, one entry per paragraph (wrapped to the dialog width).</param>
/// <param name="Consequence">What cannot be undone, or the undo hint (shown in the warning colour).</param>
/// <param name="YesLabel">What <c>y</c> does, such as <c>Discard changes</c>.</param>
public sealed record ConfirmPrompt(string Title, IReadOnlyList<string> Lines, string Consequence, string YesLabel);
