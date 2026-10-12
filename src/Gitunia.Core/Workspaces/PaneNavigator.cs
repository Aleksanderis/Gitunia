namespace Gitunia.Core.Workspaces;

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
        return index < 0 ? ordered[0] : ordered[(index + (forward ? 1 : ordered.Count - 1)) % ordered.Count];
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
}
