using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Lists;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git;
using Gitunia.Git.Rebase;

namespace Gitunia.Core.Rebase;

/// <summary>
/// The interactive rebase's todo editor (ux-workflows.md, workflow 3, "Interactive rebase") as pure transitions
/// <c>(session, command, context) -> (session, effects)</c>. <c>&lt;space&gt;bri</c> reads the commits the target does
/// not have and opens a modal list of them in replay order. Each commit gets an action (pick, squash, fixup, drop), commits move up and
/// down, a reword opens the multi-line message editor, and <c>Enter</c> asks for a Confirm that names what is kept and
/// dropped and the undo, then hands the todo to the engine. A stop on conflicts is the in-progress rebase of workflow 4.
/// While the editor is open it consumes every action; the Confirm opens on top of it, so declining keeps the todo.
/// </summary>
public static class RebaseEditorReducer
{
    /// <summary>Shown when the state changed while the todo was open or being read.</summary>
    public const string ChangedNotice = "The tab changed while the todo was open: nothing was rebased";

    /// <summary>Shown when the confirm dialog does not fit the screen.</summary>
    public const string TooSmallNotice = "The screen is too small to ask: nothing was rebased";

    /// <summary>Shown when the range holds no commit.</summary>
    public const string EmptyRangeNotice = "Nothing to rebase: the branch has no commits the target lacks";

    /// <summary>Shown when a reword is saved without a message.</summary>
    public const string BlankMessageNotice = "A reword needs a message: type one, or press Esc to cancel";

    /// <summary>How the whole history is named in messages.</summary>
    public const string RootLabel = "the root";

    private const string BusyNotice = "Busy: another operation of this tab is still running";
    private const string SelectNotice = "Select a commit (graph) or a branch or tag (sidebar) to rebase onto";

    /// <summary>Applies a command.</summary>
    /// <param name="session">The session before.</param>
    /// <param name="command">What happened.</param>
    /// <param name="context">The tab the command is for and the facts around it.</param>
    public static RebaseEditorUpdate Reduce(
        RebaseEditorSession session,
        RebaseEditorCommand command,
        RebaseEditorContext context) => command switch
    {
        RebaseEditorActionPressed pressed => Press(session, pressed, context),
        RebaseEditorFinished finished => Finished(session, finished),
        RebaseEditorKeyTyped typed => TypeKey(session, typed.Key, context),
        RebaseEditorMessageRead read => MessageRead(session, read),
        RebaseEditorNotStarted notStarted => NotStarted(session, notStarted, context),
        RebaseEditorOpenRequested open => Open(session, open, context),
        RebaseEditorRangeRead read => RangeRead(session, read, context),
        RebaseEditorReadCancelled => Done(session with { IsReading = false }),
        RebaseEditorReadFailed failed => Done(session with { IsReading = false }, Post(failed.Text, failed.Severity)),
        RebaseEditorRowClicked clicked => ClickRow(session, clicked.Index),
        RebaseEditorStartConfirmed confirmed => Start(session, confirmed.State, context),
        _ => throw new UnhandledCommandException(command),
    };

    /// <summary>The tab of the open editor was closed: it closes and leaves the message mode.</summary>
    /// <param name="session">The session before.</param>
    public static RebaseEditorUpdate Closed(RebaseEditorSession session) => Close(session);

    private static PostStatusEffect Post(string text, StatusSeverity severity = StatusSeverity.Warning) =>
        new(text, severity);

    private static RebaseEditorUpdate Done(RebaseEditorSession session, params AppEffect[] effects) =>
        new(session, [.. effects]);

    private static RebaseEditorUpdate With(RebaseEditorSession session, RebaseEditorState state) =>
        Done(session with { Editor = state });

    // Why a rebase onto the selection (or of the whole history) cannot start: the same refusals as the plain rebase.
    private static RebaseEditorUpdate Open(
        RebaseEditorSession session,
        RebaseEditorOpenRequested open,
        RebaseEditorContext context)
    {
        if (session.IsOpen || session.IsReading || context.IsOverlayOpen)
        {
            return Done(session);
        }

        var chosen = open.Selection;
        var isTarget = chosen.RowKind is null or SidebarRowKind.LocalBranch or SidebarRowKind.RemoteBranch
            or SidebarRowKind.Tag;
        if (!open.IsRoot && (!isTarget || chosen.CommitHash is null))
        {
            return Done(session, Post(SelectNotice));
        }

        var hash = open.IsRoot ? null : chosen.CommitHash;
        var tab = context.Tab;
        if (context.IsBusy)
        {
            return Done(session, Post(BusyNotice));
        }

        if (RebaseGuard.Refusal(tab, hash) is { } refusal)
        {
            return Done(session, Post(refusal));
        }

        var label = open.IsRoot ? RootLabel : chosen.Name ?? ShortHash.Of(hash!);
        var next = session with { Generation = session.Generation + 1, IsReading = true };
        var effect = new ReadRebaseRangeEffect(
            tab.Id,
            tab.Location,
            next.Generation,
            hash,
            label,
            tab.Head!.BranchName!,
            tab.Head.CommitHash!);

        return Done(next, effect);
    }

