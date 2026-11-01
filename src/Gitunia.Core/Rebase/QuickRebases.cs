using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Prompts;
using Gitunia.Git.Rebase;

namespace Gitunia.Core.Rebase;

/// <summary>
/// Fixup, reword and drop of the commit selected in the graph (ux-workflows.md, workflow 3, "Quick rebase"), run
/// through the interactive rebase engine with a todo built by the writer. Each goes through a confirm that names the
/// commit, how many later commits get new hashes, the force-push warning and the undo; a reword first asks for the new
/// subject in a prompt. The refusals are the rebase's and none of them runs git. What the action acts on is fixed when
/// it is asked, and the question is dropped when the tab, an overlay or HEAD changed meanwhile.
/// </summary>
internal static class QuickRebases
{
    private const string SelectNotice = "Select a commit in the graph first";
    private const string EmptySubjectNotice = "The subject is empty: type one or Esc to cancel";
    private const string UnchangedSubjectNotice = "The subject is unchanged: edit it or Esc to cancel";

    public static ImmutableArray<AppEffect> Request(QuickRebaseRequested request, RebaseContext context)
    {
        if (request.CommitHash is not { } hash)
        {
            return [RebaseReducer.Post(SelectNotice)];
        }

        if (context.IsBusy)
        {
            return [RebaseReducer.Post(RebaseReducer.BusyNotice)];
        }

        var tab = context.Tab;
        if (RebaseGuard.Refusal(tab, null) is { } refusal)
        {
            return [RebaseReducer.Post(refusal)];
        }

        return [new PlanQuickRebaseEffect(tab.Id, tab.Location, request.Kind, hash)];
    }

    // The plan is a few git calls, so the question opens when it is answered; the state is checked again then. The
    // plan's own running mark is still set here, so the busy flag is not asked.
    public static ImmutableArray<AppEffect> Planned(QuickRebasePlan plan, RebaseContext context)
    {
        if (!StillValid(plan, context, true))
        {
            return [RebaseReducer.Post(RebaseReducer.ChangedNotice)];
        }

        if (plan.Kind != QuickRebaseKind.Reword)
        {
            return Ask(plan, null, context);
        }

        if (!context.IsActive)
        {
            return [RebaseReducer.Post(RebaseReducer.ChangedNotice)];
        }

        return [SubjectPrompt(plan, plan.Subject, context)];
    }

    public static ImmutableArray<AppEffect> SubjectAccepted(QuickRebaseSubjectAccepted accepted, RebaseContext context)
    {
        var subject = accepted.Text.Trim();
        if (subject.Length == 0)
        {
            return [RebaseReducer.Post(EmptySubjectNotice), SubjectPrompt(accepted.Plan, accepted.Text, context)];
        }

        if (string.Equals(subject, accepted.Plan.Subject, StringComparison.Ordinal))
        {
            var notice = RebaseReducer.Post(UnchangedSubjectNotice);

            return [notice, SubjectPrompt(accepted.Plan, accepted.Text, context)];
        }

        if (!StillValid(accepted.Plan, context, false))
        {
            return [RebaseReducer.Post(RebaseReducer.ChangedNotice)];
        }

        return Ask(accepted.Plan, subject, context);
    }

    // The dialog can outlive the state it was opened in.
    public static ImmutableArray<AppEffect> Confirmed(QuickRebaseTarget target, RebaseContext context)
    {
        var tab = context.Tab;
        if (context.IsBusy)
        {
            return [RebaseReducer.Post($"{RebaseReducer.BusyNotice}; nothing was rebased")];
        }

        if (RebaseGuard.Refusal(tab, null) is { } refusal)
        {
            return [RebaseReducer.Post(refusal)];
        }

        if (!string.Equals(tab.Head?.CommitHash, target.Plan.Head, StringComparison.Ordinal))
        {
            return [RebaseReducer.Post(RebaseReducer.ChangedNotice)];
        }

        return [new RunQuickRebaseEffect(tab.Id, tab.Location, target)];
    }

    private static ShowPromptEffect SubjectPrompt(QuickRebasePlan plan, string text, RebaseContext context)
    {
        var request = new PromptRequest(
            "Reword commit",
            QuickRebaseTexts.SubjectContext(plan),
            "next",
            new QuickRebaseSubjectAnswer(context.Tab.Id, plan),
            text,
            Owner: context.Tab.Id);

        return new ShowPromptEffect(request);
    }

    private static ImmutableArray<AppEffect> Ask(QuickRebasePlan plan, string? newSubject, RebaseContext context)
    {
        var tab = context.Tab;
        var target = new QuickRebaseTarget(tab.Id, plan, tab.Head!.BranchName!, newSubject);
        var current = RebaseReducer.CurrentBranch(tab.Refs);
        var confirm = QuickRebaseTexts.Confirm(target, current, RebaseGuard.AutoStashNote(tab));
        var accepted = new QuickRebaseConfirmed(tab.Id, target);

        return [new ShowConfirmEffect(confirm, accepted, RebaseReducer.TooSmallNotice)];
    }

    private static bool StillValid(QuickRebasePlan plan, RebaseContext context, bool checkOverlay)
    {
        var tab = context.Tab;

        return RebaseGuard.Refusal(tab, null) is null
            && string.Equals(tab.Head?.CommitHash, plan.Head, StringComparison.Ordinal)
            && !(checkOverlay && context.IsOverlayOpen);
    }
}
