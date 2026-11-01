namespace Gitunia.Core.Conflicts;

/// <summary>The kinds of <see cref="ConflictTarget"/>.</summary>
public enum ConflictTargetKind
{
    /// <summary>A hint chip.</summary>
    Chip,

    /// <summary>The header line of a pane.</summary>
    Header,

    /// <summary>A row of a version pane.</summary>
    VersionRow,

    /// <summary>A line of the result.</summary>
    ResultLine,
}
