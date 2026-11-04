using System.Collections.Immutable;

namespace Gitunia.Core.Picker;

/// <summary>
/// Lays out the lines of the picker list. The unfiltered list has up to four sections (open, pinned, recent, found);
/// each shows at most a cap of rows and ends with a "more" line that expands it (a "less" line collapses an expanded
/// one). The caps shrink toward <see cref="MinimumShown"/> when the list budget is small; spare lines stay empty, so
/// a section with more rows than its cap always shows the cap and a "more" line. A query or a typed path lists the rows
/// flat: no sections, no caps. Pure: the budget (rows the list can show) is a parameter, so the same plan serves the
/// keyboard (state) and the renderer.
/// </summary>
public static class PickerListPlan
{
    /// <summary>The most rows the framed list shows at once.</summary>
    public const int MaximumListRows = 18;

    /// <summary>The fewest rows the framed list keeps, however short the terminal.</summary>
    public const int MinimumListRows = 4;

    /// <summary>Rows of the framed picker around the list (frame, query, rule, footer).</summary>
    public const int FramedChromeRows = 6;

    /// <summary>Rows every non-empty section keeps, however short the list.</summary>
    public const int MinimumShown = 2;

    private const int OpenCap = 6;
    private const int PinnedCap = 6;
    private const int RecentCap = 5;
    private const int FoundCap = 3;
    private const int SectionCount = 4;
    private const int RecentIndex = 2;
    private const int FoundIndex = 3;

    private static readonly int[] ShrinkOrder = [FoundIndex, 1, 0, RecentIndex];

    private static readonly PickerSection[] Sections =
        [PickerSection.Open, PickerSection.Pinned, PickerSection.Recent, PickerSection.Found];

    /// <summary>The list rows available in an area of <paramref name="areaHeight"/> rows.</summary>
    public static int Budget(int areaHeight) =>
        Math.Clamp(areaHeight - FramedChromeRows, MinimumListRows, MaximumListRows);

    /// <summary>The group a row belongs to.</summary>
    public static PickerSection SectionOf(PickerTags tags)
    {
        if (tags.HasFlag(PickerTags.Open))
        {
            return PickerSection.Open;
        }

        if (tags.HasFlag(PickerTags.Pinned))
        {
            return PickerSection.Pinned;
        }

        return tags.HasFlag(PickerTags.Recent) ? PickerSection.Recent : PickerSection.Found;
    }

    /// <summary>The rows a section shows when it is not expanded and the list has room.</summary>
    public static int CapOf(PickerSection section) => section switch
    {
        PickerSection.Open => OpenCap,
        PickerSection.Pinned => PinnedCap,
        PickerSection.Recent => RecentCap,
        _ => FoundCap,
    };

    /// <summary>Whether the list is flat (a query or a typed path): no sections, no caps.</summary>
    public static bool IsFlat(PickerState state) => state.Query.Length > 0 || state.IsPathMode;

    /// <summary>The lines of the list, spacers included, for a list that can show <paramref name="budget"/> rows.</summary>
    public static ImmutableArray<PickerListEntry> Build(PickerState state, int budget)
    {
        var builder = ImmutableArray.CreateBuilder<PickerListEntry>(state.Rows.Length + SectionCount * 3);
        if (IsFlat(state))
        {
            for (var index = 0; index < state.Rows.Length; index++)
            {
                builder.Add(new PickerListEntry(PickerEntryKind.Row, PickerSection.Found, index, 1, 1));
            }

            return builder.ToImmutable();
        }

        var counts = new int[SectionCount];
        var starts = new int[SectionCount];
        for (var index = 0; index < state.Rows.Length; index++)
        {
            var section = SectionIndex(SectionOf(state.Rows[index].Tags));
            if (counts[section] == 0)
            {
                starts[section] = index;
            }

            counts[section]++;
        }

        var shown = ShownCounts(counts, state.Expanded, budget);
        var withHeaders = HasHeaders(counts, shown);
        var isFirst = true;
        for (var section = 0; section < SectionCount; section++)
        {
            if (counts[section] > 0)
            {
                AddSection(
                    builder,
                    Sections[section],
                    new SectionSpan(starts[section], counts[section], shown[section]),
                    withHeaders,
                    isFirst,
                    state.Expanded);
                isFirst = false;
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>The index in <paramref name="entries"/> of the line the highlight is on, -1 when there is none.</summary>
    public static int PositionOf(IReadOnlyList<PickerListEntry> entries, PickerState state)
    {
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (entry.IsSelectable && entry.RowIndex == state.Highlight && entry.IsToggle == state.IsOnToggle)
            {
                return index;
            }
        }

        // A highlighted row the caps hide is shown as the "more" line of its section.
        if (!state.IsOnToggle && state.Highlight >= 0 && state.Highlight < state.Rows.Length)
        {
            var section = SectionOf(state.Rows[state.Highlight].Tags);
            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index].Kind == PickerEntryKind.More && entries[index].Section == section)
                {
                    return index;
                }
            }
        }

