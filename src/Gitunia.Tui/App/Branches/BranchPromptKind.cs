namespace Gitunia.Tui.App.Branches;

/// <summary>Which status bar prompt of the branch actions is open.</summary>
public enum BranchPromptKind
{
    None,

    /// <summary>Typing the name of a new branch (and checking it out, or not).</summary>
    Create,

    /// <summary>Typing the new name of a branch.</summary>
    Rename,

    /// <summary>Choosing the branch or commit to check out.</summary>
    Switch,
}
