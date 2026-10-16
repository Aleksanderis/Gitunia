using Gitunia.Core.Overlays;
using Gitunia.Git.Refs;
using Gitunia.Git.Worktrees;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Worktrees;

/// <summary>The wording of the worktree dialogs and refusals (ux.md, workflow 6): pure, so the texts are testable without a terminal.</summary>
public static class WorktreeTexts
{
    private const int NamedChangeLimit = 5;
    private const int ShortHashLength = 7;

    private const string DirtyConsequence =
        "The uncommitted changes are deleted with the directory (--force). Git keeps no copy: this cannot be undone.";

    /// <summary>Why the worktree cannot be removed here, empty when it can.</summary>
    public static string RemoveRefusal(WorktreeInfo worktree, string tabRoot, Func<string, string, bool> samePath)
    {
        if (worktree.IsMain)
        {
            return "The main worktree cannot be removed";
        }

        if (samePath(worktree.Path, tabRoot))
        {
            return "This tab is on that worktree: open another to remove it";
        }

        if (worktree.IsLocked)
        {
            return $"{worktree.Path} is locked: unlock it first (<space>wu)";
        }

        return worktree.IsPrunable
            ? $"{worktree.Path} is missing on disk: prune it instead (<space>wp)"
            : string.Empty;
    }

    public static ConfirmPrompt RemovePrompt(WorktreeInfo worktree)
    {
        var branch = worktree.BranchFullName is { } name
            ? $"Its branch {ShortName(name)} is kept."
            : "It has a detached HEAD: commits that no branch reaches are lost.";
        return new ConfirmPrompt(
            "Remove worktree",
            [$"Remove the worktree {worktree.Path}?", branch],
            RemoveUndoHint(worktree),
            "Remove worktree");
    }

    public static ConfirmPrompt DirtyRemovePrompt(WorktreeInfo worktree, IReadOnlyList<string> changes)
    {
        var names = changes.Take(NamedChangeLimit).ToList();
        var more = changes.Count > names.Count ? $" and {changes.Count - names.Count} more" : string.Empty;
        return new ConfirmPrompt(
            "Remove worktree with changes",
            [
                $"{worktree.Path} has {OperationText.Plural(changes.Count, "changed or untracked path")}: {string.Join(", ", names)}{more}",
                "Remove it anyway?",
            ],
            DirtyConsequence,
            "Remove and lose changes");
    }

    public static ConfirmPrompt PrunePrompt(IReadOnlyList<WorktreeInfo> prunable)
    {
        var lines = new List<string>
        {
            $"Prune {OperationText.Plural(prunable.Count, "worktree entry")} whose directory is gone?",
        };
        lines.AddRange(prunable.Select(worktree => worktree.Path));
        return new ConfirmPrompt(
            "Prune worktrees",
            lines,
            "Only git's records are deleted. Undo: git worktree add <path> <branch> makes a checkout again.",
            "Prune");
    }

    /// <summary>The default path of a new worktree: a sibling of the tab's working tree named after the branch.</summary>
    public static string DefaultPath(string tabRoot, string branch)
    {
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(tabRoot)) ?? tabRoot;
        var directory = branch.Replace('/', '-').Replace('\\', '-');
        return Path.Combine(parent, directory);
    }

    public static string ShortName(string fullName) =>
        fullName.StartsWith(GitRefPrefixes.Heads, StringComparison.Ordinal) ? fullName[GitRefPrefixes.Heads.Length..] : fullName;

    private static string RemoveUndoHint(WorktreeInfo worktree)
    {
        if (worktree.BranchFullName is { } branch)
        {
            return $"Undo: git worktree add <path> {ShortName(branch)} checks the branch out again (a clean worktree has nothing else to lose).";
        }

        var hash = worktree.HeadHash is { Length: >= ShortHashLength } head ? head[..ShortHashLength] : "<hash>";
        return $"Undo: git worktree add <path> {hash} checks that commit out again.";
    }
}
