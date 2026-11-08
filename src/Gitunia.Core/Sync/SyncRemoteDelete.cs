using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Sync;

/// <summary>
/// Deleting a branch on a remote (ux-workflows.md, workflow 5): asks for the remote, then the branch, then confirms;
/// when the cursor names one remote branch (its row, or the one on the graph commit) there is no picker. The confirm
/// names the tip the branch is at and how to put it back; the delete is guarded by a lease on that tip.
/// </summary>
internal static class SyncRemoteDelete
{
    /// <summary>The delete key: reads the remotes, remembering the one remote branch the cursor names.</summary>
    public static ImmutableArray<AppEffect> Requested(SyncDeleteRequested request, SyncContext context)
    {
        if (context.IsBusy)
        {
            return [SyncReducer.Post(SyncReducer.BusyNotice)];
        }

        var named = RemoteDeleteTarget.Names(context.Refs, request.Target);
        var direct = named.Count == 1 ? named[0] : null;

        return [new ListSyncRemotesEffect(context.TabId, context.Location, SyncRemotesPurpose.Delete, null, direct)];
    }

    /// <summary>The remotes were read: ask for what to delete.</summary>
    public static ImmutableArray<AppEffect> Listed(SyncRemotesListed listed, SyncContext context)
    {
        var branches = RemoteBranchesOf(context.Refs, listed.Remotes);
        if (listed.DirectName is { } direct)
        {
            return AskRow(direct, listed.Remotes, branches, context);
        }

        var withBranches = branches.Select(branch => branch.Name.Remote).Distinct().ToList();
        if (withBranches.Count == 0)
        {
            return [SyncReducer.Post("No remote branches known: fetch first")];
        }

        if (withBranches is [var only])
        {
            // One remote has branches: asking for it first is a step without a choice.
            return [AskBranch(branches, only, context)];
        }

        var request = new PromptRequest(
            "Delete remote branch",
            "Remote to delete a branch from",
            "choose",
            new SyncDeleteRemoteAnswer(context.TabId, branches),
            Choices: [.. withBranches.Select(name => new PromptChoice(name, name))],
            NoMatch: SyncReducer.NoMatch,
            Owner: context.TabId);

        return [new ShowPromptEffect(request)];
    }

    /// <summary>A remote was picked: ask for the branch on it.</summary>
    public static ImmutableArray<AppEffect> RemoteChosen(SyncDeleteRemoteChosen chosen, SyncContext context) =>
        [AskBranch(chosen.Branches, chosen.Remote, context)];

    /// <summary>A branch was picked: ask to confirm.</summary>
    public static ImmutableArray<AppEffect> BranchChosen(SyncDeleteBranchChosen chosen, SyncContext context) =>
        AskDelete(chosen.Branches, chosen.Remote, chosen.Branch, context);

    /// <summary><c>y</c> in the confirm: delete the branch on the remote.</summary>
    public static ImmutableArray<AppEffect> Confirmed(SyncDeleteConfirmed confirmed, SyncContext context)
    {
        if (context.IsBusy)
        {
            return [SyncReducer.Post($"{SyncReducer.BusyNotice}; nothing was deleted")];
        }

        var effect = new DeleteRemoteBranchEffect(
            context.TabId,
            context.Location,
            confirmed.Remote,
            confirmed.Branch,
            confirmed.CommitHash);

        return [effect];
    }

    private static ImmutableArray<AppEffect> AskRow(
        string rowName,
        ImmutableArray<string> remotes,
        ImmutableArray<RemoteBranchRef> branches,
        SyncContext context)
    {
        var name = RemoteBranchName.Split(rowName, remotes);
        if (name is null || !branches.Any(item => item.Name == name))
        {
            return [SyncReducer.Post($"{rowName} is not a branch on a remote: nothing to delete")];
        }

        return AskDelete(branches, name.Remote, name.Branch, context);
    }

    private static ShowPromptEffect AskBranch(
        ImmutableArray<RemoteBranchRef> branches,
        string remote,
        SyncContext context)
    {
        var names = branches.Where(branch => branch.Name.Remote == remote).Select(branch => branch.Name.Branch);
        var request = new PromptRequest(
            "Delete remote branch",
            $"Branch to delete on {remote}",
            "choose",
            new SyncDeleteBranchAnswer(context.TabId, branches, remote),
            Choices: [.. names.Select(name => new PromptChoice(name, name))],
            NoMatch: SyncReducer.NoMatch,
            Owner: context.TabId);

        return new ShowPromptEffect(request);
    }

    private static ImmutableArray<AppEffect> AskDelete(
        ImmutableArray<RemoteBranchRef> branches,
        string remote,
        string branch,
        SyncContext context)
    {
        var target = branches.FirstOrDefault(item => item.Name.Remote == remote && item.Name.Branch == branch);
        if (target is null)
        {
            return [SyncReducer.Post($"{remote}/{branch} is not a branch on a remote: nothing to delete")];
        }

        var prompt = new ConfirmPrompt(
            "Delete branch on remote",
            [
                $"Delete the branch {branch} on the remote {remote} (at {ShortHash.Of(target.CommitHash)})?",
                "Local branches are not touched.",
            ],
            UndoNotes.FromHistory,
            "Delete on remote");
        var accepted = new SyncDeleteConfirmed(context.TabId, remote, branch, target.CommitHash);

        return [new ShowConfirmEffect(prompt, accepted, SyncReducer.TooSmallNotice)];
    }

    private static ImmutableArray<RemoteBranchRef> RemoteBranchesOf(
        ImmutableArray<GitRef> refs,
        ImmutableArray<string> remotes)
    {
        var branches = ImmutableArray.CreateBuilder<RemoteBranchRef>();
        var remoteRefs = refs.Where(item => item.Kind == RefKind.RemoteBranch && item.SymbolicTarget is null);
        foreach (var reference in remoteRefs)
        {
            if (RemoteBranchName.Split(reference.Name, remotes) is { } name)
            {
                branches.Add(new RemoteBranchRef(name, reference.CommitHash));
            }
        }

        return branches.ToImmutable();
    }
}
