using System.Collections.Immutable;
using Gitunia.Core.Editing;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Persistence;
using Gitunia.Core.Shell;
using Gitunia.Core.Staging;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Core.Commits;

/// <summary>
/// The commit part of the Changes view (ux-workflows.md, workflow 2, "Message and commit") as pure transitions
/// <c>(command, context) -> (state, effects)</c>: the draft of the tab (message, options, amend), the message box's
/// Normal and
/// Insert behaviour, and the commit itself. A refused commit says why without running git, a failed one keeps the
/// draft and
/// shows the output of git and the hooks, a successful one clears the draft, reads the status again and returns the
/// focus to
/// the lists. The keys of the box's Normal mode are in <see cref="CommitMessageKeys"/>.
/// </summary>
public static class CommitReducer
{
    /// <summary>Applies a command.</summary>
    /// <param name="command">What happened.</param>
    /// <param name="context">The tab the command is for and the facts around it.</param>
    public static CommitUpdate Reduce(CommitCommand command, CommitContext context) => command switch
    {
        CommitActionPressed pressed => Press(pressed.Action, context),
        CommitAfterStaging after => AfterStaging(after.Request, context),
        CommitCursorPlaced placed => Place(placed, context),
        CommitDraftRestored restored => Restore(restored.Saved, context),
        CommitFailed failed => Failed(failed, context),
        CommitKeyTyped typed => TypeKey(typed.Key, context),
        CommitStageAllConfirmed confirmed => StageAllConfirmed(confirmed, context),
        CommitSucceeded succeeded => Succeeded(succeeded, context),
        HeadMessageRead read => HeadRead(read, context),
        HeadMessageReadFailed failed => HeadFailed(failed, context),
        MergeMessageRead read => MergeRead(read.Message, context),
        _ => throw new UnhandledCommandException(command),
    };

    /// <summary>The unchanged state with these effects.</summary>
    /// <param name="context">The facts.</param>
    /// <param name="effects">The effects.</param>
    internal static CommitUpdate Same(CommitContext context, params AppEffect[] effects) =>
        new(context.Tab.Commit, null, null, [.. effects], []);

    internal static CommitUpdate Changed(CommitContext context, CommitDraft draft, params AppEffect[] effects) =>
        new(context.Tab.Commit with { Draft = draft }, null, null, [.. effects], []);

    internal static PostStatusEffect Post(string text, StatusSeverity severity = StatusSeverity.Info) =>
        new(text, severity);

    internal static CommitDraft DraftOf(CommitContext context) =>
        context.Tab.Commit.Draft?.Clone() ?? new CommitDraft();

    internal static CommitRequest Snapshot(CommitDraft draft) =>
        new(draft.Editor.Text, draft.SignOff, draft.NoVerify, draft.Amend);

    // The box starts Insert mode: it takes the focus and a merge in progress prefills an empty box with git's message.
    internal static CommitUpdate StartEditing(CommitContext context, CommitDraft draft, bool marksUndo = true)
    {
        var tab = context.Tab;
        if (marksUndo)
        {
            draft.Editor.MarkUndoPoint();
        }

        ImmutableArray<AppCommand> next = [];
        if (tab.View.FocusedPane != PaneKind.WorkingMessage)
        {
            next = [Focus(tab, PaneKind.WorkingMessage)];
        }

        ImmutableArray<AppEffect> effects = [];
        if (draft.Editor.IsBlank && tab.Operation?.Kind == RepositoryOperation.Merge)
        {
            effects = [new ReadMergeMessageEffect(tab.Id, tab.Location)];
        }

        var state = tab.Commit with { Draft = draft, IsInserting = true };

        return new CommitUpdate(state, KeyMode.Insert, null, effects, next);
    }

    private static TabCommand Focus(RepositoryState tab, PaneKind pane) =>
        new(tab.Id, new FocusPaneCommand(pane));

    private static CommitUpdate HeadFailed(HeadMessageReadFailed failed, CommitContext context)
    {
        var text = $"{CommitTexts.HeadMessageFailure}: {failed.Notice}";

        return Same(context, Post(text, StatusSeverity.Error));
    }

    private static CommitUpdate Press(ActionId action, CommitContext context)
    {
        var tab = context.Tab;
        var inBox = tab.View.FocusedPane == PaneKind.WorkingMessage;
        if ((action == ActionIds.Cancel || action == ActionIds.NavigateBack) && context.IsCommitting)
        {
            return Same(context, new CancelCommitEffect(tab.Id), Post(CommitTexts.CancellingNotice));
        }

        if (action == ActionIds.Cancel)
        {
            return StopEditing(context, inBox);
        }

        if (action == ActionIds.NavigateBack)
        {
            return inBox ? Next(context, Focus(tab, CommitRules.ListPaneOf(tab))) : Same(context);
        }

        if (!CommitRules.Handles(action))
        {
            return Same(context);
        }

        return Run(action, context);
    }

