using System.Collections.Immutable;
using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Gitunia.Core.Text;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Git.Refs;
using Gitunia.Git.Worktrees;

namespace Gitunia.Core.Worktrees;

/// <summary>The wording of the worktree dialogs and refusals (ux-workflows.md, workflow 6): pure, so the texts are
/// testable without a terminal.</summary>
public static class WorktreeTexts
{
    private static readonly string NewWorktree = KeyTokens.Of(ActionIds.WorktreeAdd, "a new worktree");

    /// <summary>Shown when another worktree operation of the tab still runs.</summary>
    public const string BusyNotice = "Busy: another operation on this tab is still running";

    /// <summary>Shown when an action needs a worktree row and the cursor is elsewhere.</summary>
    public const string NoWorktreeNotice = "Select a worktree in the sidebar first";

    /// <summary>Shown when a confirm cannot be displayed.</summary>
    public const string TooSmallNotice = "The screen is too small to ask: nothing was changed";

    private const int NamedChangeLimit = 5;

    private const string DirtyConsequence =
        "The uncommitted changes are deleted with the directory (--force). Git keeps no copy: this cannot be undone.";

    /// <summary>Why the worktree cannot be removed here, empty when it can.</summary>
    public static string RemoveRefusal(WorktreeInfo worktree, string tabRoot)
    {
        if (worktree.IsMain)
        {
            return "The main worktree cannot be removed";
        }

        if (RepositoryPathComparer.Instance.Equals(worktree.Path, tabRoot))
        {
            return "This tab is on that worktree: open another to remove it";
        }

        if (worktree.IsLocked)
        {
            return $"{worktree.Path} is locked: {KeyTokens.Of(ActionIds.WorktreeUnlock, "unlock it")} first";
        }

        if (worktree.IsPrunable)
        {
            return $"{worktree.Path} is missing on disk: {KeyTokens.Of(ActionIds.WorktreePrune, "prune it")} instead";
        }

        return string.Empty;
    }

    /// <summary>The first confirm of a removal.</summary>
    public static ConfirmPrompt RemovePrompt(WorktreeInfo worktree)
    {
        var branch = "It has a detached HEAD: commits that no branch reaches are lost.";
        if (worktree.BranchFullName is { } name)
        {
            branch = $"Its branch {ShortName(name)} is kept.";
        }

        return new ConfirmPrompt(
            "Remove worktree",
            [$"Remove the worktree {worktree.Path}?", branch],
            RemoveUndoHint(worktree),
            "Remove worktree");
    }

    /// <summary>The second confirm, for a worktree that holds uncommitted changes.</summary>
    public static ConfirmPrompt DirtyRemovePrompt(string path, ImmutableArray<string> changes)
    {
        var names = changes.Take(NamedChangeLimit).ToList();
        var more = changes.Length > names.Count ? $" and {changes.Length - names.Count} more" : string.Empty;

        return new ConfirmPrompt(
            "Remove worktree with changes",
            [
                $"{path} has {CountText.Plural(changes.Length, "changed or untracked path")}: " +
                $"{string.Join(", ", names)}{more}",
                "Remove it anyway?",
            ],
            DirtyConsequence,
            "Remove and lose changes");
    }

    /// <summary>The confirm of a prune, listing the entries.</summary>
    public static ConfirmPrompt PrunePrompt(IReadOnlyList<WorktreeInfo> prunable)
    {
        var lines = new List<string>
        {
            $"Prune {CountText.Plural(prunable.Count, "worktree entry")} whose directory is gone?",
        };
        lines.AddRange(prunable.Select(worktree => worktree.Path));

        return new ConfirmPrompt(
            "Prune worktrees",
            lines,
            $"Only git's records are deleted; a branch can be checked out again with {NewWorktree}.",
            "Prune");
    }

    /// <summary>The default path of a new worktree: a sibling of the tab's working tree named after the
    /// branch.</summary>
    public static string DefaultPath(string tabRoot, string branch)
    {
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(tabRoot)) ?? tabRoot;
        var directory = branch.Replace('/', '-').Replace('\\', '-');

        return Path.Combine(parent, directory);
    }

    /// <summary>The branch name without its <c>refs/heads/</c> prefix.</summary>
    public static string ShortName(string fullName)
    {
        if (fullName.StartsWith(GitRefPrefixes.Heads, StringComparison.Ordinal))
        {
            return fullName[GitRefPrefixes.Heads.Length..];
        }

        return fullName;
    }

    private static string RemoveUndoHint(WorktreeInfo worktree)
    {
        if (worktree.BranchFullName is { } branch)
        {
            return $"The branch stays; {ShortName(branch)} can be checked out again with {NewWorktree}.";
        }

        return "No undo is offered for commits that no branch reaches.";
    }
}
