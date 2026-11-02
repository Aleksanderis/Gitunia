namespace Gitunia.Tui.Views;

/// <summary>Tells the pane widgets whether something modal owns the screen, so they swallow the mouse.</summary>
public interface IMouseGate
{
    /// <summary>True while an overlay is open.</summary>
    bool IsSuppressed { get; }
}
