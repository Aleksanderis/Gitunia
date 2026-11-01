using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Overlays;
using Gitunia.Core.Prompts;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Text;
using Gitunia.Git.Refs.Models;

namespace Gitunia.Core.Branches;

/// <summary>The delete action of <see cref="BranchReducer"/>: pick among several branches, then ask.</summary>
internal static class BranchDeletes
{
    private const string TooSmallNotice = "The screen is too small to ask: nothing was deleted";

    // The branch under the cursor goes to the confirm dialog; a folder or a graph commit with several branches opens a
    // list to pick from first.
    public static ImmutableArray<AppEffect> Request(BranchTarget target, BranchContext context)
    {
        if (context.IsBusy)
        {
            return [BranchReducer.Post(BranchRefusals.BusyNotice)];
        }

        var names = BranchResolver.DeleteChoices(target, context.Refs);
        if (names.Count > 1)
        {
            return [ShowList(target, names, context)];
        }

        if (target.RowKind == SidebarRowKind.Folder)
        {
            if (names.Count == 0)
            {
                return [BranchReducer.Post($"No local branch in {target.Name}/")];
            }

            return Ask(context.Refs.First(reference => IsLocal(reference, names[0])), context);
        }

        if (BranchResolver.ResolveLocal(target, context.Refs, out var branch) is { } refusal)
        {
            return [BranchReducer.Post(refusal)];
        }

        return Ask(branch!, context);
    }

    public static ImmutableArray<AppEffect> Ask(GitRef branch, BranchContext context)
    {
        if (branch.IsCurrent)
        {
            return [BranchReducer.Post($"{branch.Name} is checked out: switch to another branch first")];
        }

        if (branch.CheckedOutIn is { } path)
        {
            return [BranchReducer.Post($"{branch.Name} is checked out in {path}")];
        }

        var prompt = new ConfirmPrompt(
            "Delete branch",
            [$"Delete the branch {branch.Name} (at {ShortHash.Of(branch.CommitHash)})?"],
            $"Undo: git branch {branch.Name} {branch.CommitHash}",
            "Delete branch");
        var accepted = new BranchDeleteConfirmed(
            context.TabId,
            branch.Name,
            branch.CommitHash,
            branch.UpstreamFullName);

        return [new ShowConfirmEffect(prompt, accepted, TooSmallNotice)];
    }

    private static ShowPromptEffect ShowList(BranchTarget target, List<string> names, BranchContext context)
    {
        var label = target.RowKind == SidebarRowKind.Folder ? $"{target.Name}/" : ShortHash.Of(target.CommitHash);
        var request = new PromptRequest(
            "Delete branch",
            $"Branch to delete at {label}",
            "choose",
            new BranchDeleteAnswer(context.TabId),
            Choices: [.. names.Select(name => new PromptChoice(name, name))],
            Owner: context.TabId,
            ListOnly: true);

        return new ShowPromptEffect(request);
    }

    private static bool IsLocal(GitRef reference, string name) =>
        reference.Kind == RefKind.LocalBranch && reference.Name == name;
}
