using Gitunia.Core.Input;

namespace Gitunia.Tui.Input;

/// <summary>
/// Which class does the work for each <see cref="InputHandler"/> case. The order they are asked in is
/// <see cref="InputRouting"/> (Core); this only maps a case to an object.
/// </summary>
public sealed class InputHandlers
{
    private readonly Dictionary<InputHandler, IActionHandler> _actions = [];
    private readonly Dictionary<InputHandler, ITextKeyHandler> _textKeys = [];

    /// <summary>Registers the object for a case, as an action handler, a text key handler or both.</summary>
    /// <param name="handler">The case.</param>
    /// <param name="target">The object that does the work.</param>
    public void Add(InputHandler handler, object target)
    {
        if (target is IActionHandler action)
        {
            _actions[handler] = action;
        }

        if (target is ITextKeyHandler textKey)
        {
            _textKeys[handler] = textKey;
        }
    }

    /// <summary>The object that takes actions for a case.</summary>
    /// <param name="handler">The case.</param>
    public IActionHandler ActionsOf(InputHandler handler)
    {
        if (!_actions.TryGetValue(handler, out var found))
        {
            throw new InvalidOperationException($"No action handler is registered for {handler}");
        }

        return found;
    }

    /// <summary>The object that takes typed keys for a case.</summary>
    /// <param name="handler">The case.</param>
    public ITextKeyHandler TextKeysOf(InputHandler handler)
    {
        if (!_textKeys.TryGetValue(handler, out var found))
        {
            throw new InvalidOperationException($"No text key handler is registered for {handler}");
        }

        return found;
    }
}
