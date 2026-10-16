namespace Gitunia.Git.Diff.Models;

/// <summary>Which working-tree diff of a file is read: what <c>git diff</c> or what <c>git diff --cached</c> shows.</summary>
public enum WorkingDiffSide
{
    /// <summary>The index against the working tree (a file in the Unstaged list, untracked files as additions).</summary>
    Unstaged,

    /// <summary>HEAD against the index (a file in the Staged list).</summary>
    Staged,
}
