namespace Gitunia.Tui.App.Branches;

/// <summary>Which prompt popup of the branch actions is open.</summary>
public enum BranchPromptKind
{
    None,

    /// <summary>Typing the name of a new branch (and checking it out, or not).</summary>
    Create,

    /// <summary>Typing the new name of a branch.</summary>
    Rename,

    /// <summary>Choosing the branch or commit to check out.</summary>
    Switch,

    /// <summary>Choosing the remote branch that becomes the upstream.</summary>
    Upstream,

    /// <summary>Choosing which of several local branches (a folder, a graph commit) to delete.</summary>
    Delete,
}
