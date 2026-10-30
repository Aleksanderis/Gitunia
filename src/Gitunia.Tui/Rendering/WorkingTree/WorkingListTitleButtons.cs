using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Files;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>
/// The button at the right end of a file list's title row: <c>stage all</c> on the Unstaged list and <c>unstage all</c> on the
/// Staged one, drawn as a padded chip like the tabs. They are the mouse path of <c>&lt;space&gt;A</c> and <c>&lt;space&gt;U</c>. The
/// button is left out when the title and the button do not fit the row.
/// </summary>
public static class WorkingListTitleButtons
{
    private const string StageAllLabel = "stage all";
    private const string UnstageAllLabel = "unstage all";
    private const string ResolveLabel = "resolve";
    private const string DiffLabel = "diff ›";

    // The title row starts with a stretch of line and the title chip (" title ", padded), then at least one column of line follows it.
    private const int TitleOverhead = 4;

    // One column of line stays between the button and the corner of the box.
    private const int EndGap = 1;

    /// <summary>The <c>view ▾</c> chip of a list, left of its stage all button, or null when the title row has no room
    /// for it.</summary>
    /// <param name="section">The list.</param>
    /// <param name="title">The title text of the list.</param>
    /// <param name="paneWidth">Columns of the list's content.</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap.</param>
    /// <param name="hasDiffButton">The <c>diff ›</c> chip is shown (narrow screen); it keeps its place.</param>
    /// <returns>The chip, with its start column counted from the first content column.</returns>
    public static TitleButton? ViewButton(
        WorkingTreeSection section,
        string title,
        int paneWidth,
        ChipKeys? keys,
        bool hasDiffButton = false)
    {
        // The stage all key goes before the view chip does: it is dropped when that is what keeps the chip.
        foreach (var showStageKey in (bool[])[true, false])
        {
            var stage = Layout(section, title, paneWidth, keys, showStageKey);

            // One column of line stays between the two chips, or between the chip and the corner of the box.
            var rightEdge = stage is null ? paneWidth - EndGap : stage.Start - 1;
            if (hasDiffButton)
            {
                rightEdge = DiffButton(section, title, paneWidth, keys, showStageKey)?.Start - 1 ?? rightEdge;
            }

            var chip = ViewChip.Layout(
                DefaultMouseMap.ListViewButton,
                ListViewOf(section),
                TextWidth.Of(title) + TitleOverhead,
                rightEdge,
                keys);
            if (chip is not null || !showStageKey)
            {
                return chip;
            }
        }

        return null;
    }

    /// <summary>The <c>resolve</c> chip of the Unstaged list (the mouse path of <c>&lt;space&gt;gr</c>, #701), left of
    /// the view and stage all chips, or null when the title row has no room for it. The caller shows it only while
    /// the cursor is on a conflicted file.</summary>
    /// <param name="title">The title text of the list.</param>
    /// <param name="paneWidth">Columns of the list's content.</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap.</param>
    /// <param name="hasDiffButton">The <c>diff ›</c> chip is shown (narrow screen).</param>
    /// <returns>The chip, with its start column counted from the first content column.</returns>
    public static TitleButton? ResolveButton(string title, int paneWidth, ChipKeys? keys, bool hasDiffButton = false)
    {
        var section = WorkingTreeSection.Unstaged;
        var stageStart = Layout(section, title, paneWidth, keys)?.Start ?? paneWidth;
        var viewStart = ViewButton(section, title, paneWidth, keys, hasDiffButton)?.Start ?? paneWidth;
        var diffStart = hasDiffButton ? DiffButton(section, title, paneWidth, keys)?.Start ?? paneWidth : paneWidth;
        var rightEdge = Math.Min(Math.Min(stageStart, viewStart), diffStart) - 1;
        var key = (keys ?? ChipKeys.Default).For(DefaultMouseMap.ListResolveButton, DefaultKeymap.WorkingUnstagedView);
        var earliest = TextWidth.Of(title) + TitleOverhead + 1;
        var candidates = (TitleButton[])
        [
            KeyedChip.Keyed(DefaultMouseMap.ListResolveButton, ResolveLabel, key),
            KeyedChip.Plain(DefaultMouseMap.ListResolveButton, ResolveLabel),
        ];
        foreach (var candidate in candidates)
        {
            var start = rightEdge - candidate.Width;
            if (start >= earliest)
            {
                return candidate with { Start = start };
            }
        }

        return null;
    }

