using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Operations;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Git.Pick;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Pick;

/// <summary>
/// Cherry-picks (<c>gp</c>) or reverts (<c>gv</c>) the selected commit, the commits of a Visual range in the graph, or
/// the tip of the selected sidebar branch or tag, onto the current branch (ux-workflows.md, workflow 3, "Integrating"),
/// as pure transitions <c>(PickCommand, PickContext) -> PickUpdate</c>. A cherry-pick applies the range oldest first, a
/// revert newest first (git's own order for reverting a range). When a merge commit is among them, a one-key question
/// asks once which parent is the mainline. Nothing is lost, so there is no Confirm; the status line names the old tip
/// for the undo. A stop on conflicts is not a failure: the in-progress operation takes over. Refused while another
/// operation is in progress and on an unborn HEAD; a detached HEAD is allowed, as git allows it.
/// </summary>
public static class PickReducer
{
    /// <summary>Shown when a second operation is asked for while one runs.</summary>
    public const string BusyNotice = "Busy: another operation of this tab is still running";

    /// <summary>Shown when nothing pickable is selected.</summary>
    public const string SelectNotice = "Select a commit (graph) or a branch or tag (sidebar) first";

    private const string DetachedLabel = "the detached HEAD";

    /// <summary>Applies a command and returns the question that stays open and the effects to run.</summary>
    public static PickUpdate Reduce(PickCommand command, PickContext context) => command switch
    {
        PickCancelled => Same(context, []),
        PickCommitsRead read => Decide(read, context),
        PickFailed failed => Failed(failed.Text, context, true),
        PickFinished finished => Finished(finished, context),
        PickMainlineCancelled => Cancel(context),
        PickMainlineChosen chosen => Choose(chosen, context),
        PickReadFailed failed => Failed(failed.Text, context, false),
        PickRequested request => Begin(request, context),
        _ => throw new UnhandledCommandException(command),
    };

    private static PickUpdate Same(PickContext context, ImmutableArray<AppEffect> effects) =>
        new(context.Question, effects);

    private static PickUpdate Warn(PickContext context, string text) =>
        Same(context, [new PostStatusEffect(text, StatusSeverity.Warning)]);

    private static PickUpdate Begin(PickRequested request, PickContext context)
    {
        if (context.IsBusy)
        {
            return Warn(context, BusyNotice);
        }

        if (Refusal(context) is { } refusal)
        {
            return Warn(context, refusal);
        }

        var target = request.Target;
        if (target.Hashes.IsEmpty || target.RowKind is not (null or SidebarRowKind.LocalBranch
            or SidebarRowKind.RemoteBranch or SidebarRowKind.Tag))
        {
            return Warn(context, SelectNotice);
        }

        var hashes = InApplyOrder(request.PickKind, target.Hashes);

        return Same(context, [new ReadPickCommitsEffect(context.TabId, context.Location, request.PickKind, hashes)]);
    }

    // A merge commit among the commits needs a mainline first; everything else runs at once.
    private static PickUpdate Decide(PickCommitsRead read, PickContext context)
    {
        // The state may have changed while the commits were read.
        if (Refusal(context) is { } refusal)
        {
            return Warn(context, refusal);
        }

        if (!read.Commits.Any(commit => commit.IsMerge))
        {
            return Same(context, [Run(read.PickKind, read.Commits, null, context)]);
        }

        if (!context.IsActive)
        {
            return Warn(context, "The tab changed: nothing was done");
        }

        var mainlineCount = read.Commits.Where(commit => commit.IsMerge).Min(commit => commit.Parents.Count);
        var usable = read.Commits.Any(commit => !commit.IsMerge) ? 1 : mainlineCount;
        var question = new MainlineQuestion(context.TabId, read.PickKind, read.Commits, mainlineCount, usable);

        return new PickUpdate(question, [new SetKeyModeEffect(KeyMode.Insert)]);
    }

    private static PickUpdate Choose(PickMainlineChosen chosen, PickContext context)
    {
        if (context.Question is not { } question)
        {
            return Same(context, []);
        }

        if (chosen.Parent < 1 || chosen.Parent > question.MainlineCount)
        {
            return Warn(context, $"Choose a parent from 1 to {question.MainlineCount} (Esc cancels)");
        }

        if (chosen.Parent > question.UsableParents)
        {
            return Warn(context, "Parent 2 or higher only fits when every commit is a merge: press 1 or Esc");
        }

        var close = new PickUpdate(null, [new SetKeyModeEffect(KeyMode.Normal)]);
        if (Refusal(context) is { } refusal)
        {
            return close with { Effects = [.. close.Effects, Post(refusal)] };
        }

        if (context.IsBusy)
        {
            return close with { Effects = [.. close.Effects, Post(BusyNotice)] };
        }

        var run = Run(question.Kind, question.Commits, chosen.Parent, context);

        return close with { Effects = [.. close.Effects, run] };
    }

    private static PickUpdate Cancel(PickContext context) =>
        context.Question is null ? Same(context, []) : new PickUpdate(null, [new SetKeyModeEffect(KeyMode.Normal)]);

    private static PickUpdate Finished(PickFinished finished, PickContext context)
    {
        var report = PickTexts.Report(finished.Result, finished.PickKind, finished.Target, finished.What);

        return Same(context, [
            new PostStatusEffect(report.Text, report.Severity, report.Detail),
            new RefreshTabEffect(context.TabId),
        ]);
    }

    private static PickUpdate Failed(string text, PickContext context, bool refresh)
    {
        var post = new PostStatusEffect(text, StatusSeverity.Error);
        if (!refresh)
        {
            return Same(context, [post]);
        }

        return Same(context, [post, new RefreshTabEffect(context.TabId)]);
    }

    private static RunPickEffect Run(
        PickKind kind,
        ImmutableArray<PickCommit> commits,
        int? mainline,
        PickContext context) =>
        new(context.TabId, context.Location, kind, commits, mainline, TargetOf(context.Head));

    private static PostStatusEffect Post(string text) => new(text, StatusSeverity.Warning);

    private static string? Refusal(PickContext context)
    {
        if (context.Operation is { } operation)
        {
            var name = OperationBannerBuilder.NameOf(operation.Kind);

            return $"A {name} is in progress: finish or abort the {name} first (<space>gc continue, <space>ga abort)";
        }

        return context.Head?.Kind == HeadKind.Unborn ? "The branch has no commits yet: nothing to pick onto" : null;
    }

    private static string TargetOf(HeadState? head) =>
        head is { Kind: HeadKind.Branch, BranchName: { } branch } ? branch : DetachedLabel;

    // Cherry-picks apply oldest first; the selection arrives newest first, as the graph shows them.
    private static ImmutableArray<string> InApplyOrder(PickKind kind, ImmutableArray<string> hashes) =>
        kind == PickKind.CherryPick ? [.. hashes.Reverse()] : hashes;
}
