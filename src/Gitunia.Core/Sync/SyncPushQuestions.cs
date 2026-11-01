using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Text;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Remotes.Models;

namespace Gitunia.Core.Sync;

/// <summary>
/// The push cases that need a question (ux-workflows.md, workflow 5): the first push of a branch without an upstream asks
/// for the remote (<c>origin</c> first) and sets the upstream; a force push is only ever a lease and goes through a
/// confirm that names the remote branch it overwrites, offered by key or right after a plain push was rejected as a
/// non-fast-forward (#746). What an action acts on is fixed when its question opens.
/// </summary>
internal static class SyncPushQuestions
{
    private const string DefaultRemote = "origin";

    /// <summary>Reads the remotes for a question about a branch.</summary>
    public static ImmutableArray<AppEffect> ListRemotes(SyncRemotesPurpose purpose, string branch, SyncContext context) =>
        [new ListSyncRemotesEffect(context.TabId, context.Location, purpose, branch, null)];

    /// <summary>The force push key: refused without a branch that has an upstream, else reads the remotes.</summary>
    public static ImmutableArray<AppEffect> ForceRequested(GitRef? current, SyncContext context)
    {
        if (current is null)
        {
            return [SyncReducer.Post("No branch is checked out: nothing to force push")];
        }

        if (!SyncReducer.HasUpstream(current))
        {
            return [SyncReducer.Post($"{current.Name} has no upstream: push it first")];
        }

        return ListRemotes(SyncRemotesPurpose.Force, current.Name, context);
    }

    /// <summary>The remotes of a first push were read: one remote is used without asking, several are picked from.</summary>
    public static SyncUpdate FirstPushRemotes(SyncRemotesListed listed, SyncContext context)
    {
        var branch = listed.Branch ?? string.Empty;
        if (listed.Remotes is [var only])
        {
            // One remote: asking for it is a step without a choice.
            return StartFirstPush(branch, only, context);
        }

        var ordered = listed.Remotes.OrderByDescending(name => name == DefaultRemote).ToList();
        var request = new PromptRequest(
            "Push and set upstream",
            $"Remote to push {branch} to",
            "choose",
            new SyncRemoteAnswer(context.TabId, branch),
            Choices: [.. ordered.Select(name => new PromptChoice(name, name))],
            NoMatch: SyncReducer.NoMatch,
            Owner: context.TabId);

        return new SyncUpdate(context.State, [new ShowPromptEffect(request)]);
    }

    /// <summary>Starts the first push of a branch to a remote, which then becomes its upstream.</summary>
    public static SyncUpdate StartFirstPush(string branch, string remote, SyncContext context)
    {
        if (SyncReducer.CurrentBranch(context.Refs)?.Name != branch)
        {
            return new SyncUpdate(
                context.State,
                [SyncReducer.Post("The checked-out branch changed: nothing was pushed")]);
        }

        var request = new PushRequest(remote, branch, true);
        var done = $"Pushed {branch} to {remote} and set it as its upstream";
        var effect = new PushEffect(context.TabId, context.Location, request, done, branch);

        return SyncReducer.Transfer(effect, "Pushing", "Push");
    }

    /// <summary>The remotes were read for a force push: confirm it against the upstream of the branch.</summary>
    public static ImmutableArray<AppEffect> AskForce(SyncRemotesListed listed, SyncContext context)
    {
        var branch = context.Refs.FirstOrDefault(
            reference => reference.Kind == RefKind.LocalBranch && reference.Name == listed.Branch);
        if (branch is null || !SyncReducer.HasUpstream(branch))
        {
            return [SyncReducer.Post($"{listed.Branch} has no upstream: push it first")];
        }

        var upstream = context.Refs.FirstOrDefault(reference => reference.FullName == branch.UpstreamFullName);
        var target = upstream is null ? null : RemoteBranchName.Split(upstream.Name, listed.Remotes);
        if (upstream is null || target is null)
        {
            return [SyncReducer.Post($"The upstream of {branch.Name} is not a known remote branch: fetch first")];
        }

        return ConfirmForce(branch.Name, target, upstream.CommitHash, false, context);
    }

    /// <summary>
    /// A push of the tab was rejected as a non-fast-forward: offers the force push right away (#746). Nothing happens when
    /// the tab is not active, an overlay is open or the branch cannot be force pushed; the failure stays in the status bar.
    /// </summary>
    public static ImmutableArray<AppEffect> OfferForce(SyncRejection rejection, SyncContext context)
    {
        if (!context.IsActive || context.IsOverlayOpen
            || SyncReducer.CurrentBranch(context.Refs) is not { } current || current.Name != rejection.Branch)
        {
            return [];
        }

        if (rejection.Remote is not { } remote)
        {
            // A branch that is only behind its upstream is rejected too, but the answer there is a pull, not a force.
            return SyncReducer.HasUpstream(current) && current.Ahead is > 0
                ? ListRemotes(SyncRemotesPurpose.Force, current.Name, context)
                : [];
        }

        // The first push was rejected: the remote already has a branch of that name with other commits. The lease needs
        // the tip of the remote branch, so it has to be known here.
        var tracking = context.Refs.FirstOrDefault(
            reference => reference.Kind == RefKind.RemoteBranch
                && reference.SymbolicTarget is null
                && reference.Name == $"{remote}/{rejection.Branch}");
        if (tracking is null)
        {
            return
            [
                SyncReducer.Post(
                    $"{remote}/{rejection.Branch} is not known here: fetch first, then force push (<space>rP)"),
            ];
        }

        var check = new CheckBehindEffect(
            context.TabId,
            context.Location,
            rejection.Branch,
            current.CommitHash,
            remote,
            tracking.CommitHash);

        return [check];
    }

    /// <summary>The branch was compared with the remote one: offers the force push unless the branch is just behind.</summary>
    public static ImmutableArray<AppEffect> BehindChecked(SyncBehindChecked result, SyncContext context)
    {
        // Only a branch that still is where it was compared is worth a question.
        if (!context.IsActive || context.IsOverlayOpen
            || SyncReducer.CurrentBranch(context.Refs) is not { } now
            || now.Name != result.Branch || now.CommitHash != result.CommitHash)
        {
            return [];
        }

        if (result.IsBehind)
        {
            var text = $"{result.Branch} is behind {result.Remote}/{result.Branch}: pull or rebase it, no force push offered";
            return [SyncReducer.Post(text)];
        }

        var target = new RemoteBranchName(result.Remote, result.Branch);

        return ConfirmForce(result.Branch, target, result.TrackingHash, true, context);
    }

    private static ImmutableArray<AppEffect> ConfirmForce(
        string branch,
        RemoteBranchName target,
        string upstreamHash,
        bool setUpstream,
        SyncContext context)
    {
        var name = $"{target.Remote}/{target.Branch}";
        var prompt = new ConfirmPrompt(
            "Force push",
            [
                $"Force push {branch} to {name}, overwriting it (now at {ShortHash.Of(upstreamHash)})?",
                "A lease: refused when the remote moved since your last fetch.",
                "Commits that are only on the remote branch are no longer on it afterwards.",
            ],
            $"Undo: git push --force-with-lease {target.Remote} {upstreamHash}:{target.Branch}",
            "Force push");
        var lease = new PushLease(target.Branch, upstreamHash);
        var request = new PushRequest(target.Remote, branch, setUpstream, lease);
        var done = $"Force pushed {branch} to {name} (was {ShortHash.Of(upstreamHash)})";

        return [new ShowConfirmEffect(prompt, new SyncForceConfirmed(context.TabId, request, done), SyncReducer.TooSmallNotice)];
    }
}
