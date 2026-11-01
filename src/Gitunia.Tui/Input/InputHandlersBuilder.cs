using Gitunia.Core.Input;

namespace Gitunia.Tui.Input;

/// <summary>Collects the object behind each <see cref="InputHandler"/> case, then builds the table once.</summary>
public sealed class InputHandlersBuilder
{
    private readonly Dictionary<InputHandler, IActionHandler> _actions = [];
    private readonly Dictionary<InputHandler, ITextKeyHandler> _textKeys = [];

    /// <summary>Registers the object for a case, as an action handler, a text key handler or both.</summary>
    /// <param name="handler">The case.</param>
    /// <param name="target">The object that does the work.</param>
    /// <returns>This builder.</returns>
    public InputHandlersBuilder Add(InputHandler handler, object target)
    {
        var isHandler = false;
        if (target is IActionHandler action)
        {
            _actions[handler] = action;
            isHandler = true;
        }

        if (target is ITextKeyHandler textKey)
        {
            _textKeys[handler] = textKey;
            isHandler = true;
        }

        if (!isHandler)
        {
            throw new ArgumentException($"{target.GetType().Name} takes neither actions nor typed keys", nameof(target));
        }

        return this;
    }

    /// <summary>The typed-key handlers registered so far, as one handler that asks them in the routing order.</summary>
    public TextKeyRouter TextKeys()
    {
        foreach (var handler in InputRouting.TextKeyHandlers().Where(handler => !_textKeys.ContainsKey(handler)))
        {
            throw new InvalidOperationException($"No text key handler is registered for {handler}");
        }

        return new TextKeyRouter(new Dictionary<InputHandler, ITextKeyHandler>(_textKeys));
    }

    /// <summary>Builds the table; fails when a case the routing can name has no object behind it.</summary>
    public InputHandlers Build() => new(_actions, _textKeys);
}
