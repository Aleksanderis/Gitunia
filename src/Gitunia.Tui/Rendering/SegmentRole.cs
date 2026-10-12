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

    /// <summary>A sidebar section title.</summary>
    SectionTitle,

    /// <summary>The line between panes while neither side has focus.</summary>
    Border,

    /// <summary>The line between panes while the pane beside it has focus.</summary>
    FocusedBorder,
}
