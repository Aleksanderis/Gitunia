using Gitunia.Git.Conflicts.Models;

namespace Gitunia.Core.Conflicts;

public sealed partial record ConflictEditorState
{
    private static IReadOnlyList<string> VersionLines(ConflictHunk hunk, ConflictPick pick) => pick switch
    {
        ConflictPick.Ours => ConflictAlignment.Lines(hunk.Ours),
        ConflictPick.Theirs => ConflictAlignment.Lines(hunk.Theirs),
        ConflictPick.Base => ConflictAlignment.Lines(hunk.Base ?? string.Empty),
        _ => [.. ConflictAlignment.Lines(hunk.Ours), .. ConflictAlignment.Lines(hunk.Theirs)],
    };

    private ConflictEditorState OnVersionRow(int row)
    {
        var clamped = Math.Clamp(row, 0, Math.Max(0, Alignment.RowCount - 1));
        var hunk = Alignment.HunkAt(clamped);

        return this with
        {
            Row = clamped,
            SelectedIndex = hunk >= 0 ? hunk : SelectedIndex,
            IsDiscardPending = false,
        };
    }

    private ConflictEditorState SyncedFromResult()
    {
        var line = Result.Editor.CursorRow;
        var hunk = Result.RegionAt(line);

        return this with
        {
            Row = Alignment.RowOf(line, Result.Regions),
            SelectedIndex = hunk >= 0 ? hunk : SelectedIndex,
            IsDiscardPending = false,
        };
    }

    private ConflictEditorState MoveToLine(int line)
    {
        if (IsEditing)
        {
            return this;
        }

        if (ActivePane != ConflictPane.Result)
        {
            return OnVersionRow(line);
        }

        Result.Editor.SetCursor(line, 0);

        return SyncedFromResult();
    }

    private ConflictEditorState GoToConflict(int hunk)
    {
        if (ActivePane != ConflictPane.Result)
        {
            return OnVersionRow(Alignment.FirstRowOf(hunk));
        }

        Result.Editor.SetCursor(Result.Regions[hunk].Start, 0);

        return SyncedFromResult() with { SelectedIndex = hunk };
    }

    private ConflictEditorState Moved(Action move)
    {
        move();

        return SyncedFromResult();
    }

    private ConflictEditorState StartInserting(MessageEditorStart start)
    {
        var editing = WithUndoPoint();
        var editor = Result.Editor;
        switch (start)
        {
            case MessageEditorStart.OpenLine:
                var row = editor.CursorRow;
                editor.OpenLineBelow();
                Result.Reconcile(row + 1);
                break;
            case MessageEditorStart.Append:
                editor.MoveRightInLine();
                break;
            case MessageEditorStart.AppendAtEnd:
                editor.MoveToLineEnd();
                break;
            default:
                editor.MoveToLineStart();
                break;
        }

        return (editing with { IsEditing = true }).SyncedFromResult();
    }

    private ConflictEditorState WithUndoPoint() =>
        this with { UndoStack = Pushed(), RedoStack = [], IsDiscardPending = false };

    private List<ConflictSnapshot> Pushed() => [.. (UndoStack ?? []).Append(Snapshot()).TakeLast(UndoLimit)];

    private ConflictSnapshot Snapshot() => new(Result.Capture(), Index);

    // Runs a change on the result as one undo step; a change that leaves the text and regions as they were is none.
    private ConflictEditorState Changed(Action<ConflictResultBuffer> change, bool reconcile = false)
    {
        var before = Snapshot();
        var row = Result.Editor.CursorRow;
        change(Result);
        if (reconcile)
        {
            Result.Reconcile(row);
        }

        var after = ActivePane == ConflictPane.Result ? SyncedFromResult() : this;
        if (Result.Matches(before.Buffer))
        {
            return after;
        }

        var undo = (UndoStack ?? []).Append(before).TakeLast(UndoLimit).ToList();

        return after with { UndoStack = undo, RedoStack = [], IsDiscardPending = false };
    }

    private ConflictEditorState Restored(ConflictSnapshot snapshot)
    {
        var restored = this with { SelectedIndex = snapshot.SelectedIndex, Anchor = null, IsDiscardPending = false };
        if (ActivePane == ConflictPane.Result)
        {
            return restored.SyncedFromResult();
        }

        return Hunks.Count == 0 ? restored : restored.OnVersionRow(Alignment.FirstRowOf(snapshot.SelectedIndex));
    }

    private enum MessageEditorStart
    {
        OpenLine,
        Append,
        AppendAtEnd,
        InsertAtStart,
    }
}