        for (var index = 0; index < entries.Count; index++)
        {
            if (entries[index].IsSelectable)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Puts a highlight the caps hide (after a resize) on the "more" line of its section.</summary>
    public static PickerState Normalize(PickerState state, int budget)
    {
        if (IsFlat(state) || state.Rows.IsEmpty)
        {
            return state.IsOnToggle ? state with { IsOnToggle = false } : state;
        }

        var entries = Build(state, budget);
        var position = PositionOf(entries, state);

        return position < 0 ? state : At(state, entries[position]);
    }

    /// <summary>Moves the highlight over the selectable lines; the list does not wrap.</summary>
    public static PickerState Move(PickerState state, int delta, int budget)
    {
        if (state.Rows.IsEmpty)
        {
            return state;
        }

        var entries = Build(state, budget);
        var selectable = new List<int>();
        for (var index = 0; index < entries.Length; index++)
        {
            if (entries[index].IsSelectable)
            {
                selectable.Add(index);
            }
        }

        var current = Math.Max(0, selectable.IndexOf(PositionOf(entries, state)));
        var target = Math.Clamp(current + delta, 0, selectable.Count - 1);

        return At(state, entries[selectable[target]]);
    }

    /// <summary>Expands the section whose "more" line is highlighted, or collapses the one whose "less" line is.</summary>
    public static PickerState Toggle(PickerState state, int budget)
    {
        var entries = Build(state, budget);
        var position = PositionOf(entries, state);
        if (position < 0 || !entries[position].IsToggle)
        {
            return state;
        }

        var entry = entries[position];
        if (entry.Kind == PickerEntryKind.More)
        {
            // The highlight moves to the first row the expansion reveals.
            return state with
            {
                Expanded = state.Expanded | entry.Section,
                Highlight = entry.RowIndex + 1,
                IsOnToggle = false,
            };
        }

        var collapsed = state with { Expanded = state.Expanded & ~entry.Section };
        var after = Build(collapsed, budget);
        var more = after.FirstOrDefault(
            candidate => candidate.Kind == PickerEntryKind.More && candidate.Section == entry.Section);

        return more is null ? collapsed with { IsOnToggle = false } : At(collapsed, more);
    }

    private static PickerState At(PickerState state, PickerListEntry entry) =>
        state with { Highlight = entry.RowIndex, IsOnToggle = entry.IsToggle };

    private readonly record struct SectionSpan(int Start, int Total, int Shown);

    private static int SectionIndex(PickerSection section) => section switch
    {
        PickerSection.Open => 0,
        PickerSection.Pinned => 1,
        PickerSection.Recent => RecentIndex,
        _ => FoundIndex,
    };

    private static void AddSection(
        ImmutableArray<PickerListEntry>.Builder builder,
        PickerSection section,
        SectionSpan span,
        bool withHeaders,
        bool isFirst,
        PickerSection expanded)
    {
        var (start, total, shown) = (span.Start, span.Total, span.Shown);
        if (withHeaders)
        {
            if (!isFirst)
            {
                builder.Add(new PickerListEntry(PickerEntryKind.Spacer, section, -1, shown, total));
            }

            builder.Add(new PickerListEntry(PickerEntryKind.Header, section, -1, shown, total));
        }

        for (var offset = 0; offset < shown; offset++)
        {
            builder.Add(new PickerListEntry(PickerEntryKind.Row, section, start + offset, shown, total));
        }

        var last = start + shown - 1;
        if (shown < total)
        {
            builder.Add(new PickerListEntry(PickerEntryKind.More, section, last, shown, total));
        }
        else if (expanded.HasFlag(section) && total > CapOf(section))
        {
            builder.Add(new PickerListEntry(PickerEntryKind.Less, section, last, shown, total));
        }
    }

    private static bool HasHeaders(int[] counts, int[] shown)
    {
        var present = 0;
        var isCapped = false;
        for (var section = 0; section < SectionCount; section++)
        {
            present += counts[section] > 0 ? 1 : 0;
            isCapped |= shown[section] < counts[section];
        }

        return present > 1 || isCapped;
    }

    // Lines the sections take: headers with a spacer between, the rows and the more or less lines.
    private static int Lines(int[] counts, int[] shown, PickerSection expanded)
    {
        var present = 0;
        var lines = 0;
        for (var section = 0; section < SectionCount; section++)
        {
            if (counts[section] == 0)
            {
                continue;
            }

            present++;
            lines += shown[section];
            var isExpandedPastCap = expanded.HasFlag(Sections[section]) && counts[section] > CapOf(Sections[section]);
            if (shown[section] < counts[section] || isExpandedPastCap)
            {
                lines++;
            }
        }

        return HasHeaders(counts, shown) ? lines + (present * 2) - 1 : lines;
    }

    private static int[] ShownCounts(int[] counts, PickerSection expanded, int budget)
    {
        var shown = new int[SectionCount];
        for (var section = 0; section < SectionCount; section++)
        {
            var isExpanded = expanded.HasFlag(Sections[section]);
            shown[section] = isExpanded ? counts[section] : Math.Min(counts[section], CapOf(Sections[section]));
        }

        if (expanded == PickerSection.None)
        {
            Shrink(counts, shown, budget);
        }

        return shown;
    }

    // Takes rows from the found, then the pinned, the open and last the recent section (the one the user reaches for most)
    // until the list fits or every section is at its minimum.
    private static void Shrink(int[] counts, int[] shown, int budget)
    {
        while (Lines(counts, shown, PickerSection.None) > budget)
        {
            var pick = -1;
            foreach (var section in ShrinkOrder)
            {
                var canShrink = shown[section] > MinimumShown
                    && (shown[section] < counts[section] || shown[section] - 2 >= MinimumShown);
                if (canShrink)
                {
                    pick = section;
                    break;
                }
            }

            if (pick < 0)
            {
                return;
            }

            // A section that shows everything gains a more line by hiding rows, so it gives up two at a time.
            shown[pick] = shown[pick] < counts[pick] ? shown[pick] - 1 : Math.Max(MinimumShown, shown[pick] - 2);
        }
    }
}
