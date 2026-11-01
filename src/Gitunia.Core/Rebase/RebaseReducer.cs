using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Rebase;

/// <summary>
/// Rebases the current branch onto the selected commit, branch or tag (ux-workflows.md, workflow 3, "Integrating"), as
/// pure transitions <c>(RebaseCommand, RebaseContext) -> Effects</c>. A confirm names both sides, warns that a branch
/// with an upstream needs a force push afterwards and shows the undo (the old tip, kept in <c>ORIG_HEAD</c> and the
/// reflog). Refused without running git on a detached or unborn HEAD, while an operation is in progress and when
/// tracked files have changes. A rebase that stops on conflicts is not a failure: the operation banner and continue,
/// abort and skip of workflow 4 take over. Non-interactive only; the quick fixup, reword and drop are in
/// <see cref="QuickRebases"/>.
/// </summary>
public static class RebaseReducer
{
    /// <summary>Shown when a second operation is asked for while one runs.</summary>
    public const string BusyNotice = "Busy: another operation of this tab is still running";

    /// <summary>Shown when the state changed while a question was being prepared.</summary>
    public const string ChangedNotice = "The tab or the screen changed while asking: nothing was rebased";

    /// <summary>Shown when the confirm dialog does not fit the screen.</summary>
    public const string TooSmallNotice = "The screen is too small to ask: nothing was rebased";

    private const string SelectNotice = "Select a commit (graph) or a branch or tag (sidebar) to rebase onto";

    /// <summary>Applies a command and returns the effects to run.</summary>
    public static ImmutableArray<AppEffect> Reduce(RebaseCommand command, RebaseContext context) => command switch
    {
        QuickRebaseConfirmed confirmed => QuickRebases.Confirmed(confirmed.Target, context),
        QuickRebaseFinished finished => Finished(QuickRebaseTexts.Report(finished.Target, finished.Result), context),
        QuickRebasePlanFailed failed => [Post(failed.Text, StatusSeverity.Error)],
        QuickRebasePlanned planned => QuickRebases.Planned(planned.Plan, context),
        QuickRebaseRequested request => QuickRebases.Request(request, context),
        QuickRebaseSubjectAccepted accepted => QuickRebases.SubjectAccepted(accepted, context),
        RebaseCancelled => [],
        RebaseFailed failed => Failed(failed.Text, context),
        RebaseFinished finished => Finished(RebaseTexts.Report(finished.Target, finished.Result), context),
        RebaseMergesCounted counted => Ask(counted, context),
        RebaseOntoConfirmed confirmed => Confirmed(confirmed.Target, context),
        RebaseOntoRequested request => Request(request.Selection, context),
        _ => throw new UnhandledCommandException(command),
    };

    internal static PostStatusEffect Post(string text, StatusSeverity severity = StatusSeverity.Warning) =>
        new(text, severity);

    internal static GitRef? CurrentBranch(ImmutableArray<GitRef> refs) =>
        refs.FirstOrDefault(candidate => candidate.IsCurrent && candidate.Kind == RefKind.LocalBranch);

    private static ImmutableArray<AppEffect> Request(RebaseSelection chosen, RebaseContext context)
    {
        var isRebaseTarget = chosen.RowKind is null or SidebarRowKind.LocalBranch or SidebarRowKind.RemoteBranch
            or SidebarRowKind.Tag;
        if (!isRebaseTarget || chosen.CommitHash is not { } hash)
        {
            return [Post(SelectNotice)];
        }

        if (context.IsBusy)
        {
            return [Post(BusyNotice)];
        }

        var tab = context.Tab;
        if (RebaseGuard.Refusal(tab, hash) is { } refusal)
        {
            return [Post(refusal)];
        }

        var label = chosen.Name ?? ShortHash.Of(hash);
        var target = new RebaseTarget(tab.Id, hash, label, tab.Head!.BranchName!, tab.Head.CommitHash!);

        return [new CountRebaseMergesEffect(tab.Id, tab.Location, target)];
    }

    // The merge count is a git call, so the dialog opens when it is answered; the state is checked again then.
    private static ImmutableArray<AppEffect> Ask(RebaseMergesCounted counted, RebaseContext context)
    {
        var tab = context.Tab;
        var target = counted.Target;
        var moved = !string.Equals(tab.Head?.CommitHash, target.OldTip, StringComparison.Ordinal);
        if (RebaseGuard.Refusal(tab, target.CommitHash) is not null || moved || context.IsOverlayOpen)
        {
            return [Post(ChangedNotice)];
        }

        var confirm = RebaseTexts.Confirm(
            target,
            CurrentBranch(tab.Refs),
            counted.Merges,
            RebaseGuard.AutoStashNote(tab));

        return [new ShowConfirmEffect(confirm, new RebaseOntoConfirmed(tab.Id, target), TooSmallNotice)];
    }

    // The dialog can outlive the state it was opened in.
    private static ImmutableArray<AppEffect> Confirmed(RebaseTarget target, RebaseContext context)
    {
        var tab = context.Tab;
        if (context.IsBusy)
        {
            return [Post($"{BusyNotice}; nothing was rebased")];
        }

        if (RebaseGuard.Refusal(tab, target.CommitHash) is { } refusal)
        {
            return [Post(refusal)];
        }

        return [new RunRebaseEffect(tab.Id, tab.Location, target)];
    }

    private static ImmutableArray<AppEffect> Finished(PostedText report, RebaseContext context) =>
        [Post(report.Text, report.Severity), new RefreshTabEffect(context.Tab.Id)];

    private static ImmutableArray<AppEffect> Failed(string text, RebaseContext context) =>
        [Post(text, StatusSeverity.Error), new RefreshTabEffect(context.Tab.Id)];
}
