namespace Gitunia.Core.Overlays;

/// <summary>
/// What a confirm dialog says before a destructive action (ux.md, "Confirm mode"): what will happen, what is lost,
/// and what the <c>y</c> answer is called. Plain data, so the text of a dialog is testable without a terminal.
/// </summary>
/// <param name="Title">The dialog title, such as <c>Discard changes</c>.</param>
/// <param name="Lines">What will happen, one entry per paragraph (wrapped to the dialog width).</param>
/// <param name="Consequence">What cannot be undone (shown in the warning colour); empty when there is nothing to warn
/// about. A dialog never carries a restore or undo note.</param>
/// <param name="YesLabel">What <c>y</c> does, such as <c>Discard changes</c>.</param>
public sealed record ConfirmPrompt(string Title, IReadOnlyList<string> Lines, string Consequence, string YesLabel)
{
    /// <summary>Whether the dialog has a warning row.</summary>
    public bool HasConsequence => Consequence.Length > 0;
}
