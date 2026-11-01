using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Prompts;
using Gitunia.Git.Refs;

namespace Gitunia.Core.Branches;

/// <summary>The switch actions of <see cref="BranchReducer"/>: check out the branch under the cursor or pick.</summary>
internal static class BranchSwitches
{
    private const string NoMatch = "No matching branch";

    // The branch under the cursor is checked out at once; a prompt opens only when the cursor names none, or
    // several (a graph commit with more than one branch label: the prompt lists just those).
    public static ImmutableArray<AppEffect> Request(BranchTarget target, BranchContext context)
    {
        var refusal = BranchRefusals.ForCheckout(context.IsBusy, context.Operation);
        if (refusal is not null)
        {
            return [BranchReducer.Post(refusal)];
        }

        if (BranchResolver.BranchUnderCursor(target, context.Refs) is { } label
            && SwitchCandidates.Build(context.Refs, null, string.Empty, new HashSet<string> { label }) is [var only])
        {
            return Checkout(only, context);
        }

        IReadOnlySet<string>? onlyBranches = null;
        if (target.RowKind is null && target.CommitHash is { } hash)
        {
            var here = SwitchCandidates.BranchesAt(context.Refs, hash);
            onlyBranches = here.Count > 1 ? here : null;
        }

        var commitHash = target.RowKind is null ? target.CommitHash : null;
        var candidates = SwitchCandidates.Build(context.Refs, commitHash, string.Empty, onlyBranches);
        var request = new PromptRequest(
            "Switch branch",
            "Branch to check out",
            "switch",
            new BranchSwitchAnswer(context.TabId),
            Choices: [.. candidates.Select(ChoiceOf)],
            NoMatch: NoMatch,
            Owner: context.TabId);

        return [new ShowPromptEffect(request)];
    }

    // The choice carries a full ref name for a branch and the bare hash for the detached entry, so the two never clash.
    public static ImmutableArray<AppEffect> Chosen(string choice, BranchContext context)
    {
        // An operation may have started while the prompt was open (the refresh updates it).
        var refusal = BranchRefusals.ForCheckout(context.IsBusy, context.Operation);
        if (refusal is not null)
        {
            return [BranchReducer.Post(refusal)];
        }

        var candidate = CandidateOf(choice, context);

        return candidate is null ? [BranchReducer.Post(NoMatch)] : Checkout(candidate, context);
    }

    private static SwitchCandidate? CandidateOf(string choice, BranchContext context)
    {
        if (choice.Length == 0)
        {
            return null;
        }

        var all = SwitchCandidates.Build(context.Refs, null, string.Empty);
        if (choice.StartsWith(GitRefPrefixes.Heads, StringComparison.Ordinal))
        {
            var name = choice[GitRefPrefixes.Heads.Length..];

            return all.FirstOrDefault(item => item.Kind == SwitchTargetKind.LocalBranch && item.Target == name);
        }

        if (choice.StartsWith(GitRefPrefixes.Remotes, StringComparison.Ordinal))
        {
            var name = choice[GitRefPrefixes.Remotes.Length..];

            return all.FirstOrDefault(item => item.Kind == SwitchTargetKind.RemoteBranch && item.Target == name);
        }

        return new SwitchCandidate(SwitchTargetKind.DetachedCommit, choice, choice, null, false);
    }

    private static ImmutableArray<AppEffect> Checkout(SwitchCandidate candidate, BranchContext context)
    {
        if (candidate.IsCurrent)
        {
            return [BranchReducer.Post($"Already on {candidate.Label}")];
        }

        if (candidate.CheckedOutIn is { } path)
        {
            return [BranchReducer.Post($"{candidate.Label} is checked out in {path}")];
        }

        var target = candidate.Target;
        var follow = BranchResolver.CommitOf(context.Refs, FollowRef(candidate));
        var effect = new SwitchBranchEffect(context.TabId, context.Location, candidate.Kind, target, follow);

        return [effect];
    }

    private static string FollowRef(SwitchCandidate candidate) => candidate.Kind switch
    {
        SwitchTargetKind.LocalBranch => $"{GitRefPrefixes.Heads}{candidate.Target}",
        SwitchTargetKind.RemoteBranch => $"{GitRefPrefixes.Remotes}{candidate.Target}",
        _ => candidate.Target,
    };

    private static PromptChoice ChoiceOf(SwitchCandidate candidate)
    {
        // A remote branch is checked out as a local branch of the same name without the remote prefix: say so up front.
        var text = candidate.Kind == SwitchTargetKind.RemoteBranch
            ? $"{candidate.Label} -> local {SwitchCandidates.LocalNameOf(candidate.Target)}"
            : candidate.Label;

        return new PromptChoice(FollowRef(candidate), text);
    }
}