    private static CommitUpdate Next(CommitContext context, AppCommand next) =>
        new(context.Tab.Commit, null, null, [], [next]);

    private static CommitUpdate StopEditing(CommitContext context, bool inBox)
    {
        var tab = context.Tab;
        if (!inBox || !CommitRules.IsEditing(tab, context.IsActive, context.Mode))
        {
            return Same(context);
        }

        var draft = DraftOf(context);
        draft.Editor.DropUndoPointIfUnchanged();

        return new CommitUpdate(tab.Commit with { Draft = draft, IsInserting = false }, KeyMode.Normal, null, [], []);
    }

    private static CommitUpdate Run(ActionId action, CommitContext context)
    {
        if (context.IsCommitting && action != ActionIds.WorkingTreeMessageNewLine)
        {
            return Same(context, Post(CommitTexts.RunningNotice));
        }

        if (action == ActionIds.WorkingTreeWriteMessage || action == ActionIds.WorkingTreeEditMessage)
        {
            return StartEditing(context, DraftOf(context));
        }

        if (action == ActionIds.WorkingTreeMessageNewLine)
        {
            return NewLine(context);
        }

        if (CommitRules.IsNormalKey(action))
        {
            return CommitMessageKeys.Run(action, context);
        }

        if (action == ActionIds.WorkingTreeCommit)
        {
            return CommitNow(context);
        }

        var draft = DraftOf(context);
        if (action == ActionIds.WorkingTreeToggleSignOff)
        {
            draft.SignOff = !draft.SignOff;

            return Changed(context, draft, Post(CommitTexts.SignOff(draft.SignOff)));
        }

        if (action == ActionIds.WorkingTreeToggleNoVerify)
        {
            draft.NoVerify = !draft.NoVerify;

            return Changed(context, draft, Post(CommitTexts.NoVerify(draft.NoVerify)));
        }

        return ToggleAmend(context, draft);
    }

    // Enter in the box adds a line; Enter in another prompt's Insert mode is that prompt's accept, which the input
    // layer
    // routes before it gets here.
    private static CommitUpdate NewLine(CommitContext context)
    {
        if (context.IsCommitting || !CommitRules.IsEditing(context.Tab, context.IsActive, context.Mode))
        {
            return Same(context);
        }

        var draft = DraftOf(context);
        draft.Editor.Apply(new KeyEvent(KeyNames.Enter));

        return Changed(context, draft);
    }

    private static CommitUpdate TypeKey(KeyEvent key, CommitContext context)
    {
        if (context.IsCommitting
            || context.Tab.Commit.Draft is null
            || !CommitRules.IsEditing(context.Tab, context.IsActive, context.Mode))
        {
            return Same(context);
        }

        var draft = DraftOf(context);
        draft.Editor.Apply(key);

        return Changed(context, draft);
    }

    private static CommitUpdate Place(CommitCursorPlaced placed, CommitContext context)
    {
        if (context.IsCommitting || context.Tab.Commit.Draft is null)
        {
            return Same(context);
        }

        var draft = DraftOf(context);
        var line = Math.Clamp(placed.Row, 0, draft.Editor.Lines.Count - 1);
        draft.Editor.PlaceCursor(line, Math.Max(0, placed.Column));

        return Changed(context, draft);
    }

    private static CommitUpdate Restore(SessionDraft saved, CommitContext context)
    {
        var draft = DraftOf(context);
        draft.Editor.SetText(saved.Message);
        draft.SignOff = saved.SignOff;
        draft.NoVerify = saved.NoVerify;

        return Changed(context, draft);
    }

    private static CommitUpdate ToggleAmend(CommitContext context, CommitDraft draft)
    {
        var tab = context.Tab;
        if (draft.Amend)
        {
            draft.SetAmend(false);

            return Changed(context, draft, Post(CommitTexts.AmendOffNotice));
        }

        var operation = tab.Operation?.Kind ?? RepositoryOperation.None;
        if (CommitCheck.AmendRefusal(tab.WorkingTree, operation) is { } refusal)
        {
            return Same(context, Post(refusal, StatusSeverity.Warning));
        }

        draft.SetAmend(true);

        return Changed(
            context,
            draft,
            new ReadHeadMessageEffect(tab.Id, tab.Location, tab.Head?.CommitHash),
            Post(CommitTexts.AmendOnNotice));
    }

    private static CommitUpdate HeadRead(HeadMessageRead read, CommitContext context)
    {
        if (context.Tab.Commit.Draft is not { Amend: true })
        {
            return Same(context);
        }

        var draft = DraftOf(context);
        draft.ApplyHeadMessage(ShortHash.Of(read.Hash), read.Message);

        return Changed(context, draft);
    }

    private static CommitUpdate MergeRead(string message, CommitContext context)
    {
        var draft = DraftOf(context);
        draft.Prefill(message);

        return Changed(context, draft);
    }

