using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;

namespace Gitunia.Core.Overlays;

/// <summary>The command palette: all entries, the typed filter and the highlighted row. Immutable and pure.</summary>
/// <param name="Entries">Every action.</param>
/// <param name="Field">The typed filter with its caret, edited like every other text field.</param>
/// <param name="SelectedIndex">Index into <see cref="Visible"/>.</param>
public sealed record PaletteState(IReadOnlyList<PaletteEntry> Entries, TextField? Field = null, int SelectedIndex = 0)
{
    /// <summary>The typed filter with its caret.</summary>
    public TextField Field { get; init; } = Field ?? new TextField();

    /// <summary>What was typed.</summary>
    public string Query => Field.Text;

    /// <summary>The highlighted entry, null when nothing matches.</summary>
    public PaletteEntry? Selected
    {
        get
        {
            var visible = Visible();
            return visible.Count == 0 ? null : visible[ListViewport.ClampIndex(SelectedIndex, visible.Count)];
        }
    }

    /// <summary>The entries matching <see cref="Query"/>, best match first (all of them, in order, for an empty query).</summary>
    public IReadOnlyList<PaletteEntry> Visible()
    {
        if (Query.Length == 0)
        {
            return Entries;
        }

        // Groups stay together: the group holding the best match comes first, and groups without a match disappear.
        var scored = Entries
            .Select(entry => new ScoredPaletteEntry(entry, BestScore(entry)))
            .Where(item => item.Score is not null)
            .ToList();
        var groupBest = scored
            .GroupBy(item => item.Entry.Group)
            .ToDictionary(group => group.Key ?? string.Empty, group => group.Max(item => item.Score));
        return [.. scored
            .OrderByDescending(item => groupBest[item.Entry.Group ?? string.Empty])
            .ThenBy(item => item.Entry.Group, StringComparer.Ordinal)
            .ThenByDescending(item => item.Score)
            .Select(item => item.Entry)];
    }

    /// <summary>The palette after text is typed or pasted at the caret; the highlight goes back to the first row.</summary>
    /// <param name="text">The text.</param>
    public PaletteState Type(string text) =>
        Field.Insert(text) is { } field ? this with { Field = field, SelectedIndex = 0 } : this;

    /// <summary>The palette after a key edits the filter (caret moves, word and line deletions, Backspace on a grapheme); unchanged when the key does nothing.</summary>
    /// <param name="key">The key.</param>
    public PaletteState Edit(KeyEvent key) =>
        Field.Apply(key) is { } field ? With(field) : this;

    /// <summary>The palette after Backspace.</summary>
    public PaletteState DeleteLast() => Edit(new KeyEvent(KeyNames.Backspace));

    /// <summary>Moves the highlight by <paramref name="delta"/> rows, stopping at the ends.</summary>
    public PaletteState Move(int delta) =>
        this with { SelectedIndex = ListViewport.ClampIndex(SelectedIndex + delta, Visible().Count) };

    private PaletteState With(TextField field) =>
        this with { Field = field, SelectedIndex = field.Text == Field.Text ? SelectedIndex : 0 };

    private int? BestScore(PaletteEntry entry)
    {
        var title = FuzzyMatcher.Score(Query, entry.Title);
        var identifier = FuzzyMatcher.Score(Query, entry.Action.Value);
        if (title is null)
        {
            return identifier;
        }

        if (identifier is null)
        {
            return title;
        }

        return Math.Max(title.Value, identifier.Value);
    }
}
