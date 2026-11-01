using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Conflicts;

/// <summary>
/// What the actions of the open conflict editor do: moves, picks, result edits and Esc. Save and re-create are
/// <see cref="ConflictReducer"/>'s, as they leave the editor for git and the file.
/// </summary>
internal static class ConflictActions
{
    private static readonly Dictionary<ActionId, ConflictResultCommand> ResultCommands = new()
    {
        [ActionIds.WorkingTreeMessageLeft] = ConflictResultCommand.Left,
        [ActionIds.WorkingTreeMessageRight] = ConflictResultCommand.Right,
        [ActionIds.WorkingTreeMessageWordNext] = ConflictResultCommand.WordNext,
        [ActionIds.WorkingTreeMessageWordPrevious] = ConflictResultCommand.WordPrevious,
        [ActionIds.WorkingTreeMessageLineStart] = ConflictResultCommand.LineStart,
        [ActionIds.WorkingTreeMessageLineEnd] = ConflictResultCommand.LineEnd,
        [ActionIds.WorkingTreeMessageDeleteChar] = ConflictResultCommand.DeleteChar,
        [ActionIds.WorkingTreeMessageDeleteLine] = ConflictResultCommand.DeleteLine,
        [ActionIds.WorkingTreeMessageOpenLine] = ConflictResultCommand.OpenLine,
        [ActionIds.WorkingTreeMessageAppend] = ConflictResultCommand.Append,
        [ActionIds.WorkingTreeMessageAppendAtEnd] = ConflictResultCommand.AppendAtEnd,
        [ActionIds.WorkingTreeMessageInsertAtStart] = ConflictResultCommand.InsertAtStart,
    };

    /// <summary>An action while the result is not typed in; null when it is not one of the moves, picks or
    /// edits.</summary>
    public static ConflictUpdate? Execute(
        ConflictSession session,
        ConflictEditorState editor,
        ActionId action,
        int? count)
    {
        if (action == ActionIds.Cancel || action == ActionIds.NavigateBack || action == ActionIds.AppQuit)
        {
            return Escape(session, editor, action);
        }

        return Move(session, editor, action, count)
            ?? Pane(session, editor, action)
            ?? Pick(session, editor, action)
            ?? Result(session, editor, action);
    }

    /// <summary>An action while the result is typed in; Esc keeps the text like Ctrl-g does.</summary>
    public static ConflictUpdate? ExecuteInEdit(ConflictSession session, ConflictEditorState editor, ActionId action)
    {
        if (action == ActionIds.ConflictEditorEditNewLine)
        {
            return ConflictFlow.Show(session, editor.TypeKey(new KeyEvent(KeyNames.Enter)));
        }

        var isDone = action == ActionIds.ConflictEditorEditDone
            || action == ActionIds.Cancel
            || action == ActionIds.NavigateBack;

        return isDone ? ConflictFlow.Show(session, editor.EndEdit()) : null;
    }

    /// <summary>A click on a header or a row.</summary>
    public static ConflictUpdate Click(
        ConflictSession session,
        ConflictEditorState editor,
        ConflictTarget target,
        bool isDoubleClick)
    {
        // While typing only a click inside the result moves the cursor; the panes and headers wait until typing ends.
        if (editor.IsEditing && target.Kind != ConflictTargetKind.ResultLine)
        {
            return ConflictFlow.Done(session);
        }

        var moved = target.Kind switch
        {
            ConflictTargetKind.Header => editor.FocusPane(target.Pane).EndVisual(),
            ConflictTargetKind.VersionRow => editor.ClickVersionRow(target.Pane, target.Index),
            _ => editor.ClickResultLine(target.Index, target.Column),
        };
        var isTake = isDoubleClick
            && target.Kind == ConflictTargetKind.VersionRow
            && moved.Alignment.HunkAt(target.Index) >= 0;

        var shown = ConflictFlow.Show(session, moved);

        // The double-click moves first, then takes: a refused take (no common ancestor) leaves the move.
        return isTake ? ConflictFlow.Then(shown, Take(shown.Session, moved, PickOf(target.Pane))) : shown;
    }

