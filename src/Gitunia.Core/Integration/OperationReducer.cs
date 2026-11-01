using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Status;
using Gitunia.Git.Operations.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Integration;

/// <summary>
/// Continue, abort and skip for the merge, rebase, cherry-pick or revert that is stopped (ux-workflows.md, workflow 4),
/// as pure transitions. Continue is refused while conflicted files remain; abort and skip ask first and name what is
/// undone or dropped. What is acted on (the operation and its commit) travels in the commands as the user saw it, so a
/// refresh while the dialog is open never redirects it.
/// </summary>
public static class OperationReducer
{
    /// <summary>Shown when an action is asked for while another call of the tab runs.</summary>
    public const string BusyNotice = "Busy: another operation of this tab is still running";

    private const string TooSmallNotice = "The screen is too small to ask: nothing was changed";

    /// <summary>Applies a continue, abort or skip request or the answer of one of the dialogs.</summary>
    public static ImmutableArray<AppEffect> Reduce(IntegrationCommand command, IntegrationContext context) =>
        command switch
        {
            OperationAbortConfirmed confirmed => AbortConfirmed(confirmed, context),
            OperationRequested requested => Requested(requested, context),
            OperationSkipConfirmed confirmed => SkipConfirmed(confirmed, context),
            _ => throw new UnhandledCommandException(command),
        };

    private static PostStatusEffect Post(string text) => new(text, StatusSeverity.Warning);

    private static ImmutableArray<AppEffect> Requested(OperationRequested requested, IntegrationContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BusyNotice)];
        }

        if (context.Operation is not { } info)
        {
            return [Post(OperationTexts.NoOperationNotice)];
        }

        var branch = context.Head?.BranchName;

        return requested.Action switch
        {
            OperationAction.Continue => Continue(info, branch, context),
            OperationAction.Abort => AskAbort(info, branch, context),
            _ => AskSkip(info, context),
        };
    }

    private static ImmutableArray<AppEffect> Continue(OperationInfo info, string? branch, IntegrationContext context)
    {
        if (context.ConflictCount > 0)
        {
            return [Post(OperationTexts.StillConflicted(context.ConflictCount))];
        }

        return [new ContinueOperationEffect(context.TabId, context.Location, info, branch)];
    }

    private static ImmutableArray<AppEffect> AskAbort(OperationInfo info, string? branch, IntegrationContext context)
    {
        var oldTip = info.OriginalHead ?? context.Head?.CommitHash;
        var prompt = OperationTexts.AbortPrompt(info, branch, oldTip);

        var accepted = new OperationAbortConfirmed(context.TabId, info, branch);

        return [new ShowConfirmEffect(prompt, accepted, TooSmallNotice)];
    }

    private static ImmutableArray<AppEffect> AskSkip(OperationInfo info, IntegrationContext context)
    {
        if (info.Kind == RepositoryOperation.Merge)
        {
            return [Post(OperationTexts.CannotSkipMerge)];
        }

        var prompt = OperationTexts.SkipPrompt(info);

        return [new ShowConfirmEffect(prompt, new OperationSkipConfirmed(context.TabId, info), TooSmallNotice)];
    }

    private static ImmutableArray<AppEffect> AbortConfirmed(
        OperationAbortConfirmed confirmed,
        IntegrationContext context)
    {
        if (context.IsBusy)
        {
            return [Post($"{BusyNotice}; nothing was aborted")];
        }

        return [new AbortOperationEffect(context.TabId, context.Location, confirmed.Info, confirmed.Branch)];
    }

    // The dialog can outlive the state it was opened in: the tab may have started something else meanwhile.
    private static ImmutableArray<AppEffect> SkipConfirmed(
        OperationSkipConfirmed confirmed,
        IntegrationContext context)
    {
        if (context.IsBusy)
        {
            return [Post($"{BusyNotice}; nothing was skipped")];
        }

        var info = confirmed.Info;
        if (context.Operation is not { } current || current.Kind != info.Kind || current.Commit != info.Commit)
        {
            return [Post("The operation moved on while asking: nothing was skipped")];
        }

        return [new SkipOperationEffect(context.TabId, context.Location, info)];
    }
}
