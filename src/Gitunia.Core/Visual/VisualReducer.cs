using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Core.Visual;

/// <summary>
/// Visual mode as pure transitions: <c>v</c> anchors a range at the cursor row of the graph, detail, diff or Changes
/// view pane, the cursor moves the other end, and <c>v</c>, <c>Esc</c> or <c>q</c> end it. <c>y</c> copies it and ends
/// it when the copy worked. The range lives in <see cref="Shell.ShellState.Visual"/>; the key mode is Visual while it
/// is active.
/// </summary>
public static class VisualReducer
{
    /// <summary>The status message when the pane ended the range (new rows, or a wrap, width or view change).</summary>
    public const string InvalidatedNotice = "Selection ended: the rows changed";

    /// <summary>The notice for <c>v</c> in the sidebar, which has no ranges.</summary>
    public const string SidebarNotice = "No range selection in the sidebar";

    /// <summary>The notice for <c>v</c> on a pane without rows.</summary>
    public const string NothingToSelectNotice = "Nothing to select";

    /// <summary>Applies a command.</summary>
    public static AppUpdate Reduce(AppState state, VisualCommand command) => command switch
    {
        VisualToggled toggled => Toggle(state, toggled),
        VisualCursorMoved moved => MoveCursor(state, moved.Row),
        VisualCopyRequested => RequestCopy(state),
        VisualCopied copied => Copied(state, copied),
        VisualEnded => new AppUpdate(End(state), []),
        VisualInvalidated => Invalidate(state),
        _ => throw new UnhandledCommandException(command),
    };

    /// <summary>Ends the range when the focus left the pane it belongs to (another pane, another tab, a closed tab).</summary>
    /// <param name="state">The state after a command.</param>
    public static AppState EndIfFocusMoved(AppState state)
    {
        if (state.Shell.Visual is not { } selection)
        {
            return state;
        }

        var tab = state.Workspace.ActiveTab;

        return tab is not null && tab.Id == selection.TabId && tab.View.FocusedPane == selection.Pane
            ? state
            : End(state);
    }

    private static AppUpdate Toggle(AppState state, VisualToggled toggled)
    {
        if (state.Shell.Visual is not null)
        {
            return new AppUpdate(End(state), []);
        }

        if (state.Shell.Mode != KeyMode.Normal || state.Workspace.ActiveTab is not { } tab)
        {
            // A prompt or another modal state owns the keys: a range would strand it.
            return new AppUpdate(state, []);
        }

        var pane = tab.View.FocusedPane;
        if (pane == PaneKind.Sidebar)
        {
            return Notice(state, SidebarNotice);
        }

        if (toggled.CursorRow is not { } row || !toggled.HasRangeSource)
        {
            return Notice(state, NothingToSelectNotice);
        }

        var selection = new VisualSelection(tab.Id, pane, new RowRange(row, row));
        var shell = state.Shell.WithMode(KeyMode.Visual) with { Visual = selection };

        return new AppUpdate(state with { Shell = shell }, []);
    }

    private static AppUpdate MoveCursor(AppState state, int row)
    {
        if (state.Shell.Visual is not { } selection)
        {
            return new AppUpdate(state, []);
        }

        var moved = selection with { Range = selection.Range.MoveCursorTo(row) };

        return new AppUpdate(state with { Shell = state.Shell with { Visual = moved } }, []);
    }

    private static AppUpdate RequestCopy(AppState state)
    {
        if (state.Shell.Visual is not { } selection)
        {
            return new AppUpdate(state, []);
        }

        return new AppUpdate(state, [new CopyVisualRangeEffect(selection.Pane, selection.Range)]);
    }

    private static AppUpdate Copied(AppState state, VisualCopied copied)
    {
        // A failed copy keeps the range so the user can retry (or leave it with Esc).
        var next = copied.Copied ? End(state) : state;

        return copied.Notice.Length == 0 ? new AppUpdate(next, []) : Notice(next, copied.Notice);
    }

    private static AppUpdate Invalidate(AppState state) =>
        state.Shell.Visual is null ? new AppUpdate(state, []) : Notice(End(state), InvalidatedNotice);

    private static AppState End(AppState state)
    {
        if (state.Shell.Visual is null)
        {
            return state;
        }

        var shell = state.Shell with { Visual = null };
        if (shell.Mode == KeyMode.Visual)
        {
            shell = shell.WithMode(KeyMode.Normal);
        }

        return state with { Shell = shell };
    }

    private static AppUpdate Notice(AppState state, string text) =>
        new(state, [new PostStatusEffect(text, StatusSeverity.Info)]);
}
