namespace Gitunia.Core.Conflicts;

/// <summary>How the user resolved one conflict region.</summary>
public enum ConflictPick
{
    /// <summary>Not resolved yet: the region keeps its conflict markers in the result.</summary>
    None,

    /// <summary>The current side's lines.</summary>
    Ours,

    /// <summary>The other side's lines.</summary>
    Theirs,

    /// <summary>The common ancestor's lines.</summary>
    Base,

    /// <summary>Both sides, ours first.</summary>
    Both,

    /// <summary>Lines picked one by one in a Visual selection of a pane.</summary>
    Lines,

    /// <summary>Text the user typed in the result pane.</summary>
    Edited,
}