    // The range was read for the state of a moment ago.
    private static RebaseEditorUpdate RangeRead(
        RebaseEditorSession session,
        RebaseEditorRangeRead read,
        RebaseEditorContext context)
    {
        var ended = session with { IsReading = false };
        if (read.Generation != session.Generation)
        {
            return Done(ended);
        }

        var tab = context.Tab;
        if (context.IsOverlayOpen
            || !string.Equals(tab.Head?.CommitHash, read.State.OldTip, StringComparison.Ordinal)
            || RebaseGuard.Refusal(tab, read.TargetHash) is not null)
        {
            return Done(ended, Post(ChangedNotice));
        }

        if (read.State.Rows.Count == 0)
        {
            return Done(ended, Post(EmptyRangeNotice));
        }

        return With(ended, read.State);
    }

    private static RebaseEditorUpdate Press(
        RebaseEditorSession session,
        RebaseEditorActionPressed pressed,
        RebaseEditorContext context)
    {
        if (session.Editor is not { } state)
        {
            return Done(session);
        }

        return state.Edit is null
            ? InList(session, state, pressed, context)
            : InMessage(session, state, pressed.Action);
    }

    private static RebaseEditorUpdate InList(
        RebaseEditorSession session,
        RebaseEditorState state,
        RebaseEditorActionPressed pressed,
        RebaseEditorContext context)
    {
        var action = pressed.Action;
        var count = Math.Max(1, pressed.Count ?? 1);
        if (action == ActionIds.Cancel || action == ActionIds.NavigateBack || action == ActionIds.AppQuit)
        {
            return RequestClose(session, state);
        }

        if (action == ActionIds.RebaseEditorPick)
        {
            return With(session, state.SetAction(RebaseTodoAction.Pick));
        }

        if (action == ActionIds.RebaseEditorSquash)
        {
            return With(session, state.SetAction(RebaseTodoAction.Squash));
        }

        if (action == ActionIds.RebaseEditorFixup)
        {
            return With(session, state.SetAction(RebaseTodoAction.Fixup));
        }

        if (action == ActionIds.RebaseEditorDrop)
        {
            return With(session, state.SetAction(RebaseTodoAction.Drop));
        }

        if (action == ActionIds.RebaseEditorMoveUp)
        {
            return With(session, state.MoveRow(-count));
        }

        if (action == ActionIds.RebaseEditorMoveDown)
        {
            return With(session, state.MoveRow(count));
        }

        if (action == ActionIds.RebaseEditorReword)
        {
            return BeginReword(session, state, context);
        }

        if (action == ActionIds.RebaseEditorStart)
        {
            return Ask(session, state, context);
        }

        var moves = ListMotion.TryMove(
            action,
            pressed.Count,
            state.SelectedIndex,
            state.Rows.Count,
            pressed.PageRows,
            out var target);

        return moves ? With(session, state.MoveTo(target)) : Done(session);
    }

    private static RebaseEditorUpdate InMessage(RebaseEditorSession session, RebaseEditorState state, ActionId action)
    {
        if (action == ActionIds.RebaseEditorMessageSave)
        {
            if (state.Edit!.Editor.IsBlank)
            {
                return Done(session, Post(BlankMessageNotice));
            }

            return Done(session with { Editor = state.SaveEdit() }, new SetKeyModeEffect(KeyMode.Normal));
        }

        if (action == ActionIds.RebaseEditorMessageNewLine)
        {
            return With(session, state with { Edit = state.Edit!.Typed(new KeyEvent(KeyNames.Enter)) });
        }

        if (action == ActionIds.Cancel || action == ActionIds.NavigateBack)
        {
            return Done(session with { Editor = state.CancelEdit() }, new SetKeyModeEffect(KeyMode.Normal));
        }

        return Done(session);
    }

    private static RebaseEditorUpdate TypeKey(
        RebaseEditorSession session,
        KeyEvent key,
        RebaseEditorContext context)
    {
        if (session.Editor is not { Edit: { } edit } state || !context.IsActive)
        {
            return Done(session);
        }

        return With(session, state with { Edit = edit.Typed(key) });
    }

