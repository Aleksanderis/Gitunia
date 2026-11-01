using Gitunia.Core.Conflicts;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;

namespace Gitunia.Tui.Views;

/// <summary>
/// The buttons on the title row of a file list of the Changes view: stage all / unstage all, the view dropdown, the
/// diff chip and (Unstaged, on a conflicted file) resolve. A button has no width when the title leaves no room for it.
/// </summary>
/// <param name="geometry">Where the lists are.</param>
/// <param name="panes">The lists and the keys and glyphs the buttons are drawn with.</param>
public sealed class WorkingListButtons(PaneGeometry geometry, PaneViews panes)
{
    private static readonly PaneRect Empty = new(0, 0, 0, 0);

    /// <summary>The button of a list, null when the title row has no room for it or it does not apply.</summary>
    /// <param name="section">The list.</param>
    /// <param name="kind">The button.</param>
    public TitleButton? Button(WorkingTreeSection section, ListButtonKind kind)
    {
        var rect = geometry.ListRect(section);
        var tree = section == WorkingTreeSection.Staged ? panes.StagedSource.Tree : panes.UnstagedSource.Tree;
        if (rect.IsEmpty)
        {
            return null;
        }

        var title = WorkingTreePaneRenderer.ListTitle(tree, section);
        var keys = panes.Content.Keys;

        // Where the diff is not beside the list (a screen under 80 columns) the diff chip is its mouse path.
        var hasDiffButton = geometry.WorkingRects().Diff.IsEmpty;

        return kind switch
        {
            ListButtonKind.View => WorkingListTitleButtons.ViewButton(section, title, rect.Width, keys, hasDiffButton),
            ListButtonKind.Resolve => ResolveButton(title, rect, hasDiffButton),
            ListButtonKind.Diff => DiffButton(section, title, rect, hasDiffButton),
            _ => WorkingListTitleButtons.Layout(section, title, rect.Width, keys),
        };
    }

    /// <summary>The drawn line of a button; empty when there is none.</summary>
    /// <param name="section">The list.</param>
    /// <param name="kind">The button.</param>
    public StyledLine Line(WorkingTreeSection section, ListButtonKind kind)
    {
        if (Button(section, kind) is { } button)
        {
            return ViewChip.Line(button, panes.Content.Glyphs);
        }

        return new StyledLine([]);
    }

    /// <summary>The place of a button on the screen; empty when there is none.</summary>
    /// <param name="section">The list.</param>
    /// <param name="kind">The button.</param>
    public PaneRect Rect(WorkingTreeSection section, ListButtonKind kind)
    {
        var rect = geometry.ListRect(section);
        if (Button(section, kind) is { } button)
        {
            return new PaneRect(rect.X + button.Start, rect.Y - 1, button.Width, 1);
        }

        return Empty;
    }

    private TitleButton? ResolveButton(string title, PaneRect rect, bool hasDiffButton)
    {
        if (IsConflictedUnderCursor())
        {
            return WorkingListTitleButtons.ResolveButton(title, rect.Width, panes.Content.Keys, hasDiffButton);
        }

        return null;
    }

    private TitleButton? DiffButton(WorkingTreeSection section, string title, PaneRect rect, bool hasDiffButton)
    {
        if (hasDiffButton)
        {
            return WorkingListTitleButtons.DiffButton(section, title, rect.Width, panes.Content.Keys);
        }

        return null;
    }

    // The resolve chip shows while the Unstaged list's cursor is on a conflicted file (the row <space>gr opens).
    // A file the editor refuses (delete/modify) gets no chip: it would promise what the key then declines.
    private bool IsConflictedUnderCursor() =>
        ConflictedSelection.Find(panes.UnstagedSource.Tree) is { } entry && ConflictEditorSupport.CanOpen(entry.Conflict);
}
