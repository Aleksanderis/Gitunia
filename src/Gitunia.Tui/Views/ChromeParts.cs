namespace Gitunia.Tui.Views;

/// <summary>What the chrome around the panes (frame, titles, buttons, tab row, status bar) is drawn from.</summary>
/// <param name="Panes">The list widgets.</param>
/// <param name="Geometry">Where the panes are.</param>
/// <param name="Grids">The frame lines.</param>
/// <param name="Content">The screen model and the pane titles.</param>
/// <param name="Gate">Tells when an overlay owns the mouse.</param>
/// <param name="Mode">Which panes are on screen.</param>
public sealed record ChromeParts(
    PaneViews Panes,
    PaneGeometry Geometry,
    FrameGrids Grids,
    IScreenContent Content,
    IMouseGate Gate,
    ScreenMode Mode);
