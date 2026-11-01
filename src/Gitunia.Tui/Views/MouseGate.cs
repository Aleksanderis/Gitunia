namespace Gitunia.Tui.Views;

/// <summary>
/// The mouse gate of the pane widgets. The widgets are built before the objects that know when an overlay is open, so
/// the overlay cover reports the fact here on every change instead of being asked on every mouse event.
/// </summary>
public sealed class MouseGate : IMouseGate
{
    /// <inheritdoc />
    public bool IsSuppressed { get; private set; }

    /// <summary>Records whether an overlay is open.</summary>
    /// <param name="isSuppressed">True while one is.</param>
    public void Set(bool isSuppressed) => IsSuppressed = isSuppressed;
}
