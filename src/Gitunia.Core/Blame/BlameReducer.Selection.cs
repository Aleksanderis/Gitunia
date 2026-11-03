using Gitunia.Core.Effects;
using Gitunia.Core.FileHistory;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Git.History.Models;

namespace Gitunia.Core.Blame;

public static partial class BlameReducer
{
    /// <summary>The warning when a selection is started in a file without lines.</summary>
    public const string NothingToSelectNotice = "Nothing to select";

    /// <summary>The warning for the line history of lines that cannot be told apart in the working tree file.</summary>
    public const string MixedLinesNotice =
        "These lines do not come from one commit, or are not blamed yet: open the blame at a commit to follow them";

    /// <summary>The selection that follows a pointer: a drag, a double click or a triple click on the code.</summary>
    private static BlameUpdate SelectText(BlameSession session, BlameTextSelected selected)
    {
        if (session.View is not { IsEditingSearch: false, LineCount: > 0 } view)
        {
            return Done(session);
        }

        var selection = new BlameSelection(
            VisualKind.Chars,
            Math.Clamp(selected.Anchor.Line, 0, view.LineCount - 1),
            selected.Anchor.Column,
            selected.Cursor.Column);

        return Select(session, view, selection, selected.Cursor.Line, selected.Top);
    }

    private static BlameUpdate SelectLines(BlameSession session, BlameLinesSelected selected)
    {
        if (session.View is not { IsEditingSearch: false, LineCount: > 0 } view)
        {
            return Done(session);
        }

        var selection = new BlameSelection(VisualKind.Lines, Math.Clamp(selected.Anchor, 0, view.LineCount - 1));

        return Select(session, view, selection, selected.Cursor, selected.Top);
    }

    private static BlameUpdate Select(
        BlameSession session,
        BlameState view,
        BlameSelection selection,
        int cursor,
        int top)
    {
        var moved = (view with { Selection = selection, PinnedTop = top }).MoveTo(cursor);

        return With(session, moved, ModeEffects(view, moved));
    }

    // The selection keys of the open view; null when the action is none of them.
    private static BlameUpdate? TrySelectionKey(BlameSession session, BlameState view, BlameActionPressed pressed)
    {
        var action = pressed.Action;
        if (action == ActionIds.VisualToggle)
        {
            return ToggleLines(session, view);
        }

        if (action == ActionIds.VisualToggleChars)
        {
            return ToggleChars(session, view);
        }

        if (view.Selection is null)
        {
            return null;
        }

        if (action == ActionIds.VisualCopy)
        {
            return Copy(session, view);
        }

        if (action == ActionIds.Cancel || action == ActionIds.NavigateBack || action == ActionIds.AppQuit)
        {
            return With(session, EndSelection(view), ModeEffects(view, EndSelection(view)));
        }

        if (action == ActionIds.VisualSelectWord || action == ActionIds.VisualSelectLine)
        {
            return SelectUnit(session, view, pressed, action == ActionIds.VisualSelectWord);
        }

        return VisualActions.IsMotion(action) ? MoveColumn(session, view, pressed) : null;
    }

    private static BlameUpdate ToggleLines(BlameSession session, BlameState view)
    {
        if (view.Selection is { Kind: VisualKind.Chars } chars)
        {
            // The row-wise key turns a character selection into whole lines, like vim.
            var lines = view with { Selection = new BlameSelection(VisualKind.Lines, chars.AnchorLine) };

            return With(session, lines);
        }

        if (view.Selection is not null)
        {
            return With(session, EndSelection(view), ModeEffects(view, EndSelection(view)));
        }

        return Start(session, view, new BlameSelection(VisualKind.Lines, view.Cursor));
    }

    private static BlameUpdate ToggleChars(BlameSession session, BlameState view)
    {
        if (view.Selection is { Kind: VisualKind.Chars })
        {
            return With(session, EndSelection(view), ModeEffects(view, EndSelection(view)));
        }

        if (view.Selection is { } lines)
        {
            return With(session, view with { Selection = lines with { Kind = VisualKind.Chars } });
        }

        return Start(session, view, new BlameSelection(VisualKind.Chars, view.Cursor));
    }

