using Gitunia.Core.Workspaces.Panes;
using Gitunia.Git;

namespace Gitunia.Core.DiffPane;

/// <summary>
/// What a tab's diff pane is asked to show and how it is laid out: open or not, zoomed, the file or folder to put the
/// cursor on once the diff has loaded, the one it shows, and the file of the Files tab it shows instead of a diff. The
/// loaded diff itself lives in the tab's stores (big data); this state refers to files by path and to commits by id.
/// Not saved with the session.
/// </summary>
/// <param name="IsOpen">The pane was opened on demand (narrow or short screens).</param>
/// <param name="Zoomed">The pane that fills the screen, null for the normal layout.</param>
/// <param name="PendingFile">The file to show once the diff has loaded, null for none.</param>
/// <param name="PendingFolder">The folder whose combined changes to show once the diff has loaded (tree style of the
/// Changes tab), null for none.</param>
/// <param name="ShownFolder">The folder the pane is asked to show; kept so the folder view survives a reload with other
/// options. Null when the pane shows a single file.</param>
/// <param name="ShownPath">The path of the file the pane shows, kept to show the same file when the diff reloads.</param>
/// <param name="ContentPath">The path of the file whose content the pane shows instead of a diff (Files tab).</param>
/// <param name="ContentCommit">The commit <paramref name="ContentPath"/> is read from.</param>
/// <param name="Placeholder">The note the Files tab's pane shows while no file is under the tree's cursor.</param>
public sealed record DiffPaneState(
    bool IsOpen = false,
    PaneKind? Zoomed = null,
    string? PendingFile = null,
    string? PendingFolder = null,
    string? ShownFolder = null,
    string? ShownPath = null,
    string? ContentPath = null,
    ObjectId? ContentCommit = null,
    DiffPlaceholder Placeholder = DiffPlaceholder.None)
{
    /// <summary>A pane that was never used.</summary>
    public static DiffPaneState Empty { get; } = new();

    /// <summary>The pane shows the content of a file, not a diff.</summary>
    public bool IsShowingContent => ContentPath is not null;

    /// <summary>The pane belongs to the Files tab: it shows a file's content or the placeholder.</summary>
    public bool ShowsFilesPane => IsShowingContent || Placeholder != DiffPlaceholder.None;

    /// <summary>The same pane without any file or content chosen (the layout choices stay).</summary>
    public DiffPaneState WithoutContent() => this with
    {
        ContentPath = null,
        ContentCommit = null,
        Placeholder = DiffPlaceholder.None,
    };
}
