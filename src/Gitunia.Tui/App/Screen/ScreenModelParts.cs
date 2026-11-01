using Gitunia.Tui.App.Tabs;
using Gitunia.App;
using Gitunia.App.Tabs;
using Gitunia.Tui.App.Config;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.Input;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Screen;

/// <summary>What the model of the screen is built from.</summary>
/// <param name="Host">The state.</param>
/// <param name="Sessions">The sessions of the open tabs; the shown one gives the graph and the details.</param>
/// <param name="Panes">The lists, which know the graph cursor and the sidebar rows.</param>
/// <param name="Keys">The key mode and the pending chord.</param>
/// <param name="Messages">The message of the status bar.</param>
/// <param name="Picker">The repository picker.</param>
/// <param name="Config">The options and the keys the chips show.</param>
/// <param name="Lines">The status bar lines of the prompts, the repeat mode and the hints.</param>
public sealed record ScreenModelParts(
    Dispatcher Host,
    TabSessionRegistry<TerminalTabSession> Sessions,
    PaneViews Panes,
    KeyController Keys,
    StatusMessages Messages,
    PickerInput Picker,
    LiveConfig Config,
    StatusLines Lines);