    private static BlameUpdate Start(BlameSession session, BlameState view, BlameSelection selection)
    {
        if (view.LineCount == 0)
        {
            return Warn(session, NothingToSelectNotice);
        }

        var started = view with { Selection = selection };

        return With(session, started, ModeEffects(view, started));
    }

    private static BlameUpdate MoveColumn(BlameSession session, BlameState view, BlameActionPressed pressed)
    {
        if (view.Selection is not { Kind: VisualKind.Chars } selection)
        {
            return Done(session);
        }

        var column = VisualActions.MoveColumn(
            pressed.Action,
            pressed.CursorText,
            selection.CursorColumn,
            pressed.Count ?? 1);

        return With(session, view with { Selection = selection with { CursorColumn = column } });
    }

    // The word or the text of the cursor line, as the character selection (iw, il).
    private static BlameUpdate SelectUnit(
        BlameSession session,
        BlameState view,
        BlameActionPressed pressed,
        bool isWord)
    {
        var current = view.Selection is { Kind: VisualKind.Chars } chars ? chars.CursorColumn : 0;
        var text = pressed.CursorText;
        var span = isWord ? TextMotions.WordAt(text, current) : new ColumnSpan(0, TextMotions.LineEnd(text));
        var selection = new BlameSelection(VisualKind.Chars, view.Cursor, span.First, span.Last);

        return With(session, view with { Selection = selection });
    }

    private static BlameUpdate Copy(BlameSession session, BlameState view)
    {
        if (view.Selection is not { } selection)
        {
            return Done(session);
        }

        var range = selection.RangeTo(view.Cursor);
        var effect = new CopyBlameSelectionEffect(
            session.Generation,
            range.First,
            range.Last,
            selection.TextTo(view.Cursor));
        var ended = EndSelection(view);

        return With(session, ended, [.. ModeEffects(view, ended), effect]);
    }

    /// <summary>
    /// The history to open for the line history key: of the selected lines, else of the cursor line. Lines of one
    /// commit start at that commit (whose numbering their original line numbers are in); lines of several commits use
    /// the file as shown, which is numbered exactly only for a committed revision. Null when nothing can be opened.
    /// </summary>
    /// <param name="view">The view before the key (still with its selection).</param>
    /// <param name="pressed">The key with the facts of the first selected line.</param>
    public static FileHistoryOpenRequested? BlameLineHistory(BlameState view, BlameActionPressed pressed)
    {
        if (!pressed.RangeIsOneCommit)
        {
            if (view.Revision is null)
            {
                return null;
            }

            var range = view.Selection?.RangeTo(view.Cursor) ?? new RowRange(view.Cursor, view.Cursor);

            return new FileHistoryOpenRequested(
                view.TabId,
                view.Path,
                HistoryRevisionOf(view),
                new LineRange(range.First + 1, range.Last + 1));
        }

        if (pressed.CursorCommit is not { } owner || pressed.CursorOwnPath is not { } ownPath)
        {
            return null;
        }

        // One-based here.
        var first = pressed.CursorOriginalLine + 1;
        var last = first + Math.Max(pressed.RangeLines, 1) - 1;

        return new FileHistoryOpenRequested(view.TabId, ownPath, owner, new LineRange(first, last));
    }

    /// <summary>The state with the selection dropped.</summary>
    /// <param name="view">The view.</param>
    public static BlameState EndSelection(BlameState view) => view with { Selection = null };

    // The key mode follows the selection: Visual while there is one.
    private static AppEffect[] ModeEffects(BlameState before, BlameState after)
    {
        if ((before.Selection is null) == (after.Selection is null))
        {
            return [];
        }

        return [new SetKeyModeEffect(after.Selection is null ? KeyMode.Normal : KeyMode.Visual)];
    }
}
