namespace Gitunia.Git.Worktrees.Models;

/// <summary>What <c>git worktree add</c> should create.</summary>
/// <param name="Path">Where the new worktree goes; absolute, so git never resolves it against a working
/// directory.</param>
/// <param name="Branch">The branch the worktree checks out: an existing local branch, or the name of a new
/// one.</param>
/// <param name="NewBranchStartPoint">
/// The commit a new branch starts at (a hash or ref); null checks out the existing <paramref name="Branch"/> as it
/// is.
/// </param>
public sealed record WorktreeAddRequest(string Path, string Branch, string? NewBranchStartPoint);
