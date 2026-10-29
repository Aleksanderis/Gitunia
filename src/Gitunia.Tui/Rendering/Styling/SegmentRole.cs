namespace Gitunia.Tui.Rendering.Styling;

/// <summary>What a piece of text means; the view maps a role to colours and text styles, tests ignore it.</summary>
public enum SegmentRole
{
    Plain,

    /// <summary>Part of the commit graph; <see cref="StyledSegment.ColorIndex"/> is the lane.</summary>
    Lane,

    /// <summary>
    /// A branch name in the graph, in the colour of its lane (<see cref="StyledSegment.ColorIndex"/>, as for
    /// <see cref="Lane"/>), bold.
    /// </summary>
    LaneLabel,
    Hash,
    Punctuation,
    LocalBranch,
    CurrentBranch,
    RemoteBranch,
    Tag,
    BarButton,

    ActiveTab,

    /// <summary>The selected tab of a pane while the pane has no focus (the bottom pane's tab, or the title of a pane with a single view): a chip like the other tabs, told apart by bold text in the accent colour (bold and underlined where there are no colours).</summary>
    SelectedTab,

    /// <summary>A tab that is not the selected one, in the tab row or the bottom pane's title row: a button, a tinted chip on the bar (underlined where there are no colours, #240).</summary>
    RepositoryTab,

    /// <summary>The key text inside a <see cref="RepositoryTab"/> chip, in bold.</summary>
    RepositoryTabKey,
    StatusMode,

    /// <summary>
    /// The title of the in-progress operation banner: a chip like <see cref="StatusMode"/> in the warning colour.
    /// </summary>
    StatusOperation,

    /// <summary>The keys of a status bar hint (the description is <see cref="Status"/>).</summary>
    StatusKey,
    Status,
    Faint,

    /// <summary>
    /// A whitespace mark of the diff (<c>·</c>, <c>→</c>): much dimmer than <see cref="Faint"/>, still readable.
    /// </summary>
    WhitespaceMark,

    /// <summary>A whitespace mark inside the changed words of an added line.</summary>
    WhitespaceMarkAddedWord,

    /// <summary>A whitespace mark inside the changed words of a removed line.</summary>
    WhitespaceMarkRemovedWord,

    /// <summary>The file name of a name-first file row: bold, so the dimmed directory after it recedes.</summary>
    FileName,

    /// <summary>A failure notice.</summary>
    Error,

    /// <summary>A failure notice in the status bar (the bar's own background).</summary>
    StatusError,

    /// <summary>A success or info notice in the status bar.</summary>
    StatusNotice,

    /// <summary>A running operation's progress in the status bar.</summary>
    StatusProgress,

    /// <summary>A warning notice in the status bar.</summary>
    StatusWarning,

    /// <summary>A success or info notice when new: a filled chip, then <see cref="StatusNotice"/>.</summary>
    StatusNoticeFresh,

    /// <summary>A warning in its first moments, settling to <see cref="StatusWarning"/>.</summary>
    StatusWarningFresh,

    /// <summary>A failure in its first moments, settling to <see cref="StatusError"/>.</summary>
    StatusErrorFresh,

    /// <summary>A sidebar section title.</summary>
    SectionTitle,

    /// <summary>The line between panes while neither side has focus.</summary>
    Border,

    /// <summary>The line between panes while the pane beside it has focus.</summary>
    FocusedBorder,

    /// <summary>A stretch of a pane's top edge inside a title row; <see cref="FrameGrid.ResolveRules"/> replaces it by the frame's own cells.</summary>
    Rule,

    /// <summary>A reference in a commit message that the repository's tracker file turns into a link.</summary>
    Link,

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

    /// <summary>The words that changed inside an added line: the line's colour on a tinted background.</summary>
    DiffAddedWord,

    /// <summary>The words that changed inside a removed line: the line's colour on a tinted background.</summary>
    DiffRemovedWord,

    /// <summary>The <c>@@</c> line of a hunk.</summary>
    DiffHunk,

    /// <summary>The bar in the left margin beside the change group under the cursor of the working-tree diff.</summary>
    DiffGroupBar,

    /// <summary>The line that starts a file in a diff.</summary>
    DiffFile,

    /// <summary>A clickable button in a pane title or on a picker row.</summary>
    Button,

    /// <summary>The text cursor of the message editor: a chip like the mode label, so it shows without colour too.</summary>
    Cursor,

    /// <summary>The key of a quick-jump target, drawn over the character after the match: a chip like the cursor, so it shows without colour too.</summary>
    JumpLabel,
}
