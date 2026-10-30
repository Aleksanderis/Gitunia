using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

namespace Gitunia.Tui.Input;

/// <summary>
/// <c>Ctrl-c</c>, the last-resort quit (#856). It is matched before the key controller, so no mode, modal, picker or prompt
/// (and no user binding) can swallow it.
/// </summary>
public static class ForceQuitKey
{
    /// <summary>True for a plain Ctrl-c (not Ctrl-Alt-c).</summary>
    public static bool IsMatch(Key key) => key.IsCtrl && !key.IsAlt && key.NoCtrl.NoShift.KeyCode == KeyCode.C;
}
