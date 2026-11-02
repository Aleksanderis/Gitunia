using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Shell;

/// <summary>What a change of the options or the theme reaches.</summary>
/// <param name="Overlays">Takes the which-key setting.</param>
/// <param name="Picker">Takes the scan roots.</param>
/// <param name="Terminal">Rebuilds the palette.</param>
/// <param name="View">Repaints every view.</param>
public sealed record OptionTargets(OverlayController Overlays, PickerInput Picker, TerminalHost Terminal, MainView View);
