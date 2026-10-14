using Gitunia.Git.Diff.Models;

namespace Gitunia.Tui.Rendering.Styling;

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
    BarButton,

    /// <summary>A tab of the bottom pane that is not showing: a button on the bar background (underlined where there is no background).</summary>
    TabButton,
    ActiveTab,
    InactiveTab,

    /// <summary>A repository tab of the tab row that is not the active one: a button, a tinted chip on the bar (underlined where there are no colours, #240).</summary>
    RepositoryTab,
    StatusMode,
    Status,
    Faint,

    /// <summary>A failure notice.</summary>
    Error,

    /// <summary>A failure notice in the status bar (the bar's own background).</summary>
    StatusError,

    /// <summary>A sidebar section title.</summary>
    SectionTitle,

    /// <summary>The line between panes while neither side has focus.</summary>
    Border,

    /// <summary>The line between panes while the pane beside it has focus.</summary>
    FocusedBorder,

    /// <summary>A stretch of a pane's top edge inside a title row; <see cref="FrameGrid.ResolveRules"/> replaces it by the frame's own cells.</summary>
    Rule,

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

    /// <summary>A clickable button in a pane title or on a picker row.</summary>
    Button,
}
