using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Reset;

namespace Gitunia.Core.Integration;

/// <summary>
/// Moves the current branch to a commit with a soft, mixed or hard reset (ux-workflows.md, workflow 3, "Reset"), as
/// pure transitions. A one-key prompt picks the mode; soft runs at once, mixed runs at once when nothing is staged, and
/// everything that loses work opens the Confirm dialog, which names the target, the files and the old tip for the undo.
/// Refused on a detached or unborn HEAD and while an operation (merge, rebase, ...) is in progress. The target travels
/// in the commands as the user saw it.
/// </summary>
public static class ResetReducer
{
    /// <summary>Applies the user's reset commands and the read of what the reset drops.</summary>
    public static ImmutableArray<AppEffect> Reduce(IntegrationCommand command, IntegrationContext context) =>
        command switch
        {
            ResetConfirmed confirmed => Confirmed(confirmed, context),
            ResetModeChosen chosen => Chosen(chosen, context),
            ResetPrepared prepared => Prepared(prepared, context),
            ResetRequested requested => Requested(requested, context),
            _ => throw new UnhandledCommandException(command),
        };

    private static PostStatusEffect Post(string text) => new(text, StatusSeverity.Warning);

    private static ImmutableArray<AppEffect> Requested(ResetRequested requested, IntegrationContext context)
    {
        if (requested.CommitHash is not { } hash)
        {
            return [Post(ResetTexts.SelectNotice)];
        }

        if (context.IsBusy)
        {
            return [Post(ResetTexts.BusyNotice)];
        }

        if (Refusal(context) is { } refusal)
        {
            return [Post(refusal)];
        }

        var branch = context.Head!.BranchName!;
        var request = new PromptRequest(
            "Reset",
            $"Move {branch} to {ShortHash.Of(hash)}",
            string.Empty,
            new ResetModeAnswer(context.TabId, hash, branch),
            Owner: context.TabId,
            KeyChoices: ResetTexts.ModeChoices);

        return [new ShowPromptEffect(request)];
    }

    private static ImmutableArray<AppEffect> Chosen(ResetModeChosen chosen, IntegrationContext context)
    {
        if (context.IsBusy)
        {
            return [Post(ResetTexts.BusyNotice)];
        }

        return [new PrepareResetEffect(context.TabId, context.Location, chosen.Target)];
    }

    private static ImmutableArray<AppEffect> Prepared(ResetPrepared prepared, IntegrationContext context)
    {
        // The state may have changed while the status was read.
        if (prepared.Operation != RepositoryOperation.None)
        {
            return [Post(ResetTexts.OperationRefusal(prepared.Operation))];
        }

        if (Refusal(context) is { } refusal)
        {
            return [Post(refusal)];
        }

        var target = prepared.Target;
        var oldHash = context.Head!.CommitHash!;
        if (target.Mode == ResetMode.Hard)
        {
            return [Ask(ResetTexts.HardPrompt(target, oldHash, prepared.Changes), target, context)];
        }

        if (target.Mode == ResetMode.Mixed && prepared.Changes.Any(entry => entry.IsStaged))
        {
            return [Ask(ResetTexts.MixedPrompt(target, oldHash, prepared.Changes), target, context)];
        }

        return [new ResetBranchEffect(context.TabId, context.Location, target)];
    }

    private static ImmutableArray<AppEffect> Confirmed(ResetConfirmed confirmed, IntegrationContext context)
    {
        if (context.IsBusy)
        {
            return [Post($"{ResetTexts.BusyNotice}; nothing was reset")];
        }

        return [new ResetBranchEffect(context.TabId, context.Location, confirmed.Target)];
    }

    private static ShowConfirmEffect Ask(
        ConfirmPrompt prompt,
        ResetTarget target,
        IntegrationContext context) =>
        new(prompt, new ResetConfirmed(context.TabId, target), ResetTexts.TooSmallNotice);

    // Why a reset must not start in this tab, or null.
    private static string? Refusal(IntegrationContext context)
    {
        if (context.Head is not { Kind: HeadKind.Branch, BranchName: not null })
        {
            if (context.Head?.Kind == HeadKind.Unborn)
            {
                return "The branch has no commits yet: nothing to reset";
            }

            return "HEAD is detached: switch to a branch first";
        }

        if (context.Operation is { } operation)
        {
            return ResetTexts.OperationRefusal(operation.Kind);
        }

        return null;
    }
}
