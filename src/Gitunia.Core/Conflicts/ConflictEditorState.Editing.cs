using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Conflicts;

public sealed partial record ConflictEditorState
{
    /// <summary>
    /// Moves into the result at the current conflict and starts Insert mode there (from a version pane), or starts
    /// Insert mode at the cursor (from the result). The typing session is one step of <see cref="Undo"/>.
    /// </summary>
    public ConflictEditorState BeginEdit()
    {
        if (IsEditing)
        {
            return this;
        }

        var focused = this;
        if (IsVersionPane)
        {
            focused = (this with { Anchor = null }).FocusPane(ConflictPane.Result);
            if (Hunks.Count > 0)
            {
                Result.Editor.SetCursor(Result.Regions[Index].Start, 0);
            }
        }

        return focused.WithUndoPoint() with { IsEditing = true };
    }

    /// <summary>Leaves Insert mode, keeping the text; a session that changed nothing is not an undo step.</summary>
    public ConflictEditorState EndEdit()
    {
        if (!IsEditing)
        {
            return this;
        }

        var undo = UndoStack ?? [];
        if (undo.Count > 0 && Result.Matches(undo[^1].Buffer))
        {
            undo = [.. undo.Take(undo.Count - 1)];
        }

        return (this with { IsEditing = false, UndoStack = undo, IsDiscardPending = false }).SyncedFromResult();
    }

    /// <summary>Types a key into the result (Insert mode); false in any other mode.</summary>
    public ConflictEditorState TypeKey(KeyEvent key)
    {
        if (!IsEditing)
        {
            return this;
        }

        var row = Result.Editor.CursorRow;
        Result.Editor.Apply(key);
        Result.Reconcile(row);

        return (this with { IsDiscardPending = false }).SyncedFromResult();
    }

    /// <summary>
    /// A Normal mode command of the result pane (motions, <c>x</c>, <c>dd</c>, <c>o</c>, <c>a</c> ...).
    /// </summary>
    public ConflictEditorState ApplyResultCommand(ConflictResultCommand command)
    {
        if (IsEditing || IsVersionPane)
        {
            return this;
        }

        var editor = Result.Editor;

        return command switch
        {
            ConflictResultCommand.Left => Moved(editor.MoveLeftInLine),
            ConflictResultCommand.Right => Moved(editor.MoveRightInLine),
            ConflictResultCommand.WordNext => Moved(editor.MoveWordForward),
            ConflictResultCommand.WordPrevious => Moved(editor.MoveWordBackward),
            ConflictResultCommand.LineStart => Moved(editor.MoveToLineStart),
            ConflictResultCommand.LineEnd => Moved(editor.MoveToLineEnd),
            ConflictResultCommand.DeleteChar => Changed(result => result.Editor.DeleteUnderCursor(), reconcile: true),
            ConflictResultCommand.DeleteLine => DeletedLine(),
            ConflictResultCommand.OpenLine => StartInserting(MessageEditorStart.OpenLine),
            ConflictResultCommand.Append => StartInserting(MessageEditorStart.Append),
            ConflictResultCommand.AppendAtEnd => StartInserting(MessageEditorStart.AppendAtEnd),
            _ => StartInserting(MessageEditorStart.InsertAtStart),
        };
    }

    // dd keeps the line for p, as vim's register does.
    private ConflictEditorState DeletedLine()
    {
        var deleted = new ConflictYank(Result.Editor.Lines[Result.Editor.CursorRow], true);

        return Changed(result => result.Editor.DeleteLine(), reconcile: true) with { Yank = deleted };
    }

    /// <summary>Takes back the latest change and puts the cursor on its conflict.</summary>
    public ConflictEditorState Undo()
    {
        if (UndoStack is not { Count: > 0 } undo || IsEditing)
        {
            return this;
        }

        var redo = Snapshot();
        Result.Restore(undo[^1].Buffer);

        return Restored(undo[^1]) with
        {
            UndoStack = [.. undo.Take(undo.Count - 1)],
            RedoStack = [.. RedoStack ?? [], redo],
        };
    }

    /// <summary>Brings back the latest undone change.</summary>
    public ConflictEditorState Redo()
    {
        if (RedoStack is not { Count: > 0 } redo || IsEditing)
        {
            return this;
        }

        var undo = Snapshot();
        Result.Restore(redo[^1].Buffer);

        return Restored(redo[^1]) with
        {
            RedoStack = [.. redo.Take(redo.Count - 1)],
            UndoStack = [.. UndoStack ?? [], undo],
        };
    }

    /// <summary>Remembers that the user asked to close a changed file; the next close really closes.</summary>
    public ConflictEditorState AskDiscard() => this with { IsDiscardPending = true };
}
