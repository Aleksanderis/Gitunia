using Gitunia.Core.Keymap;
using Gitunia.Tui.App.Overlays;

namespace Gitunia.Tui.Input;

/// <summary>Which view has the keys: the picker, an overlay or the focused pane; and whether anything but the panes does.</summary>
/// <param name="picker">The repository picker.</param>
/// <param name="cover">Which overlays are open.</param>
public sealed class KeyFocus(PickerInput picker, OverlayCover cover) : IInputBlocker
{
    /// <summary>The picker is on screen and no overlay is above it.</summary>
    public bool IsPickerShowing => picker.IsOpen && !cover.IsAnyOverlayOpen();

    /// <inheritdoc />
    public bool IsBlocked => cover.IsAnyOverlayOpen() || picker.IsOpen;

    /// <summary>The keymap view the next key is looked up in.</summary>
    public string ViewName() => IsPickerShowing ? DefaultKeymap.PickerView : cover.ViewName();
}
