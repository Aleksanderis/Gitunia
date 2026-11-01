using Gitunia.Core.Input;

namespace Gitunia.Tui.Input;

/// <summary>
/// Which class does the work for each <see cref="InputHandler"/> case. The order they are asked in is
/// <see cref="InputRouting"/> (Core); this only maps a case to an object. Built once by <see cref="InputHandlersBuilder"/>.
/// </summary>
public sealed class InputHandlers
{
    private readonly IReadOnlyDictionary<InputHandler, IActionHandler> _actions;
    private readonly IReadOnlyDictionary<InputHandler, ITextKeyHandler> _textKeys;

    /// <summary>Fails when a case the routing can name has no object behind it.</summary>
    /// <param name="actions">The objects that take actions, by case.</param>
    /// <param name="textKeys">The objects that take typed keys, by case.</param>
    public InputHandlers(
        IReadOnlyDictionary<InputHandler, IActionHandler> actions,
        IReadOnlyDictionary<InputHandler, ITextKeyHandler> textKeys)
    {
        _actions = actions;
        _textKeys = textKeys;
        RequireComplete();
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

    // Fails at startup instead of on the first key that reaches the case (a failure inside the runner would only show
    // as a status message).
    private void RequireComplete()
    {
        foreach (var handler in Enum.GetValues<InputHandler>())
        {
            if (handler != InputHandler.CommitMessage)
            {
                ActionsOf(handler);
            }
        }

        foreach (var handler in InputRouting.TextKeyHandlers())
        {
            TextKeysOf(handler);
        }
    }
}
