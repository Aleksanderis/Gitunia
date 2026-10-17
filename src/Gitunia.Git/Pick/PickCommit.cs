namespace Gitunia.Git.Pick;

/// <summary>A commit to cherry-pick or revert, with what the mainline prompt needs to describe a merge.</summary>
/// <param name="Hash">The full hash.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="Parents">The parents, first parent first; only read for a merge commit, empty otherwise.</param>
public sealed record PickCommit(string Hash, string Subject, IReadOnlyList<PickParent> Parents)
{
    /// <summary>The commit has more than one parent.</summary>
    public bool IsMerge => Parents.Count > 1;
}
