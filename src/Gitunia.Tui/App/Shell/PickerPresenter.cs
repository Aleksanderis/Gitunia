using Gitunia.Tui.Input;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// The picker lives in the shell state; the view follows it: the graph hides behind it and the focus comes back when it
/// closes.
/// </summary>
public sealed class PickerPresenter
{
    private readonly PickerInput _picker;
    private readonly KeyController _keys;
    private readonly VisualRangeInput _visual;
    private readonly MainView _view;
    private readonly PaneFocusCoordinator _focus;
    private bool _pickerShown;

    /// <param name="picker">The picker state.</param>
    /// <param name="keys">The key mode, which decides whether typed keys go to the picker.</param>
    /// <param name="visual">A Visual range ends when the picker opens.</param>
    /// <param name="view">Shows and hides the picker.</param>
    /// <param name="focus">Puts the focus back on the pane the tab names.</param>
    public PickerPresenter(
        PickerInput picker,
        KeyController keys,
        VisualRangeInput visual,
        MainView view,
        PaneFocusCoordinator focus)
    {
        _picker = picker;
        _keys = keys;
        _visual = visual;
        _view = view;
        _focus = focus;
    }

    /// <summary>Follows the picker state after every state change.</summary>
    public void OnStateChanged()
    {
        var isOpen = _picker.IsOpen;
        if (isOpen)
        {
            _visual.End();
        }

        if (isOpen == _pickerShown)
        {
            return;
        }

        _pickerShown = isOpen;
        _view.ShowPicker(isOpen);
        if (!isOpen)
        {
            // Hiding the graph moved the toolkit focus: put it back on the pane the tab's state names.
            _focus.ReapplyFocus();
        }

        _view.RefreshChrome();
    }

    /// <summary>A typed key goes to the picker's filter in Insert mode.</summary>
    /// <param name="keyEvent">The key.</param>
    public void OnTextKey(KeyEvent keyEvent)
    {
        if (_keys.Mode == KeyMode.Insert)
        {
            _picker.TypeKey(keyEvent);
        }
    }

    /// <summary>A click highlights the row; the double-click's "open" comes from the mouse map as picker.accept.</summary>
    /// <param name="index">The row.</param>
    public void OnRowClicked(int index) => _picker.ClickRow(index);
}