    // Commit: refusals first (an empty message keeps the box focused so the user can type it), then the question about
    // staging everything when nothing is staged, then the commit itself.
    private static CommitUpdate CommitNow(CommitContext context)
    {
        var tab = context.Tab;
        var draft = DraftOf(context);
        var operation = tab.Operation?.Kind ?? RepositoryOperation.None;
        var refusal = CommitCheck.Refusal(draft, tab.WorkingTree, operation);
        if (context.IsBlocked && refusal is null or CommitCheck.NothingStaged)
        {
            return Same(context, Post(StagingReducer.BlockedNotice, StatusSeverity.Warning));
        }

        if (refusal == CommitCheck.NothingStaged && OffersStageAll(tab))
        {
            var confirm = new ShowConfirmEffect(
                CommitTexts.StageAll,
                new CommitStageAllConfirmed(tab.Id, Snapshot(draft)),
                CommitCheck.NothingStaged);

            return Same(context, confirm);
        }

        if (refusal is not null)
        {
            var warning = Post(refusal, StatusSeverity.Warning);
            if (refusal != CommitCheck.EmptyMessage || tab.View.FocusedPane == PaneKind.WorkingMessage)
            {
                return Same(context, warning);
            }

            var started = StartEditing(context, draft);

            return started with { Effects = [.. started.Effects, warning] };
        }

        return Start(context, Snapshot(draft));
    }

    // Nothing staged but unstaged changes exist: ask before staging them (the draft stays as it is on no).
    private static bool OffersStageAll(RepositoryState tab) =>
        tab.WorkingTree is { Staged.Count: 0 } tree
        && tree.Unstaged.Any(entry => entry.Kind != StatusEntryKind.Unmerged);

    // The message and options are those of the question: later edits cannot change what the confirmed commit does. The
    // staging reducer refuses when the tab is busy and posts why; the commit follows once the status shows the result.
    private static CommitUpdate StageAllConfirmed(CommitStageAllConfirmed confirmed, CommitContext context)
    {
        if (context.IsCommitting)
        {
            return Same(context);
        }

        var then = new CommitAfterStaging(confirmed.TabId, confirmed.Request);

        return Next(context, new StageAllThenRequested(confirmed.TabId, then));
    }

    private static CommitUpdate AfterStaging(CommitRequest request, CommitContext context)
    {
        if (context.IsCommitting)
        {
            return Same(context);
        }

        if (context.Tab.WorkingTree is { Staged.Count: 0 })
        {
            return Same(context, Post(CommitCheck.NothingStaged, StatusSeverity.Warning));
        }

        return Start(context, request);
    }

    private static CommitUpdate Start(CommitContext context, CommitRequest request)
    {
        var tab = context.Tab;
        if (context.IsBlocked)
        {
            return Same(context, Post(StagingReducer.BlockedNotice, StatusSeverity.Warning));
        }

        var needsForcePush = request.Amend && CommitCheck.NeedsForcePush(tab.WorkingTree);
        var upstream = needsForcePush ? tab.WorkingTree?.Snapshot.Branch.UpstreamName : null;

        return Same(context, new RunCommitEffect(tab.Id, tab.Location, request, upstream));
    }

    // The message and Amend are gone, SignOff and No-Verify stay; the focus goes back to a list (which ends Insert
    // mode).
    private static CommitUpdate Succeeded(CommitSucceeded succeeded, CommitContext context)
    {
        var tab = context.Tab;
        var draft = DraftOf(context);
        draft.Sent();
        ImmutableArray<AppCommand> next = [];
        if (tab.View.Mode == TabViewMode.WorkingTree && tab.View.FocusedPane == PaneKind.WorkingMessage)
        {
            next = [Focus(tab, CommitRules.ListPaneOf(tab))];
        }

        var message = CommitTexts.Success(succeeded.Request, succeeded.Result, succeeded.ForcePushUpstream);
        ImmutableArray<AppEffect> effects = [Post(message), new RefreshTabEffect(tab.Id)];

        return new CommitUpdate(tab.Commit with { Draft = draft }, null, null, effects, next);
    }

    // The draft stays. The output opens now when the tab is shown, else when it is (the root reducer settles that);
    // a hook may have changed files and a cancelled commit may have got as far as the index, so the status is read
    // again.
    private static CommitUpdate Failed(CommitFailed failed, CommitContext context)
    {
        var tab = context.Tab;
        ImmutableArray<AppEffect> effects = [Post(failed.Notice, StatusSeverity.Error), new RefreshTabEffect(tab.Id)];
        if (failed.Output is not { } output)
        {
            return new CommitUpdate(tab.Commit, null, null, effects, []);
        }

        if (context.IsActive)
        {
            var shown = new OutputShownCommand(CommitFailureReport.Title, CommitFailureReport.Heading, output);

            return new CommitUpdate(tab.Commit, null, shown, effects, []);
        }

        return new CommitUpdate(tab.Commit with { PendingFailure = output }, null, null, effects, []);
    }
}
