using Gitunia.Core.Keymap;
using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Prompts;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Branches;

/// <summary>The upstream and fast-forward actions of <see cref="BranchReducer"/>.</summary>
internal static class BranchUpstreams
{
    public static ImmutableArray<AppEffect> SetRequested(BranchTarget target, BranchContext context)
    {
        if (context.IsBusy)
        {
            return [BranchReducer.Post(BranchRefusals.BusyNotice)];
        }

        if (BranchResolver.ResolveLocal(target, context.Refs, out var branch) is { } refusal)
        {
            return [BranchReducer.Post(refusal)];
        }

        var remotes = SwitchCandidates.BuildRemote(context.Refs, string.Empty);
        if (remotes.Count == 0)
        {
            return [BranchReducer.Post("There is no remote branch to track: add or fetch a remote first")];
        }

        var request = new PromptRequest(
            "Set upstream",
            $"Upstream of {branch!.Name}",
            "set",
            new BranchUpstreamAnswer(context.TabId, branch.Name),
            Choices: [.. remotes.Select(candidate => new PromptChoice(candidate.Target, candidate.Label))],
            NoMatch: "No matching remote branch",
            Owner: context.TabId);

        return [new ShowPromptEffect(request)];
    }

    public static ImmutableArray<AppEffect> Chosen(BranchUpstreamChosen chosen, BranchContext context)
    {
        if (context.IsBusy)
        {
            return [BranchReducer.Post(BranchRefusals.BusyNotice)];
        }

        return [new SetUpstreamEffect(context.TabId, context.Location, chosen.Branch, chosen.Upstream)];
    }

    public static ImmutableArray<AppEffect> Unset(BranchTarget target, BranchContext context)
    {
        if (context.IsBusy)
        {
            return [BranchReducer.Post(BranchRefusals.BusyNotice)];
        }

        if (BranchResolver.ResolveLocal(target, context.Refs, out var branch) is { } refusal)
        {
            return [BranchReducer.Post(refusal)];
        }

        if (branch!.UpstreamFullName is not { } upstream)
        {
            return [BranchReducer.Post($"{branch.Name} has no upstream")];
        }

        var shown = BranchResolver.ShownUpstream(upstream);

        return [new UnsetUpstreamEffect(context.TabId, context.Location, branch.Name, shown)];
    }

    // Moves a local branch that is not checked out to its upstream as last fetched; git is the backstop for
    // the same refusals the refs already tell.
    public static ImmutableArray<AppEffect> FastForward(BranchTarget target, BranchContext context)
    {
        if (context.IsBusy)
        {
            return [BranchReducer.Post(BranchRefusals.BusyNotice)];
        }

        if (BranchResolver.ResolveLocal(target, context.Refs, out var found) is { } refusal)
        {
            return [BranchReducer.Post(refusal)];
        }

        var branch = found!;
        if (FastForwardRefusal(branch, context) is { } blocked)
        {
            return [BranchReducer.Post(blocked)];
        }

        var upstream = branch.UpstreamFullName!;
        var newHash = context.Refs.First(reference => reference.FullName == upstream).CommitHash;
        var effect = new FastForwardBranchEffect(
            context.TabId,
            context.Location,
            branch.Name,
            upstream,
            branch.CommitHash,
            newHash);

        return [effect];
    }

    private static string? FastForwardRefusal(GitRef branch, BranchContext context)
    {
        var name = branch.Name;
        if (branch.IsCurrent)
        {
            return $"{name} is checked out: {KeyTokens.Of(ActionIds.RemotePull, "pull it")} instead";
        }

        if (branch.CheckedOutIn is { } path)
        {
            return $"{name} is checked out in {path}";
        }

        if (branch.UpstreamFullName is not { } upstream)
        {
            return $"{name} has no upstream";
        }

        var upstreamRef = context.Refs.FirstOrDefault(reference => reference.FullName == upstream);
        if (branch.IsUpstreamGone || upstreamRef is null)
        {
            return $"The upstream of {name} is gone: nothing changed";
        }

        if (upstreamRef.CommitHash == branch.CommitHash)
        {
            return $"{name} is up to date with its upstream";
        }

        return branch.Ahead is > 0 ? $"{name} has commits its upstream lacks (diverged): nothing changed" : null;
    }
}
