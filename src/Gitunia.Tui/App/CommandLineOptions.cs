using Gitunia.Core.Workspaces;

namespace Gitunia.Tui.App;

/// <summary>What the command line asked for.</summary>
/// <param name="RepositoryPath">The repository (or any path inside it) to open, null for none.</param>
/// <param name="FrameWidth">With <see cref="FrameHeight"/>: print one frame as text of this size and exit instead of starting the UI.</param>
/// <param name="FrameHeight">See <see cref="FrameWidth"/>.</param>
/// <param name="OpenDiff">With a printed frame: open the diff of the newest commit (its first file list entry is not selected, the whole commit is shown).</param>
/// <param name="Zoom">With a printed frame: the pane that fills the screen, null for the normal layout. <c>diff</c> implies <see cref="OpenDiff"/>.</param>
/// <param name="Error">Why the arguments are invalid, null when they are fine.</param>
public sealed record CommandLineOptions(string? RepositoryPath, int? FrameWidth, int? FrameHeight, string? Error, bool OpenDiff = false, PaneKind? Zoom = null)
{
    public bool PrintsFrame => FrameWidth is not null && FrameHeight is not null;
}
