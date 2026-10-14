namespace Gitunia.Git.Refs.Models;

public enum RefKind
{
    LocalBranch,
    RemoteBranch,
    Tag,

    /// <summary>The HEAD pseudo-ref (only seen in commit decorations, when HEAD is detached).</summary>
    Head,
    Other,
}
