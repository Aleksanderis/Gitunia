using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Views.Theme;

namespace Gitunia.Tui.App.CommandLine;

/// <summary>What the command line asked for.</summary>
/// <param name="RepositoryPath">The repository (or any path inside it) to open, null for none.</param>
/// <param name="FrameWidth">With <see cref="FrameHeight"/>: print one frame as text of this size and exit instead of starting the UI.</param>
/// <param name="FrameHeight">See <see cref="FrameWidth"/>.</param>
/// <param name="OpenDiff">With a printed frame: open the diff of the newest commit (the diff pane shows the whole commit from its first line).</param>
/// <param name="Zoom">With a printed frame: the pane that fills the screen, null for the normal layout. <c>diff</c> implies <see cref="OpenDiff"/>.</param>
/// <param name="Tab">With a printed frame: the tab of the detail pane (default Changes, what a fresh tab shows).</param>
/// <param name="Colors">With a printed frame: write ANSI colour sequences for this scheme instead of plain text, null for plain text.</param>
/// <param name="Error">Why the arguments are invalid, null when they are fine.</param>
public sealed record CommandLineOptions(string? RepositoryPath, int? FrameWidth, int? FrameHeight, string? Error, bool OpenDiff = false, PaneKind? Zoom = null, BottomTab Tab = BottomTab.Changes, PaletteSettings? Colors = null)
{
    public bool PrintsFrame => FrameWidth is not null && FrameHeight is not null;
}
