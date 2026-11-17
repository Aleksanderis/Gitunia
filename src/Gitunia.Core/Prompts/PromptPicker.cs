using System.Collections.Immutable;
using Gitunia.Core.Editing;
using Gitunia.Core.Filtering;
using Gitunia.Core.Overlays;

namespace Gitunia.Core.Prompts;

/// <summary>
/// The candidate list of a picker prompt: a <see cref="FilterList{T}"/> over the choices (fuzzy, best match first) with
/// the typed text as its query.
/// </summary>
/// <param name="List">The filtered choices and the highlighted row.</param>
public sealed record PromptPicker(FilterList<PromptChoice> List)
{
    /// <summary>Every choice.</summary>
    public ImmutableArray<PromptChoice> All => [.. List.Items.Select(item => item.Value)];

    /// <summary>The choices matching the text, best match first.</summary>
    public ImmutableArray<PromptChoice> Candidates => List.Candidates;

    /// <summary>The highlighted candidate.</summary>
    public int Index => List.Selected;

    /// <summary>The text shown (and the notice given) when nothing matches.</summary>
    public string NoMatch => List.NoMatch;

    /// <summary>A picker over <paramref name="choices"/> filtered by <paramref name="text"/>, the best match highlighted.</summary>
    public static PromptPicker Of(ImmutableArray<PromptChoice> choices, string noMatch, TextField text)
    {
        var items = choices.Select(choice => FilterItems.Of(choice, choice.Match ?? choice.Text));

        return new PromptPicker(FilterLists.Of([.. items], MatcherKind.Fuzzy, noMatch, text));
    }

    /// <summary>Keeps the choices matching the edited text, best first, and highlights the first.</summary>
    public PromptPicker Filtered(TextField text) => this with { List = List.WithField(text) };

    /// <summary>Moves the highlight by <paramref name="step"/>, stopping at the ends.</summary>
    public PromptPicker Moved(int step) => this with { List = List.Move(step) };

    /// <summary>The highlighted candidate, or false when nothing matches.</summary>
    public bool TryGetSelected(out PromptChoice selected)
    {
        if (List.TryGetSelected(out selected))
        {
            return true;
        }

        selected = new PromptChoice(string.Empty, string.Empty);

        return false;
    }

    /// <summary>
    /// The candidate rows with the highlight and the position (<c>n/m</c>), or the empty text alone (no highlight, no
    /// position) when nothing matches.
    /// </summary>
    public PickerWindow Rows() =>
        new(
            [.. List.Candidates.Select(choice => choice.Text)],
            List.Count == 0 ? -1 : List.Selected,
            List.Position,
            List.EmptyText);
}
