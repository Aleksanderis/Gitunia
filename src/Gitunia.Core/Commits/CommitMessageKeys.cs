using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Core.Commits;

/// <summary>The Normal mode of the message box (a vim subset). The keys only act while the box has the focus (the
/// palette
/// can run them from elsewhere).</summary>
internal static class CommitMessageKeys
{
    /// <summary>Applies a Normal mode key to a copy of the draft.</summary>
    /// <param name="action">One of the box's Normal mode actions.</param>
    /// <param name="context">The tab and the facts around it.</param>
    public static CommitUpdate Run(ActionId action, CommitContext context)
    {
        if (context.Tab.View.FocusedPane != PaneKind.WorkingMessage || context.Mode != KeyMode.Normal)
        {
            return CommitReducer.Same(context);
        }

        var draft = CommitReducer.DraftOf(context);
        var editor = draft.Editor;
        if (action == ActionIds.WorkingTreeMessageLeft)
        {
            editor.MoveLeftInLine();
        }
        else if (action == ActionIds.WorkingTreeMessageRight)
        {
            editor.MoveRightInLine();
        }
        else if (action == ActionIds.WorkingTreeMessageDown || action == ActionIds.WorkingTreeMessageUp)
        {
            editor.MoveLines(action == ActionIds.WorkingTreeMessageDown ? 1 : -1);
        }
        else if (action == ActionIds.WorkingTreeMessageWordNext)
        {
            editor.MoveWordForward();
        }
        else if (action == ActionIds.WorkingTreeMessageWordPrevious)
        {
            editor.MoveWordBackward();
        }
        else if (action == ActionIds.WorkingTreeMessageLineStart)
        {
            editor.MoveToLineStart();
        }
        else if (action == ActionIds.WorkingTreeMessageLineEnd)
        {
            editor.MoveToLineEnd();
        }
        else
        {
            return Change(action, context, draft);
        }

        return CommitReducer.Changed(context, draft);
    }

    // Keys that change the text or start Insert mode; one undo point per change (an Insert session is one change).
    private static CommitUpdate Change(ActionId action, CommitContext context, CommitDraft draft)
    {
        var editor = draft.Editor;
        if (action == ActionIds.WorkingTreeMessageUndo)
        {
            var undone = editor.Undo();

            return undone
                ? CommitReducer.Changed(context, draft)
                : CommitReducer.Same(context, CommitReducer.Post(CommitTexts.NothingToUndoNotice));
        }

        if (action == ActionIds.WorkingTreeMessageDeleteChar)
        {
            editor.MarkUndoPoint();
            editor.DeleteUnderCursor();
            editor.DropUndoPointIfUnchanged();

            return CommitReducer.Changed(context, draft);
        }

        if (action == ActionIds.WorkingTreeMessageDeleteLine)
        {
            editor.MarkUndoPoint();
            editor.DeleteLine();
            editor.DropUndoPointIfUnchanged();

            return CommitReducer.Changed(context, draft);
        }

        if (action == ActionIds.WorkingTreeMessageOpenLine)
        {
            editor.MarkUndoPoint();
            editor.OpenLineBelow();

            return CommitReducer.StartEditing(context, draft, marksUndo: false);
        }

        if (action == ActionIds.WorkingTreeMessageAppend)
        {
            editor.MoveRightInLine();
        }
        else if (action == ActionIds.WorkingTreeMessageAppendAtEnd)
        {
            editor.MoveToLineEnd();
        }
        else
        {
            editor.MoveToLineStart();
        }

        return CommitReducer.StartEditing(context, draft);
    }
}
