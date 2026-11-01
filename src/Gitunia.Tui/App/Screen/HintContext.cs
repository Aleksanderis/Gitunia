using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Panes;

namespace Gitunia.Tui.App.Screen;

/// <summary>What the status bar hints depend on besides the overlays.</summary>
/// <param name="Repeat">The sticky repeat mode, which has hints of its own.</param>
/// <param name="DiffView">Tells whether the diff pane shows a file's content.</param>
/// <param name="Config">How many hints and which commands to show.</param>
public sealed record HintContext(RepeatController Repeat, DiffPaneView DiffView, LiveConfig Config);
