namespace Gitunia.Tui.Input;

/// <summary>Tells when something other than the panes owns the keys: an overlay, a prompt, a search, the picker.</summary>
public interface IInputBlocker
{
    /// <summary>True while one does.</summary>
    bool IsBlocked { get; }
}
