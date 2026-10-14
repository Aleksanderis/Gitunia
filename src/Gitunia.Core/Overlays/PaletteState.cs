namespace Gitunia.Core.Overlays;

/// <summary>The command palette: all entries, the typed filter and the highlighted row. Immutable and pure.</summary>
/// <param name="Entries">Every action.</param>
/// <param name="Query">What was typed.</param>
/// <param name="SelectedIndex">Index into <see cref="Visible"/>.</param>
public sealed record PaletteState(IReadOnlyList<PaletteEntry> Entries, string Query = "", int SelectedIndex = 0)
{
    /// <summary>The highlighted entry, null when nothing matches.</summary>
    public PaletteEntry? Selected
    {
        get
        {
            var visible = Visible();
            return visible.Count == 0 ? null : visible[Math.Clamp(SelectedIndex, 0, visible.Count - 1)];
        }
    }

    /// <summary>The entries matching <see cref="Query"/>, best match first (all of them, in order, for an empty query).</summary>
    public IReadOnlyList<PaletteEntry> Visible()
    {
        if (Query.Length == 0)
        {
            return Entries;
        }

        return [.. Entries
            .Select(entry => new ScoredPaletteEntry(entry, BestScore(entry)))
            .Where(item => item.Score is not null)
            .OrderByDescending(item => item.Score)
            .Select(item => item.Entry)];
    }

    public PaletteState Type(string text) => this with { Query = Query + text, SelectedIndex = 0 };

    public PaletteState DeleteLast() => Query.Length == 0 ? this : this with { Query = TrimLastCodePoint(Query), SelectedIndex = 0 };

    /// <summary>Moves the highlight by <paramref name="delta"/> rows, stopping at the ends.</summary>
    public PaletteState Move(int delta)
    {
        var count = Visible().Count;
        return this with { SelectedIndex = count == 0 ? 0 : Math.Clamp(SelectedIndex + delta, 0, count - 1) };
    }

    private static string TrimLastCodePoint(string text) =>
        text.Length > 1 && char.IsLowSurrogate(text[^1]) ? text[..^2] : text[..^1];

    private int? BestScore(PaletteEntry entry)
    {
        var title = FuzzyMatcher.Score(Query, entry.Title);
        var identifier = FuzzyMatcher.Score(Query, entry.Action.Value);
        return title is null ? identifier : identifier is null ? title : Math.Max(title.Value, identifier.Value);
    }
}
