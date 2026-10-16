namespace Gitunia.Core.Branches;

/// <summary>What the switch picker would check out.</summary>
public enum SwitchTargetKind
{
    /// <summary>An existing local branch.</summary>
    LocalBranch,

    /// <summary>A remote branch: a local tracking branch is created.</summary>
    RemoteBranch,

    /// <summary>A commit with a detached HEAD.</summary>
    DetachedCommit,
}
