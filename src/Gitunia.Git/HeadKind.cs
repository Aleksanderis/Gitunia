namespace Gitunia.Git;

public enum HeadKind
{
    /// <summary>HEAD is on a branch that has at least one commit.</summary>
    Branch,

    /// <summary>HEAD points at a commit, not a branch.</summary>
    Detached,

    /// <summary>HEAD is on a branch that has no commits yet.</summary>
    Unborn,
}
