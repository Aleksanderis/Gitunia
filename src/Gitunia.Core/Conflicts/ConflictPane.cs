namespace Gitunia.Core.Conflicts;

/// <summary>The panes of the conflict editor: three read-only versions on top and the result below.</summary>
public enum ConflictPane
{
    /// <summary>The common ancestor.</summary>
    Base,

    /// <summary>The current side.</summary>
    Ours,

    /// <summary>The other side.</summary>
    Theirs,

    /// <summary>The whole file as it will be saved; the only pane that can be typed in.</summary>
    Result,
}
