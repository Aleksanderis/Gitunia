namespace Gitunia.Tui.Rendering;

/// <summary>What a piece of text means; the view maps a role to colours and text styles, tests ignore it.</summary>
public enum SegmentRole
{
    Plain,

    /// <summary>Part of the commit graph; <see cref="StyledSegment.ColorIndex"/> is the lane.</summary>
    Lane,
    Hash,
    Punctuation,
    LocalBranch,
    CurrentBranch,
    RemoteBranch,
    Tag,
    Header,
    ActiveTab,
    InactiveTab,
    StatusMode,
    Status,
    Faint,

    /// <summary>A file the commit added.</summary>
    FileAdded,

    /// <summary>A file the commit deleted.</summary>
    FileDeleted,

    /// <summary>A file the commit modified, renamed, copied or changed the type of.</summary>
    FileChanged,
}
