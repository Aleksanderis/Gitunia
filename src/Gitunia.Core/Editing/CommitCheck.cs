using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Editing;

/// <summary>The refusals made before git runs (ux.md, workflow 2, "Message and commit"): a commit that cannot work says why at once.</summary>
public static class CommitCheck
{
    /// <summary>Shown when the message is empty or only whitespace.</summary>
    public const string EmptyMessage = "Empty commit message: write one first";

    /// <summary>Shown when nothing is staged and Amend is off.</summary>
    public const string NothingStaged = "Nothing staged";

    /// <summary>Shown when the status has not been read yet.</summary>
    public const string StatusNotLoaded = "The status is not loaded yet";

    /// <summary>Shown when Amend is on but the branch has no commit.</summary>
    public const string NothingToAmend = "Nothing to amend: there is no commit yet";

    /// <summary>Shown when Amend is switched on during a merge.</summary>
    public const string AmendDuringMerge = "Amend is not possible while a merge is in progress";

    /// <summary>Why the commit cannot run, or null when it can.</summary>
    /// <param name="draft">The message and options.</param>
    /// <param name="tree">The tab's working tree, null while the status is not loaded.</param>
    /// <param name="operation">The operation in progress; finishing a merge, cherry-pick or revert with an empty index is valid.</param>
    public static string? Refusal(CommitDraft draft, WorkingTreeState? tree, RepositoryOperation operation)
    {
        if (tree is null)
        {
            return StatusNotLoaded;
        }

        if (draft.Editor.IsBlank)
        {
            return EmptyMessage;
        }

        if (draft.Amend && tree.Snapshot.Branch.Head.Kind == HeadKind.Unborn)
        {
            return NothingToAmend;
        }

        return !draft.Amend && tree.Staged.Count == 0 && !FinishesOperation(operation) ? NothingStaged : null;
    }

    /// <summary>Why Amend cannot be switched on, or null when it can.</summary>
    /// <param name="tree">The tab's working tree.</param>
    /// <param name="operation">The operation in progress.</param>
    public static string? AmendRefusal(WorkingTreeState? tree, RepositoryOperation operation)
    {
        if (operation == RepositoryOperation.Merge)
        {
            return AmendDuringMerge;
        }

        return tree?.Snapshot.Branch.Head.Kind == HeadKind.Unborn ? NothingToAmend : null;
    }

    /// <summary>True when HEAD is already on the upstream branch, so amending it needs a force push.</summary>
    /// <param name="tree">The tab's working tree.</param>
    public static bool NeedsForcePush(WorkingTreeState? tree) =>
        tree?.Snapshot.Branch is { UpstreamName: not null, Ahead: 0 };

    private static bool FinishesOperation(RepositoryOperation operation) =>
        operation is RepositoryOperation.Merge or RepositoryOperation.CherryPick or RepositoryOperation.Revert;
}
