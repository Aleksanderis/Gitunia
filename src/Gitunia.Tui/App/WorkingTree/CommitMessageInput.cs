using Gitunia.App;
using Gitunia.Core.Commits;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Running;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Input;
using Gitunia.Tui.Rendering.Panes;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// Turns the keys and clicks of the commit message box into commands (ux-workflows.md, workflow 2, "Message and
/// commit"). The
/// state decides what they do (<c>Core/Commits</c>); this class only asks whether the box takes an action, dispatches
/// it, and
/// gives the box the keys typed in Insert mode next to the other prompts.
/// </summary>
/// <param name="host">The state and where commands go.</param>
/// <param name="route">Runs an action as if its key had been pressed (for the Enter this class passes on).</param>
public sealed class CommitMessageInput(Dispatcher host, IActionRoute route) : ITextKeyHandler
{
    /// <summary>True while the message box of the active tab takes typed keys.</summary>
    public bool IsEditing => CommitRules.IsEditing(host.State);

    /// <summary>True while the focused message box shows its cursor: Insert mode, and the box's Normal mode.</summary>
    public bool ShowsCursor => CommitRules.ShowsCursor(host.State);

    /// <summary>Runs the action when it concerns the message box; false lets the other handlers have it.</summary>
    /// <param name="tab">The active tab, showing the Changes view.</param>
    /// <param name="action">The action.</param>
    public bool TryExecute(RepositoryState tab, ActionId action)
    {
        if (!CommitRules.Takes(host.State, tab, action))
        {
            return false;
        }

        if (action == ActionIds.WorkingTreeMessageNewLine && !CommitRules.IsEditing(host.State, tab))
        {
            // Enter in another prompt's Insert mode (such as the search) is that prompt's accept.
            route.Route(ActionIds.InputAccept, null);

            return true;
        }

        host.Dispatch(new CommitActionPressed(tab.Id, action));

        return true;
    }

    /// <summary>Gives a key typed in Insert mode to the editor when the box owns Insert mode; true when it took the
    /// key.</summary>
    /// <param name="key">The key.</param>
    public bool HandleTextKey(KeyEvent key)
    {
        if (host.Workspace.ActiveTab is not { } tab
            || !CommitRules.IsEditing(host.State, tab)
            || tab.Running.IsRunning(RunningOperationKind.Commit)
            || tab.Commit.Draft is null)
        {
            return false;
        }

        host.Dispatch(new CommitKeyTyped(tab.Id, key));

        return true;
    }

    /// <summary>
    /// Puts the editor's cursor where a click on the message box landed. The click's own gesture (focus, Insert
    /// mode) follows; lines are scrolled sideways only while the box is edited, so the offset is read before that.
    /// </summary>
    /// <param name="textColumn">The pointer's column in the text, left padding removed.</param>
    /// <param name="row">The pointer's row in the box.</param>
    /// <param name="textWidth">Text columns of the box.</param>
    public void PlaceCursor(int textColumn, int row, int textWidth)
    {
        if (PointerTarget(textColumn, row, textWidth) is { } target)
        {
            host.Dispatch(new CommitCursorPlaced(target.Tab, target.Line, target.Column));
        }
    }

    /// <summary>
    /// Extends the selection to where a drag with the button held is: the drag's first cell was placed like a click,
    /// so the selection starts there.
    /// </summary>
    /// <param name="textColumn">The pointer's column in the text, left padding removed.</param>
    /// <param name="row">The pointer's row in the box (negative above it, past the height below it).</param>
    /// <param name="textWidth">Text columns of the box.</param>
    public void DragSelection(int textColumn, int row, int textWidth)
    {
        if (PointerTarget(textColumn, row, textWidth) is { } target)
        {
            host.Dispatch(new CommitSelectionDragged(target.Tab, target.Line, target.Column));
        }
    }

    // The line and display column of a pointer cell; only the cursor's own line is scrolled sideways, and only while it is drawn with the cursor.
    private PointerTarget? PointerTarget(int textColumn, int row, int textWidth)
    {
        if (host.Workspace.ActiveTab is not { Commit.Draft: { } draft } tab)
        {
            return null;
        }

        var editor = draft.Editor;
        var line = Math.Clamp(draft.Top + row, 0, editor.Lines.Count - 1);
        var shows = CommitRules.ShowsCursor(host.State);
        var offset = shows && line == editor.CursorRow ? MessageEditorRenderer.ScrollOffset(editor, textWidth) : 0;

        return new PointerTarget(tab.Id, line, Math.Max(0, textColumn) + offset);
    }
}
