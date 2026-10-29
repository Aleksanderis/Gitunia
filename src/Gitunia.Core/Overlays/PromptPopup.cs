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
/// <param name="Hint">The keys that answer the prompt, such as <c>Enter create   Esc cancel</c>.</param>
public sealed record PromptPopup(
    string Title,
    IReadOnlyList<string> Lines,
    string? Input,
    IReadOnlyList<string> Details,
    string Hint)
{
    private const string CancelHint = "Esc cancel";
    /// <summary>The text between two hints of a prompt footer, also the gap the footer is drawn with.</summary>
    public const string HintGap = "   ";

    /// <summary>A prompt with a text field; <paramref name="acceptLabel"/> names what <c>Enter</c> does.</summary>
    public static PromptPopup ForText(string title, string context, string input, string acceptLabel) =>
        new(title, [context], input, [], $"Enter {acceptLabel}{HintGap}{CancelHint}");

    /// <summary>
    /// A prompt whose text filters candidates (see <see cref="FilterPicker{T}"/>); <paramref name="rows"/> is the
    /// visible candidate list and <paramref name="acceptLabel"/> names what <c>Enter</c> does.
    /// </summary>
    public static PromptPopup ForFilter(
        string title,
        string context,
        string input,
        IReadOnlyList<string> rows,
        string acceptLabel) =>
        new(
            title,
            [context],
            input,
            rows,
            $"Type filters{HintGap}Up/Down select{HintGap}Enter {acceptLabel}{HintGap}{CancelHint}");

    /// <summary>A prompt answered by one key; each option is written as <c>key  label</c>.</summary>
    public static PromptPopup ForChoice(string title, string context, IReadOnlyList<string> options) =>
        new(title, [context], null, options, CancelHint);

    /// <summary>True when both show the same text (a record compares its lists by reference).</summary>
    public bool SameText(PromptPopup? other) =>
        other is not null
        && Title == other.Title
        && Input == other.Input
        && Hint == other.Hint
        && Lines.SequenceEqual(other.Lines)
        && Details.SequenceEqual(other.Details);
}
