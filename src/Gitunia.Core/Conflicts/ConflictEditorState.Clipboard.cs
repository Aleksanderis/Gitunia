using Gitunia.Core.Editing;

namespace Gitunia.Core.Conflicts;

public sealed partial record ConflictEditorState
{
    /// <summary>
    /// The line under the cursor of the active pane as whole lines to copy (<c>yy</c>); null on a filler row of a
    /// version pane, which has no text.
    /// </summary>
    public ConflictYank? YankLine()
    {
        if (IsVersionPane)
        {
            return Alignment.TextAt(Row, ActivePane) is { } text ? new ConflictYank(text, true) : null;
        }

        return new ConflictYank(Result.Editor.Lines[Result.Editor.CursorRow], true);
    }

    /// <summary>
    /// The lines of the Visual selection of the active version pane (every selected row that has text, also outside
    /// the current conflict, unlike a take); null when none has.
    /// </summary>
    public ConflictYank? YankSelectedRows()
    {
        if (Anchor is null || !IsVersionPane)
        {
            return null;
        }

        var lines = new List<string>();
        for (var row = SelectionStart; row <= SelectionEnd; row++)
        {
            if (Alignment.TextAt(row, ActivePane) is { } text)
            {
                lines.Add(text);
            }
        }

        return lines.Count == 0 ? null : new ConflictYank(string.Join('\n', lines), true);
    }

    /// <summary>The text selected in the result (by mouse or Shift with a movement key); null when none.</summary>
    public ConflictYank? YankResultSelection() =>
        Result.Editor.HasSelection ? new ConflictYank(Result.Editor.SelectedText, false) : null;

    /// <summary>Remembers copied text for <see cref="Put"/>; ends a Visual selection.</summary>
    /// <param name="yank">The text.</param>
    public ConflictEditorState Yanked(ConflictYank yank) => EndVisual() with { Yank = yank };

    /// <summary>
    /// Deletes the selected text while typing (cut) and returns the state; the caller copies what was selected first.
    /// The deletion is part of the typing session's undo step.
    /// </summary>
    public ConflictEditorState CutSelection()
    {
        if (!IsEditing || !Result.Editor.HasSelection)
        {
            return this;
        }

        var row = Result.Editor.CursorRow;
        Result.Editor.DeleteSelection();
        Result.Reconcile(row);

        return (this with { IsDiscardPending = false }).SyncedFromResult();
    }

    /// <summary>
    /// <c>p</c> and <c>P</c> in the result's Normal mode: puts <see cref="Yank"/> back. Whole lines go below (or
    /// above) the cursor's line with the cursor on the first of them; text goes after (or before) the cursor
    /// character. One step of <see cref="Undo"/>; nothing changes without a yank.
    /// </summary>
    /// <param name="after">True for <c>p</c>, false for <c>P</c>.</param>
    public ConflictEditorState Put(bool after)
    {
        if (IsEditing || IsVersionPane || Yank is not { } yank)
        {
            return this;
        }

        return Changed(
            result =>
            {
                var editor = result.Editor;
                var row = editor.CursorRow;
                if (yank.IsLinewise)
                {
                    PutLines(editor, yank.Text, after);
                }
                else
                {
                    if (after)
                    {
                        editor.MoveRightInLine();
                    }

                    editor.Insert(yank.Text);
                }

                result.Reconcile(after && yank.IsLinewise ? row + 1 : row);
            });
    }

    /// <summary>
    /// A terminal paste while the result is in Normal mode: inserts the text at the cursor, as one step of
    /// <see cref="Undo"/> (in Insert mode a paste is typed like any text).
    /// </summary>
    /// <param name="text">The pasted text.</param>
    public ConflictEditorState Paste(string text)
    {
        if (IsEditing || IsVersionPane || text.Length == 0)
        {
            return this;
        }

        return Changed(result =>
        {
            var row = result.Editor.CursorRow;
            result.Editor.Insert(text);
            result.Reconcile(row);
        });
    }

    /// <summary>The far end of a mouse selection in the result follows the pointer.</summary>
    /// <param name="line">The result line.</param>
    /// <param name="column">The display column in the line.</param>
    public ConflictEditorState ExtendResultSelection(int line, int column)
    {
        if (IsVersionPane)
        {
            return this;
        }

        Result.Editor.ExtendTo(Math.Clamp(line, 0, ResultLineCount - 1), Math.Max(0, column));

        return SyncedFromResult();
    }

    private static void PutLines(MessageEditor editor, string text, bool after)
    {
        var row = editor.CursorRow;
        if (after)
        {
            editor.MoveToLineEnd();
            editor.Insert($"\n{text}");
            editor.SetCursor(row + 1, 0);
        }
        else
        {
            editor.MoveToLineStart();
            editor.Insert($"{text}\n");
            editor.SetCursor(row, 0);
        }
    }
}