    /// <summary>The <c>diff ›</c> chip of a list on a screen too narrow to show the diff beside it (the mouse path of
    /// <c>Enter</c> / <c>l</c>, #376), left of the stage all button and ahead of the other chips, or null when the
    /// title row has no room for it. The caller shows it only while the diff is not on screen.</summary>
    /// <param name="section">The list.</param>
    /// <param name="title">The title text of the list.</param>
    /// <param name="paneWidth">Columns of the list's content.</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap.</param>
    /// <returns>The chip, with its start column counted from the first content column.</returns>
    public static TitleButton? DiffButton(WorkingTreeSection section, string title, int paneWidth, ChipKeys? keys) =>
        DiffButton(section, title, paneWidth, keys, true) ?? DiffButton(section, title, paneWidth, keys, false);

    // The stage all key goes before the diff chip loses its key or its place.
    private static TitleButton? DiffButton(
        WorkingTreeSection section,
        string title,
        int paneWidth,
        ChipKeys? keys,
        bool showStageKey)
    {
        var stage = Layout(section, title, paneWidth, keys, showStageKey);
        var rightEdge = stage is null ? paneWidth - EndGap : stage.Start - 1;
        var view = ListViewOf(section);
        var key = (keys ?? ChipKeys.Default).For(DefaultMouseMap.ListDiffButton, view);
        var earliest = TextWidth.Of(title) + TitleOverhead + 1;
        var keyed = KeyedChip.Keyed(DefaultMouseMap.ListDiffButton, DiffLabel, key);
        var plain = KeyedChip.Plain(DefaultMouseMap.ListDiffButton, DiffLabel);
        foreach (var candidate in (TitleButton[])[keyed, plain])
        {
            var start = rightEdge - candidate.Width;
            if (start >= earliest)
            {
                return candidate with { Start = start };
            }
        }

        return null;
    }

    /// <summary>The button of a list, or null when the row of <paramref name="paneWidth"/> columns is too narrow for it
    /// after <paramref name="title"/>. It shows its key (<c>&lt;space&gt;A stage all</c>) when that fits and drops it
    /// first when narrow.</summary>
    /// <param name="section">The list.</param>
    /// <param name="title">The title text of the list, as <see cref="WorkingTreePaneRenderer.ListTitle"/> gives it.</param>
    /// <param name="paneWidth">Columns of the list's content (the title row is one column wider on each side).</param>
    /// <param name="keys">Where the key text comes from; null uses the default keymap.</param>
    /// <returns>The button, with its start column counted from the first content column.</returns>
    public static TitleButton? Layout(WorkingTreeSection section, string title, int paneWidth, ChipKeys? keys = null) =>
        Layout(section, title, paneWidth, keys, true);

    private static TitleButton? Layout(
        WorkingTreeSection section,
        string title,
        int paneWidth,
        ChipKeys? keys,
        bool showKey)
    {
        var isStaged = section == WorkingTreeSection.Staged;
        var view = isStaged ? DefaultMouseMap.UnstageAllButton : DefaultMouseMap.StageAllButton;
        var label = isStaged ? UnstageAllLabel : StageAllLabel;
        var key = default(string);
        if (showKey)
        {
            key = (keys ?? ChipKeys.Default).For(view, ListViewOf(section));
        }

        var earliest = TextWidth.Of(title) + TitleOverhead + 1;
        foreach (var candidate in (TitleButton[])[KeyedChip.Keyed(view, label, key), KeyedChip.Plain(view, label)])
        {
            var start = paneWidth - candidate.Width - EndGap;
            if (start >= earliest)
            {
                return candidate with { Start = start };
            }
        }

        return null;
    }

    private static string ListViewOf(WorkingTreeSection section)
    {
        if (section == WorkingTreeSection.Staged)
        {
            return DefaultKeymap.WorkingStagedView;
        }

        return DefaultKeymap.WorkingUnstagedView;
    }
}
