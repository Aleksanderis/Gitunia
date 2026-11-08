using Gitunia.Core.Editing;

namespace Gitunia.Core.Overlays;

/// <summary>
/// What a prompt popup shows (ux.md, "Prompt popups"): a title, what the answer is for, an optional one-line
/// text field, extra lines under the field (the chosen candidate, the options of a choice) and the keys that
/// answer it. Plain data, so the text of a prompt is testable without a terminal; every prompt that asks for
/// text or a choice uses it.
/// </summary>
/// <param name="Title">The popup title, such as <c>New branch</c>.</param>
/// <param name="Lines">What the answer is for, one entry per paragraph (wrapped to the popup width).</param>
/// <param name="Input">The text typed so far; null for a prompt without a text field (a choice).</param>
/// <param name="Details">Rows under the field: the highlighted candidate, or the options of a choice.</param>
/// <param name="Hints">The keys that answer the prompt, such as <c>Enter create</c> and <c>Esc cancel</c>.</param>
/// <param name="InputCaret">The caret column in <see cref="Input"/> (see <see cref="PromptCaret"/>).</param>
public sealed record PromptPopup(
    string Title,
    IReadOnlyList<string> Lines,
    string? Input,
    IReadOnlyList<string> Details,
    IReadOnlyList<PromptHint> Hints,
    int InputCaret = PromptCaret.End)
{
    private static readonly PromptHint Cancel = new("Esc", "cancel");

    /// <summary>The text between two hints of a prompt footer, also the gap the footer is drawn with.</summary>
    public const string HintGap = "   ";

    /// <summary>The hints as one line of text, for the status bar and tests.</summary>
    public string Hint => string.Join(HintGap, Hints.Select(hint => hint.Text));

    /// <summary>A prompt with a text field; <paramref name="acceptLabel"/> names what <c>Enter</c> does.</summary>
    public static PromptPopup ForText(string title, string context, string input, string acceptLabel) =>
        new(title, [context], input, [], [new("Enter", acceptLabel), Cancel]);

    /// <summary>
    /// A prompt whose text filters candidates (see <see cref="Prompts.PromptPicker"/>); <paramref name="rows"/> is the
    /// visible candidate list and <paramref name="acceptLabel"/> names what <c>Enter</c> does.
    /// </summary>
    public static PromptPopup ForFilter(
        string title,
        string context,
        string input,
        IReadOnlyList<string> rows,
        string acceptLabel,
        bool canSelect = true) =>
        new(
            title,
            [context],
            input,
            rows,
            canSelect
                ? [new(string.Empty, "Type filters"), new("Ctrl-j/k", "select"), new("Enter", acceptLabel), Cancel]
                : [new(string.Empty, "Type filters"), new("Enter", acceptLabel), Cancel]);

    /// <summary>A prompt answered by one key; each option is written as <c>key  label</c>.</summary>
    public static PromptPopup ForChoice(string title, string context, IReadOnlyList<string> options) =>
        new(title, [context], null, options, [Cancel]);

    /// <summary>True when both show the same text (a record compares its lists by reference).</summary>
    public bool SameText(PromptPopup? other) =>
        other is not null
        && Title == other.Title
        && Input == other.Input
        && InputCaret == other.InputCaret
        && Hints.SequenceEqual(other.Hints)
        && Lines.SequenceEqual(other.Lines)
        && Details.SequenceEqual(other.Details);
}
