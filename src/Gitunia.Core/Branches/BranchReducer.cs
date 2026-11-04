using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Text;
using Gitunia.Git.Refs;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Branches;

/// <summary>
/// Create, switch, rename, delete branches and set or unset their upstream (ux-workflows.md, workflow 3, "Branches"),
/// as pure transitions <c>(BranchCommand, BranchContext) -> Effects</c>. A name is typed in a prompt, a switch checks
/// out the branch under the cursor at once and asks in a type-to-filter prompt only when the cursor names no single
/// branch, and a delete goes through the confirm dialog (a second one when the branch holds commits that nothing else
/// reaches). Refusals that need no git (the current branch, a branch held by another worktree, a name in use) are
/// decided here from the tab refs. What an action acts on is fixed in the prompt result when the prompt opens. One
/// operation runs per tab.
/// </summary>
public static class BranchReducer
{
    /// <summary>Applies a command and returns the effects to run.</summary>
    public static ImmutableArray<AppEffect> Reduce(BranchCommand command, BranchContext context) => command switch
    {
        BranchCreateAtRequested request => CreateAt(request, context),
        BranchCreateRequested request => Create(request, context),
        BranchDeleteChosen chosen => DeleteChosen(chosen, context),
        BranchDeleteConfirmed confirmed => DeleteConfirmed(confirmed, context),
        BranchDeleteNeedsForce needsForce => AskForce(needsForce),
        BranchDeleteRequested request => BranchDeletes.Request(request.Target, context),
        BranchDetachRequested request => Detach(request, context),
        BranchFastForwardRequested request => BranchUpstreams.FastForward(request.Target, context),
        BranchForceDeleteConfirmed confirmed => ForceDeleteConfirmed(confirmed, context),
        BranchNameAccepted accepted => NameAccepted(accepted, context),
        BranchOperationCancelled => [],
        BranchOperationFailed failed => Failed(failed, context),
        BranchOperationSucceeded succeeded => Succeeded(succeeded, context),
        BranchRenameAccepted accepted => RenameAccepted(accepted, context),
        BranchRenameRequested request => RenameRequested(request, context),
        BranchSetUpstreamRequested request => BranchUpstreams.SetRequested(request.Target, context),
        BranchSwitchChosen chosen => BranchSwitches.Chosen(chosen.Choice, context),
        BranchSwitchRequested request => BranchSwitches.Request(request.Target, context),
        BranchUnsetUpstreamRequested request => BranchUpstreams.Unset(request.Target, context),
        BranchUpstreamChosen chosen => BranchUpstreams.Chosen(chosen, context),
        _ => throw new UnhandledCommandException(command),
    };

    /// <summary>A warning status, the way a refusal is told.</summary>
    internal static PostStatusEffect Post(string text, StatusSeverity severity = StatusSeverity.Warning) =>
        new(text, severity);

    private static ImmutableArray<AppEffect> Create(BranchCreateRequested request, BranchContext context)
    {
        var refusal = Refusal(context, request.Checkout);
        if (refusal is not null)
        {
            return [Post(refusal)];
        }

        var target = request.Target;
        if (target.RowKind is SidebarRowKind.LocalBranch or SidebarRowKind.RemoteBranch && target.Name is { } name)
        {
            // The full ref name, so a tag called like the branch cannot be picked instead.
            var prefix = target.RowKind == SidebarRowKind.LocalBranch ? GitRefPrefixes.Heads : GitRefPrefixes.Remotes;

            return [NamePrompt(context, $"{prefix}{name}", name, request.Checkout)];
        }

        if (target.CommitHash is { } hash)
        {
            return [NamePrompt(context, hash, ShortHash.Of(hash), request.Checkout)];
        }

        return [Post("Select a commit or a branch first")];
    }

    private static ImmutableArray<AppEffect> CreateAt(BranchCreateAtRequested request, BranchContext context)
    {
        var refusal = Refusal(context, request.Checkout);
        if (refusal is not null)
        {
            return [Post(refusal)];
        }

        return [NamePrompt(context, request.CommitHash, ShortHash.Of(request.CommitHash), request.Checkout)];
    }

    private static ShowPromptEffect NamePrompt(BranchContext context, string startPoint, string label, bool checkout)
    {
        var suffix = checkout ? string.Empty : " (not checked out)";
        var request = new PromptRequest(
            "New branch",
            $"At {label}{suffix}",
            "create",
            new BranchNameAnswer(context.TabId, startPoint, label, checkout),
            Owner: context.TabId);

        return new ShowPromptEffect(request);
    }

    private static ImmutableArray<AppEffect> NameAccepted(BranchNameAccepted accepted, BranchContext context)
    {
        // An operation may have started while the prompt was open (the refresh updates it).
        var refusal = Refusal(context, accepted.Checkout);
        if (refusal is not null)
        {
            return [Post(refusal)];
        }

        var name = accepted.Name.Trim();
        if (name.Length == 0)
        {
            return [Post("No name: nothing was created")];
        }

        if (BranchResolver.HasLocal(context.Refs, name))
        {
            return [Post($"Branch {name} already exists")];
        }

        var follow = BranchResolver.CommitOf(context.Refs, accepted.StartPoint);
        var effect = new CreateBranchEffect(
            context.TabId,
            context.Location,
            name,
            accepted.StartPoint,
            accepted.Label,
            accepted.Checkout,
            follow);

        return [effect];
    }

