using Gitunia.Core.Input;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Tui.Input;

/// <summary>Offers a key typed in Insert or Command mode to the text field handlers in the routing order.</summary>
/// <param name="handlers">The typed-key handler of each case.</param>
public sealed class TextKeyRouter(IReadOnlyDictionary<InputHandler, ITextKeyHandler> handlers) : ITextKeyHandler
{
    /// <inheritdoc />
    public bool HandleTextKey(KeyEvent key) =>
        InputRouting.TextKeyHandlers().Any(handler => handlers[handler].HandleTextKey(key));
}
