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

    /// <summary>The notice for the character-wise key where no text can be selected by character.</summary>
    public const string NoCharsNotice = "No character selection here (needs a diff without wrap or split view)";

    /// <summary>Applies a command.</summary>
    public static AppUpdate Reduce(AppState state, VisualCommand command) => command switch
    {
        VisualToggled toggled => Toggle(state, toggled),
        VisualCharsToggled chars => ToggleChars(state, chars),
        VisualColumnMoved column => MoveColumn(state, column.Column),
        VisualCharsSelected selected => SelectChars(state, selected),
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
        if (state.Shell.Visual is { Kind: VisualKind.Chars } chars)
        {
            // The row-wise key turns a character-wise selection into whole rows, like vim.
            var rows = chars with { Kind = VisualKind.Lines, AnchorColumn = 0, CursorColumn = 0 };

            return new AppUpdate(state with { Shell = state.Shell with { Visual = rows } }, []);
        }

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

    private static AppUpdate ToggleChars(AppState state, VisualCharsToggled toggled)
    {
        if (state.Shell.Visual is { } active)
        {
            if (active.Kind == VisualKind.Chars)
            {
                return new AppUpdate(End(state), []);
            }

            if (!toggled.HasTextSource)
            {
                // A row selection in a pane without text stays as it is, so its copy keeps working.
                return Notice(state, NoCharsNotice);
            }

            var converted = active with
            {
                Kind = VisualKind.Chars,
                AnchorColumn = 0,
                CursorColumn = toggled.CursorColumn,
            };

            return new AppUpdate(state with { Shell = state.Shell with { Visual = converted } }, []);
        }

        if (state.Shell.Mode != KeyMode.Normal || state.Workspace.ActiveTab is not { } tab)
        {
            return new AppUpdate(state, []);
        }

        if (tab.View.FocusedPane == PaneKind.Sidebar)
        {
            return Notice(state, SidebarNotice);
        }

        if (toggled.CursorRow is not { } row || !toggled.HasTextSource)
        {
            return Notice(state, NoCharsNotice);
        }

        var selection = new VisualSelection(
            tab.Id,
            tab.View.FocusedPane,
            new RowRange(row, row),
            VisualKind.Chars,
            toggled.CursorColumn,
            toggled.CursorColumn);

        var visual = state.Shell.WithMode(KeyMode.Visual) with { Visual = selection };

        return new AppUpdate(state with { Shell = visual }, []);
    }

    private static AppUpdate MoveColumn(AppState state, int column)
    {
        if (state.Shell.Visual is not { Kind: VisualKind.Chars } selection)
        {
            return new AppUpdate(state, []);
        }

        var moved = selection with { CursorColumn = Math.Max(column, 0) };

        return new AppUpdate(state with { Shell = state.Shell with { Visual = moved } }, []);
    }

    private static AppUpdate SelectChars(AppState state, VisualCharsSelected selected)
    {
        var isFree = state.Shell.Mode is KeyMode.Normal or KeyMode.Visual;
        if (!isFree || state.Workspace.ActiveTab is not { } tab || tab.View.FocusedPane == PaneKind.Sidebar)
        {
            return new AppUpdate(state, []);
        }

        var selection = new VisualSelection(
            tab.Id,
            tab.View.FocusedPane,
            new RowRange(selected.Anchor.Line, selected.Cursor.Line),
            VisualKind.Chars,
            selected.Anchor.Column,
            selected.Cursor.Column);
        var shell = state.Shell.Mode == KeyMode.Visual ? state.Shell : state.Shell.WithMode(KeyMode.Visual);

        return new AppUpdate(state with { Shell = shell with { Visual = selection } }, []);
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

        return new AppUpdate(state, [new CopyVisualRangeEffect(selection.Pane, selection.Range, selection.Text)]);
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