    private static ConflictUpdate Escape(ConflictSession session, ConflictEditorState editor, ActionId action)
    {
        // Esc ends a Visual selection first, then closes (a changed file asks for a second Esc).
        if (editor.IsVisual && action != ActionIds.AppQuit)
        {
            return ConflictFlow.Show(session, editor.EndVisual());
        }

        var ended = editor.EndVisual();
        if (ended.IsModified && !ended.IsDiscardPending)
        {
            // Picks are not thrown away by one stray Esc: the first press warns, the second closes.
            return ConflictFlow.Show(session, ended.AskDiscard());
        }

        return ConflictFlow.Close(session);
    }

    private static ConflictUpdate? Move(
        ConflictSession session,
        ConflictEditorState editor,
        ActionId action,
        int? count)
    {
        var steps = Math.Max(1, count ?? 1);
        if (action == ActionIds.ListDown || action == ActionIds.ListUp)
        {
            return ConflictFlow.Show(session, editor.MoveCursor(action == ActionIds.ListDown ? steps : -steps));
        }

        if (action == ActionIds.ListHalfPageDown || action == ActionIds.ListHalfPageUp)
        {
            var half = Math.Max(1, ConflictFlow.PaneRows(session, editor) / 2);

            return ConflictFlow.Show(session, editor.MoveCursor(action == ActionIds.ListHalfPageDown ? half : -half));
        }

        if (action == ActionIds.ListTop)
        {
            return ConflictFlow.Show(session, editor.MoveToTop());
        }

        if (action == ActionIds.ListBottom)
        {
            return ConflictFlow.Show(session, editor.MoveToBottom());
        }

        if (action == ActionIds.ConflictEditorScrollLeft || action == ActionIds.ConflictEditorScrollRight)
        {
            var columns = steps * ConflictEditorState.SidewaysStep;

            return ConflictFlow.Show(
                session,
                editor.ScrollSideways(action == ActionIds.ConflictEditorScrollRight ? columns : -columns));
        }

        if (action == ActionIds.ConflictEditorNext)
        {
            return ConflictFlow.ShowCentered(session, editor.NextConflict());
        }

        return action == ActionIds.ConflictEditorPrevious
            ? ConflictFlow.ShowCentered(session, editor.PreviousConflict())
            : null;
    }

    private static ConflictUpdate? Pane(ConflictSession session, ConflictEditorState editor, ActionId action)
    {
        if (action == ActionIds.PaneNext)
        {
            return ConflictFlow.Show(session, editor.NextPane());
        }

        if (action == ActionIds.PanePrevious)
        {
            return ConflictFlow.Show(session, editor.PreviousPane());
        }

        if (action == ActionIds.PaneFocusLeft)
        {
            return ConflictFlow.Show(session, editor.PaneLeft());
        }

        if (action == ActionIds.PaneFocusRight)
        {
            return ConflictFlow.Show(session, editor.PaneRight());
        }

        if (action == ActionIds.PaneFocusDown)
        {
            return ConflictFlow.Show(session, editor.PaneDown());
        }

        if (action == ActionIds.PaneFocusUp)
        {
            return ConflictFlow.Show(session, editor.PaneUp());
        }

        if (action == ActionIds.VisualToggle)
        {
            return ToggleVisual(session, editor);
        }

        // The copy key of a Visual selection means nothing here but is still the editor's key.
        return action == ActionIds.VisualCopy ? ConflictFlow.Done(session) : null;
    }

    private static ConflictUpdate ToggleVisual(ConflictSession session, ConflictEditorState editor)
    {
        if (!editor.IsVersionPane)
        {
            return ConflictFlow.Warn(session, ConflictNotices.SelectInVersion);
        }

        return ConflictFlow.Show(session, editor.ToggleVisual());
    }

