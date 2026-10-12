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

    /// <summary>A failure notice.</summary>
    Error,
    /// <summary>A sidebar section title.</summary>
    SectionTitle,

    /// <summary>The line between panes while neither side has focus.</summary>
    Border,

    /// <summary>The line between panes while the pane beside it has focus.</summary>
    FocusedBorder,
    /// <summary>A file the commit added.</summary>
    FileAdded,

    /// <summary>A file the commit deleted.</summary>
    FileDeleted,

    /// <summary>A file the commit modified, renamed, copied or changed the type of.</summary>
    FileChanged,

    /// <summary>The title bar of an overlay (palette, help, which-key).</summary>
    OverlayTitle,

    /// <summary>The keys column of an overlay.</summary>
    OverlayKey,

    /// <summary>Text that matches the active graph search.</summary>
    SearchMatch,

    /// <summary>An added line of a diff.</summary>
    DiffAdded,

    /// <summary>A removed line of a diff.</summary>
    DiffRemoved,

    /// <summary>The <c>@@</c> line of a hunk.</summary>
    DiffHunk,

    /// <summary>The line that starts a file in a diff.</summary>
    DiffFile,
}
