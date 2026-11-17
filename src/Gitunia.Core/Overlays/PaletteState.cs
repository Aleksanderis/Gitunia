using Gitunia.Core.Editing;
using Gitunia.Core.Filtering;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Text;

namespace Gitunia.Core.Overlays;

/// <summary>
/// The command palette: a <see cref="FilterList{T}"/> over every action (fuzzy on the title and the action id, groups
/// kept together) with the typed filter and the highlighted row. Immutable and pure.
/// </summary>
/// <param name="List">The actions, the typed filter with its caret and the highlighted row.</param>
public sealed record PaletteState(FilterList<PaletteEntry> List)
{
    /// <summary>The subject of the no-match text.</summary>
    public const string Subject = "action";

    /// <summary>A palette over <paramref name="entries"/>.</summary>
    /// <param name="entries">Every action.</param>
    /// <param name="field">The typed filter with its caret, edited like every other text field.</param>
    /// <param name="selectedIndex">Index into <see cref="Visible"/>.</param>
    public PaletteState(IReadOnlyList<PaletteEntry> entries, TextField? field = null, int selectedIndex = 0)
        : this(FilterLists.Of(
            [.. entries.Select(entry => FilterItems.Of(entry, entry.Title, entry.Action.Value, entry.Group))],
            MatcherKind.Fuzzy,
            UiTexts.NoMatch(Subject),
            field).MoveTo(selectedIndex))
    {
    }

    /// <summary>Every action, in the order of the palette.</summary>
    public IReadOnlyList<PaletteEntry> Entries => [.. List.Items.Select(item => item.Value)];

    /// <summary>The typed filter with its caret.</summary>
    public TextField Field => List.Field;

    /// <summary>What was typed.</summary>
    public string Query => List.Text;

    /// <summary>The highlighted row of <see cref="Visible"/>.</summary>
    public int SelectedIndex => List.Selected;

    /// <summary>The highlighted entry, null when nothing matches.</summary>
    public PaletteEntry? Selected => List.TryGetSelected(out var selected) ? selected : null;

    /// <summary>The entries matching <see cref="Query"/>, best match first (all of them, in order, for an empty query).</summary>
    public IReadOnlyList<PaletteEntry> Visible() => List.Candidates;

    /// <summary>The palette after text is typed or pasted at the caret; the highlight goes back to the first row.</summary>
    /// <param name="text">The text.</param>
    public PaletteState Type(string text) =>
        Field.Insert(text) is { } field ? this with { List = List.WithField(field) } : this;

    /// <summary>The palette after a key edits the filter (caret moves, word and line deletions, Backspace on a grapheme); unchanged when the key does nothing.</summary>
    /// <param name="key">The key.</param>
    public PaletteState Edit(KeyEvent key) =>
        Field.Apply(key) is { } field ? this with { List = List.WithField(field) } : this;

    /// <summary>The palette after Backspace.</summary>
    public PaletteState DeleteLast() => Edit(new KeyEvent(KeyNames.Backspace));

    /// <summary>Highlights the row at <paramref name="index"/> of <see cref="Visible"/>, clamped.</summary>
    public PaletteState MoveTo(int index) => this with { List = List.MoveTo(index) };

    /// <summary>Moves the highlight by <paramref name="delta"/> rows, stopping at the ends.</summary>
    public PaletteState Move(int delta) => this with { List = List.Move(delta) };
}
