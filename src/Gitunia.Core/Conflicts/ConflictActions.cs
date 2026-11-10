using Gitunia.Core.Clipboard;
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
            ?? Clipboard(session, editor, action)
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

        if (action == ActionIds.ConflictEditorCopySelection)
        {
            return CopySelection(session, editor, isCut: false);
        }

        if (action == ActionIds.ConflictEditorCutSelection)
        {
            return CopySelection(session, editor, isCut: true);
        }

        return isDone ? ConflictFlow.Show(session, editor.EndEdit()) : null;
    }

    // Copy and cut of the text selected in the result, in Insert and (copy) Normal mode.
    private static ConflictUpdate CopySelection(ConflictSession session, ConflictEditorState editor, bool isCut)
    {
        if (editor.YankResultSelection() is not { } yank)
        {
            return ConflictFlow.Warn(session, ConflictNotices.NothingSelected);
        }

        var copied = editor.Yanked(yank);
        if (!isCut && !editor.IsEditing)
        {
            // Outside typing a copy ends the selection, as in vim, so a later put or paste does not replace it.
            copied.Result.Editor.ClearSelection();
        }

        var effect = new CopyToClipboardEffect(yank.Text, ConflictNotices.SelectionDescription);

        return ConflictFlow.Show(session, isCut ? copied.CutSelection() : copied, effect);
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

        if (action == ActionIds.ConflictEditorPrevious)
        {
            return ConflictFlow.ShowCentered(session, editor.PreviousConflict());
        }

        return null;
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

        return null;
    }

    // Copy (yy, Visual y, Alt-w) and put (p, P): the text goes to the terminal clipboard and is kept for the puts.
    private static ConflictUpdate? Clipboard(ConflictSession session, ConflictEditorState editor, ActionId action)
    {
        if (action == ActionIds.VisualCopy)
        {
            return CopyLines(session, editor, editor.YankSelectedRows());
        }

        if (action == ActionIds.ConflictEditorYankLine)
        {
            return CopyLines(session, editor, editor.YankLine());
        }

        if (action == ActionIds.ConflictEditorCopySelection)
        {
            return CopySelection(session, editor, isCut: false);
        }

        if (action == ActionIds.ConflictEditorPutAfter || action == ActionIds.ConflictEditorPutBefore)
        {
            return Put(session, editor, action == ActionIds.ConflictEditorPutAfter);
        }

        return null;
    }

    private static ConflictUpdate CopyLines(ConflictSession session, ConflictEditorState editor, ConflictYank? yank)
    {
        if (yank is null)
        {
            return ConflictFlow.Show(session, editor.EndVisual(), ConflictFlow.Warning(ConflictNotices.NothingToCopy));
        }

        var lines = yank.Text.Split('\n').Length;
        var description = lines == 1 ? ConflictNotices.LineDescription : $"{lines} lines";

        return ConflictFlow.Show(session, editor.Yanked(yank), new CopyToClipboardEffect(yank.Text, description));
    }

    private static ConflictUpdate Put(ConflictSession session, ConflictEditorState editor, bool after)
    {
        if (editor.IsVersionPane)
        {
            return ConflictFlow.Warn(session, ConflictNotices.PutInResult);
        }

        if (editor.Yank is null)
        {
            return ConflictFlow.Warn(session, ConflictNotices.NothingToPut);
        }

        return ConflictFlow.Show(session, editor.Put(after));
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

        if (editor.SelectedLines().Count == 0)
        {
            return ConflictFlow.Then(ConflictFlow.Warn(session, ConflictNotices.NoLines), taken);
        }

        return taken;
    }

    // The inverse of the take key: the whole conflict goes back to unresolved, in a Visual selection only the selected
    // lines leave the result.
    private static ConflictUpdate Untake(ConflictSession session, ConflictEditorState editor)
    {
        if (editor.IsVisual)
        {
            var untaken = ConflictFlow.Show(session, editor.UntakeSelectedLines());

            if (editor.UntakeableLineCount == 0)
            {
                return ConflictFlow.Then(ConflictFlow.Warn(session, ConflictNotices.NoLinesToUntake), untaken);
            }

            return untaken;
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

        if (ResultCommands.TryGetValue(action, out var command))
        {
            return ConflictFlow.Show(session, editor.ApplyResultCommand(command));
        }

        return null;
    }

    private static ConflictUpdate BeginEdit(ConflictSession session, ConflictEditorState editor)
    {
        var edited = editor.BeginEdit();
        if (!edited.IsEditing)
        {
            return ConflictFlow.Done(session);
        }

        var text = ConflictNotices.TypingInResult;
        if (edited.Hunks.Count > 0)
        {
            text = $"Typing in the result at conflict {edited.Index + 1}: Esc or Ctrl-g keeps the text";
        }

        return ConflictFlow.Show(session, edited, ConflictFlow.Post(text));
    }

    private static ConflictPick PickOf(ConflictPane pane) => pane switch
    {
        ConflictPane.Base => ConflictPick.Base,
        ConflictPane.Ours => ConflictPick.Ours,
        _ => ConflictPick.Theirs,
    };
}
