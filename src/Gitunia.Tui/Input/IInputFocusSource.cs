using Gitunia.Core.Input;

namespace Gitunia.Tui.Input;

/// <summary>Tells what the toolkit and the overlay layer know about the focus, which state does not hold.</summary>
public interface IInputFocusSource
{
    /// <summary>The open overlay and the focused list, as of now.</summary>
    InputFocus Current();
}
