using System.Collections.Immutable;
using Gitunia.Core.Overlays;
using ChoiceMatcher = Gitunia.Core.Search.FuzzyMatcher;

namespace Gitunia.Core.Prompts;

/// <summary>
/// The candidate list of a picker prompt as immutable data: every
/// choice, those matching the typed text best first, and the highlighted row.
/// </summary>
/// <param name="All">Every choice.</param>
/// <param name="Candidates">The choices matching the text, best match first.</param>
/// <param name="Index">The highlighted candidate.</param>
/// <param name="NoMatch">The row shown (and the notice given) when nothing matches.</param>
public sealed record PromptPicker(
    ImmutableArray<PromptChoice> All,
    ImmutableArray<PromptChoice> Candidates,
    int Index,
    string NoMatch)
{
    /// <summary>A picker over <paramref name="choices"/> filtered by <paramref name="text"/>, the best match highlighted.</summary>
    public static PromptPicker Of(ImmutableArray<PromptChoice> choices, string noMatch, string text) =>
        new PromptPicker(choices, choices, 0, noMatch).Filtered(text);

    /// <summary>Keeps the choices matching <paramref name="text"/>, best first, and highlights the first.</summary>
    public PromptPicker Filtered(string text)
    {
        var pattern = text.Trim();

        return this with
        {
            Candidates =
            [
                .. All
                    .Select(choice => (Choice: choice, Score: ChoiceMatcher.Score(pattern, choice.Match ?? choice.Text)))
                    .Where(pair => pair.Score is not null)
                    .OrderByDescending(pair => pair.Score)
                    .Select(pair => pair.Choice),
            ],
            Index = 0,
        };
    }

    /// <summary>Moves the highlight by <paramref name="step"/>, wrapping around.</summary>
    public PromptPicker Moved(int step)
    {
        var count = Candidates.Length;

        return this with { Index = count == 0 ? 0 : (((Index + step) % count) + count) % count };
    }

    /// <summary>The highlighted candidate, or false when nothing matches.</summary>
    public bool TryGetSelected(out PromptChoice selected)
    {
        if (Index >= 0 && Index < Candidates.Length)
        {
            selected = Candidates[Index];
            return true;
        }

        selected = new PromptChoice(string.Empty, string.Empty);
        return false;
    }

    /// <summary>The visible rows: a window of the candidates with the highlight and the position, or the no-match row.</summary>
    public IReadOnlyList<string> Rows()
    {
        if (Candidates.Length == 0)
        {
            return [NoMatch];
        }

        var rows = PickerRows.Window([.. Candidates.Select(choice => choice.Text)], Index);
        rows.Add(PickerRows.Position(Index, Candidates.Length));

        return rows;
    }
}
