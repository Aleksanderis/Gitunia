namespace Gitunia.Core.Workspaces.Panes;

/// <summary>The parts of a tab's pane arrangement only the terminal layer keeps (the diff views).</summary>
/// <param name="IsDiffOpen">The diff pane is open.</param>
/// <param name="Zoomed">The pane that fills the screen, null for the normal layout.</param>
/// <param name="ZoomBoth">With a zoomed detail or diff pane: detail and diff fill the area together.</param>
/// <param name="IsWorkingDiffZoomed">The Changes view diff fills the view and has no divider.</param>
public sealed record PaneLayoutFacts(bool IsDiffOpen, PaneKind? Zoomed, bool ZoomBoth, bool IsWorkingDiffZoomed);
