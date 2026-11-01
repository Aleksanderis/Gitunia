namespace Gitunia.Core.Workspaces.Panes;

/// <summary>Pure pane focus movement: <c>Tab</c> cycling and directional moves over the panes that are currently open.</summary>
public static class PaneNavigator
{
    /// <summary>The next (or previous) open pane in focus order, wrapping around; the first open pane when <paramref name="current"/> is not open.</summary>
    /// <param name="current">The focused pane.</param>
    /// <param name="forward">True for <c>Tab</c>, false for <c>Shift-Tab</c>.</param>
    /// <param name="available">The open panes; a pane not listed is skipped.</param>
    public static PaneKind Cycle(PaneKind current, bool forward, IReadOnlyCollection<PaneKind> available)
    {
        var ordered = Enum.GetValues<PaneKind>().Where(available.Contains).ToList();
        if (ordered.Count == 0)
        {
            return current;
        }

        var index = ordered.IndexOf(current);
        if (index < 0)
        {
            return ordered[0];
        }

        var offset = forward ? 1 : ordered.Count - 1;

        return ordered[(index + offset) % ordered.Count];
    }

    /// <summary>The open pane to the left (or right) of <paramref name="current"/> in the layout, or <paramref name="current"/> at the edge.</summary>
    /// <param name="current">The focused pane.</param>
    /// <param name="toRight">True to move right, false to move left.</param>
    /// <param name="available">The open panes.</param>
    public static PaneKind Step(PaneKind current, bool toRight, IReadOnlyCollection<PaneKind> available)
    {
        var ordered = Enum.GetValues<PaneKind>().Where(available.Contains).ToList();
        var index = ordered.IndexOf(current);
        var target = index + (toRight ? 1 : -1);
        return index < 0 || target < 0 || target >= ordered.Count ? current : ordered[target];
    }

    /// <summary>
    /// The open pane above (or below) <paramref name="current"/>: the sidebar and the graph form the top row, the detail and
    /// diff panes the bottom row. Down from the top row goes to the detail pane (the diff when that is not open), up from the
    /// bottom row to the graph (the sidebar when that is not open); otherwise <paramref name="current"/> stays.
    /// </summary>
    /// <param name="current">The focused pane.</param>
    /// <param name="down">True to move down, false to move up.</param>
    /// <param name="available">The open panes.</param>
    public static PaneKind StepVertically(PaneKind current, bool down, IReadOnlyCollection<PaneKind> available)
    {
        if (WorkingPanes.IsDiff(current))
        {
            // The diff is the whole right column for now: nothing above or below it.
            return current;
        }

        if (current is PaneKind.WorkingUnstaged or PaneKind.WorkingStaged)
        {
            // The Changes view stacks the lists: Unstaged above Staged.
            var listTarget = down ? PaneKind.WorkingStaged : PaneKind.WorkingUnstaged;
            return available.Contains(listTarget) ? listTarget : current;
        }

        var isTop = current is PaneKind.Sidebar or PaneKind.Graph;
        if (isTop != down)
        {
            return current;
        }

        PaneKind[] targets = down ? [PaneKind.Detail, PaneKind.Diff] : [PaneKind.Graph, PaneKind.Sidebar];
        foreach (var target in targets)
        {
            if (available.Contains(target))
            {
                return target;
            }
        }

        return current;
    }
}