    private static ImmutableArray<AppEffect> Detach(BranchDetachRequested request, BranchContext context)
    {
        var refusal = Refusal(context, true);
        if (refusal is not null)
        {
            return [Post(refusal)];
        }

        var follow = BranchResolver.CommitOf(context.Refs, request.CommitHash);

        return [new SwitchBranchEffect(
            context.TabId,
            context.Location,
            SwitchTargetKind.DetachedCommit,
            request.CommitHash,
            follow)];
    }

    private static ImmutableArray<AppEffect> RenameRequested(BranchRenameRequested request, BranchContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BranchRefusals.BusyNotice)];
        }

        if (BranchResolver.ResolveLocal(request.Target, context.Refs, out var branch) is { } refusal)
        {
            return [Post(refusal)];
        }

        var prompt = new PromptRequest(
            "Rename branch",
            branch!.Name,
            "rename",
            new BranchRenameAnswer(context.TabId, branch.Name),
            branch.Name,
            Owner: context.TabId);

        return [new ShowPromptEffect(prompt)];
    }

    private static ImmutableArray<AppEffect> RenameAccepted(BranchRenameAccepted accepted, BranchContext context)
    {
        if (context.IsBusy)
        {
            return [Post(BranchRefusals.BusyNotice)];
        }

        var name = accepted.Name.Trim();
        if (name.Length == 0 || name == accepted.From)
        {
            return [Post("The name is unchanged: nothing was renamed")];
        }

        if (BranchResolver.HasLocal(context.Refs, name))
        {
            return [Post($"Branch {name} already exists")];
        }

        var follow = BranchResolver.CommitOf(context.Refs, $"{GitRefPrefixes.Heads}{accepted.From}");

        return [new RenameBranchEffect(context.TabId, context.Location, accepted.From, name, follow)];
    }

    private static ImmutableArray<AppEffect> DeleteChosen(BranchDeleteChosen chosen, BranchContext context)
    {
        var branch = context.Refs.FirstOrDefault(reference =>
            reference.Kind == RefKind.LocalBranch && reference.Name == chosen.Name);

        return branch is null
            ? [Post("That branch is gone: nothing was deleted")]
            : BranchDeletes.Ask(branch, context);
    }

    private static ImmutableArray<AppEffect> DeleteConfirmed(BranchDeleteConfirmed confirmed, BranchContext context)
    {
        if (context.IsBusy)
        {
            return [Post($"{BranchRefusals.BusyNotice}; nothing was deleted")];
        }

        var effect = new DeleteBranchEffect(
            context.TabId,
            context.Location,
            confirmed.Name,
            confirmed.CommitHash,
            confirmed.UpstreamFullName,
            false,
            BranchResolver.HeadCommit(context.Refs));

        return [effect];
    }

    // The branch holds commits that neither HEAD nor its upstream reach: a second, explicit answer is needed.
    private static ImmutableArray<AppEffect> AskForce(BranchDeleteNeedsForce needsForce)
    {
        var prompt = new ConfirmPrompt(
            "Delete unmerged branch",
            [
                $"{needsForce.Name} has commits that are not on HEAD or its upstream.",
                "Deleting it makes them unreachable.",
            ],
            UndoNotes.FromHistory,
            "Delete anyway");
        var accepted = new BranchForceDeleteConfirmed(needsForce.TabId, needsForce.Name, needsForce.CommitHash);
        var unavailable = $"{needsForce.Name} is unmerged and the screen is too small to ask: nothing was deleted";

        return [new ShowConfirmEffect(prompt, accepted, unavailable)];
    }

    private static ImmutableArray<AppEffect> ForceDeleteConfirmed(
        BranchForceDeleteConfirmed confirmed,
        BranchContext context)
    {
        if (context.IsBusy)
        {
            return [Post($"{BranchRefusals.BusyNotice}; nothing was deleted")];
        }

        var effect = new DeleteBranchEffect(
            context.TabId,
            context.Location,
            confirmed.Name,
            confirmed.CommitHash,
            null,
            true,
            BranchResolver.HeadCommit(context.Refs));

        return [effect];
    }

    private static ImmutableArray<AppEffect> Succeeded(BranchOperationSucceeded succeeded, BranchContext context) =>
        [
            new PostStatusEffect(succeeded.Text, StatusSeverity.Info, succeeded.Detail),
            new RefreshTabEffect(context.TabId),
        ];

    private static ImmutableArray<AppEffect> Failed(BranchOperationFailed failed, BranchContext context) =>
        [Post(failed.Text, StatusSeverity.Error), new RefreshTabEffect(context.TabId)];

    // Busy, or an unfinished merge, rebase, cherry-pick or revert when the action checks something out.
    private static string? Refusal(BranchContext context, bool checksOut)
    {
        if (context.IsBusy)
        {
            return BranchRefusals.BusyNotice;
        }

        return checksOut ? BranchRefusals.ForOperation(context.Operation) : null;
    }
}
