namespace Gitunia.Git.Watching.Models;

/// <summary>One hash per group of state files, from <see cref="RepositoryFingerprint.Compute"/>.</summary>
public readonly record struct RepositoryFingerprintGroups(int Head, int Refs, int Index, int Worktrees, int Operation)
{
    /// <summary>
    /// The refresh kinds that cover the groups that differ from <paramref name="previous"/>: HEAD and refs together
    /// read HEAD, refs and worktrees, the index reads the status, an operation file reads the status too.
    /// <see cref="RepositoryChangeKinds.None"/> when nothing differs.
    /// </summary>
    public RepositoryChangeKinds ChangedKinds(RepositoryFingerprintGroups previous)
    {
        var kinds = RepositoryChangeKinds.None;
        if (Head != previous.Head || Refs != previous.Refs)
        {
            kinds |= RepositoryChangeKinds.Head | RepositoryChangeKinds.Refs;
        }

        if (Index != previous.Index)
        {
            kinds |= RepositoryChangeKinds.Index | RepositoryChangeKinds.WorkingTree;
        }

        if (Worktrees != previous.Worktrees)
        {
            kinds |= RepositoryChangeKinds.Worktrees;
        }

        if (Operation != previous.Operation)
        {
            kinds |= RepositoryChangeKinds.Operation | RepositoryChangeKinds.Index | RepositoryChangeKinds.WorkingTree;
        }

        return kinds;
    }

    /// <summary>
    /// These groups, except that those a refresh of <paramref name="covered"/> kinds has read take their value from
    /// <paramref name="current"/>.
    /// </summary>
    public RepositoryFingerprintGroups Accept(RepositoryFingerprintGroups current, RepositoryChangeKinds covered)
    {
        var refsCovered = covered.HasFlag(RepositoryChangeKinds.Head) && covered.HasFlag(RepositoryChangeKinds.Refs);
        var statusCovered = covered.HasFlag(RepositoryChangeKinds.Index)
            && covered.HasFlag(RepositoryChangeKinds.WorkingTree);
        return new RepositoryFingerprintGroups(
            refsCovered ? current.Head : Head,
            refsCovered ? current.Refs : Refs,
            statusCovered ? current.Index : Index,
            covered.HasFlag(RepositoryChangeKinds.Worktrees) || refsCovered ? current.Worktrees : Worktrees,
            covered.HasFlag(RepositoryChangeKinds.Operation) && statusCovered ? current.Operation : Operation);
    }
}