    private static RebaseEditorUpdate ClickRow(RebaseEditorSession session, int index) =>
        session.Editor is { Edit: null } state ? With(session, state.MoveTo(index)) : Done(session);

    // An edited todo is not thrown away by one stray Esc: the first press warns, the second closes.
    private static RebaseEditorUpdate RequestClose(RebaseEditorSession session, RebaseEditorState state)
    {
        if (state.IsModified && !state.IsDiscardPending)
        {
            return With(session, state.AskDiscard());
        }

        return Close(session);
    }

    private static RebaseEditorUpdate Close(RebaseEditorSession session)
    {
        var closed = session with { Editor = null, Generation = session.Generation + 1 };

        return session.IsEditingMessage ? Done(closed, new SetKeyModeEffect(KeyMode.Normal)) : Done(closed);
    }

    // The editor starts with the commit's full message, not only its subject, so the body is not lost by a reword.
    private static RebaseEditorUpdate BeginReword(
        RebaseEditorSession session,
        RebaseEditorState state,
        RebaseEditorContext context)
    {
        if (state.Selected is not { IsEditable: true } row || session.IsReading)
        {
            return Done(session);
        }

        if (row.Message is { } message)
        {
            return OpenMessage(session, state, message);
        }

        if (!ObjectId.TryParse(row.Hash, out var id))
        {
            return OpenMessage(session, state, row.Subject);
        }

        var next = session with { Generation = session.Generation + 1, IsReading = true };
        var effect = new ReadRebaseMessageEffect(
            context.Tab.Id,
            context.Tab.Location,
            next.Generation,
            row.Hash,
            id,
            row.Subject);

        return Done(next, effect);
    }

    private static RebaseEditorUpdate MessageRead(RebaseEditorSession session, RebaseEditorMessageRead read)
    {
        var ended = session with { IsReading = false };
        if (read.Generation != session.Generation
            || session.Editor is not { Edit: null } state
            || state.Selected?.Hash != read.Hash)
        {
            return Done(ended);
        }

        return OpenMessage(ended, state, read.Text);
    }

    private static RebaseEditorUpdate OpenMessage(
        RebaseEditorSession session,
        RebaseEditorState state,
        string message) =>
        Done(session with { Editor = state.BeginEdit(message) }, new SetKeyModeEffect(KeyMode.Insert));

    private static RebaseEditorUpdate Ask(
        RebaseEditorSession session,
        RebaseEditorState state,
        RebaseEditorContext context)
    {
        if (state.Problem is { } problem)
        {
            return Done(session, Post(problem));
        }

        if (state.Rows.Count == 0)
        {
            return Done(session);
        }

        var tab = context.Tab;
        var note = RebaseGuard.AutoStashNote(tab);
        var confirm = RebaseEditorTexts.ConfirmFor(state, RebaseReducer.CurrentBranch(tab.Refs), note);

        var accepted = new RebaseEditorStartConfirmed(tab.Id, state);

        return Done(session, new ShowConfirmEffect(confirm, accepted, TooSmallNotice));
    }

    // The Confirm outlives nothing but a moment, yet the tab can have changed under it.
    private static RebaseEditorUpdate Start(
        RebaseEditorSession session,
        RebaseEditorState state,
        RebaseEditorContext context)
    {
        var tab = context.Tab;
        if (context.IsBusy)
        {
            return Done(session, Post($"{BusyNotice}; nothing was rebased"));
        }

        var moved = !string.Equals(tab.Head?.CommitHash, state.OldTip, StringComparison.Ordinal);
        var refusal = RebaseGuard.Refusal(tab, state.Base.Onto);
        if (moved || refusal is not null)
        {
            return Done(session, Post(refusal ?? ChangedNotice));
        }

        var closed = Close(session);

        return new RebaseEditorUpdate(
            closed.Session,
            [.. closed.Effects, new RunInteractiveRebaseEffect(tab.Id, tab.Location, state)]);
    }

    private static RebaseEditorUpdate Finished(RebaseEditorSession session, RebaseEditorFinished finished)
    {
        var report = RebaseEditorTexts.ReportOf(finished.State, finished.Result);

        return Done(session, Post(report.Text, report.Severity), new RefreshTabEffect(finished.TabId));
    }

    // Nothing ran: the todo comes back so the work of editing it is not lost.
    private static RebaseEditorUpdate NotStarted(
        RebaseEditorSession session,
        RebaseEditorNotStarted notStarted,
        RebaseEditorContext context)
    {
        var post = Post(notStarted.Text, StatusSeverity.Error);
        if (session.IsOpen || context.IsOverlayOpen)
        {
            return Done(session, post);
        }

        return Done(session with { Editor = notStarted.State }, post);
    }
}