    private static ConflictUpdate? Pick(ConflictSession session, ConflictEditorState editor, ActionId action)
    {
        if (action == ActionIds.ConflictEditorTake)
        {
            return TakeKey(session, editor);
        }

        if (action == ActionIds.ConflictEditorOurs)
        {
            return Take(session, editor, ConflictPick.Ours);
        }

        if (action == ActionIds.ConflictEditorTheirs)
        {
            return Take(session, editor, ConflictPick.Theirs);
        }

        if (action == ActionIds.ConflictEditorBase)
        {
            return Take(session, editor, ConflictPick.Base);
        }

        if (action == ActionIds.ConflictEditorBoth)
        {
            return Take(session, editor, ConflictPick.Both);
        }

        if (action == ActionIds.ConflictEditorAllOurs)
        {
            return ConflictFlow.Show(session, editor.PickAllUnresolved(ConflictPick.Ours));
        }

        if (action == ActionIds.ConflictEditorAllTheirs)
        {
            return ConflictFlow.Show(session, editor.PickAllUnresolved(ConflictPick.Theirs));
        }

        if (action == ActionIds.ConflictEditorUntake)
        {
            return Untake(session, editor);
        }

        if (action == ActionIds.ConflictEditorUndo)
        {
            return ConflictFlow.ShowCentered(session, editor.Undo());
        }

        return action == ActionIds.ConflictEditorRedo ? ConflictFlow.ShowCentered(session, editor.Redo()) : null;
    }

    // The pick key of a version pane copies that pane's version (or the Visual selection's lines); the named picks
    // (o, t, b, B) are bound in the version panes too, the palette and the chips run them from any pane.
    private static ConflictUpdate TakeKey(ConflictSession session, ConflictEditorState editor)
    {
        if (editor.IsVisual)
        {
            return TakeSelection(session, editor);
        }

        if (!editor.IsVersionPane)
        {
            return ConflictFlow.Warn(session, ConflictNotices.NotInVersion);
        }

        return Take(session, editor, PickOf(editor.ActivePane));
    }

    private static ConflictUpdate Take(ConflictSession session, ConflictEditorState editor, ConflictPick pick)
    {
        if (pick == ConflictPick.Base && !editor.CanTake(pick))
        {
            return ConflictFlow.Warn(session, ConflictNotices.NoBase);
        }

        return ConflictFlow.Show(session, editor.Take(pick));
    }

    private static ConflictUpdate TakeSelection(ConflictSession session, ConflictEditorState editor)
    {
        var taken = ConflictFlow.Show(session, editor.TakeSelectedLines());

        return editor.SelectedLines().Count == 0
            ? ConflictFlow.Then(ConflictFlow.Warn(session, ConflictNotices.NoLines), taken)
            : taken;
    }

    // The inverse of the take key: the whole conflict goes back to unresolved, in a Visual selection only the selected
    // lines leave the result.
    private static ConflictUpdate Untake(ConflictSession session, ConflictEditorState editor)
    {
        if (editor.IsVisual)
        {
            var untaken = ConflictFlow.Show(session, editor.UntakeSelectedLines());

            return editor.UntakeableLineCount == 0
                ? ConflictFlow.Then(ConflictFlow.Warn(session, ConflictNotices.NoLinesToUntake), untaken)
                : untaken;
        }

        if (!editor.CanUntake)
        {
            return ConflictFlow.Warn(session, ConflictNotices.NothingToUntake);
        }

        return ConflictFlow.Show(session, editor.Untake());
    }

    private static ConflictUpdate? Result(ConflictSession session, ConflictEditorState editor, ActionId action)
    {
        if (action == ActionIds.ConflictEditorEdit)
        {
            return BeginEdit(session, editor);
        }

        return ResultCommands.TryGetValue(action, out var command)
            ? ConflictFlow.Show(session, editor.ApplyResultCommand(command))
            : null;
    }

    private static ConflictUpdate BeginEdit(ConflictSession session, ConflictEditorState editor)
    {
        var edited = editor.BeginEdit();
        if (!edited.IsEditing)
        {
            return ConflictFlow.Done(session);
        }

        var text = edited.Hunks.Count == 0
            ? ConflictNotices.TypingInResult
            : $"Typing in the result at conflict {edited.Index + 1}: Esc or Ctrl-g keeps the text";

        return ConflictFlow.Show(session, edited, ConflictFlow.Post(text));
    }

    private static ConflictPick PickOf(ConflictPane pane) => pane switch
    {
        ConflictPane.Base => ConflictPick.Base,
        ConflictPane.Ours => ConflictPick.Ours,
        _ => ConflictPick.Theirs,
    };
}
