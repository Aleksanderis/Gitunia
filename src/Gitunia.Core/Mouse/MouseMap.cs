namespace Gitunia.Core.Mouse;

/// <summary>
/// Maps mouse gestures to the action ids the keymap produces, so a mouse path and a key path end in the same action
/// handlers. Views inherit like in the keymap: a gesture the view does not bind is looked up in its parent.
/// </summary>
public sealed class MouseMap
{
    private readonly IReadOnlyDictionary<string, string> _viewParents;

    public MouseMap(IEnumerable<MouseBinding> bindings, IReadOnlyDictionary<string, string>? viewParents = null)
    {
        Bindings = [.. bindings];
        _viewParents = viewParents ?? new Dictionary<string, string>();
    }

    public IReadOnlyList<MouseBinding> Bindings { get; }

    /// <summary>The binding for a gesture on a view, searching the view's ancestors; null when the gesture does nothing there.</summary>
    public MouseBinding? Resolve(string view, MouseGesture gesture)
    {
        var current = view;
        var visited = new HashSet<string>();
        while (visited.Add(current))
        {
            foreach (var binding in Bindings)
            {
                if (binding.View == current && binding.Gesture == gesture)
                {
                    return binding;
                }
            }

            if (!_viewParents.TryGetValue(current, out var parent))
            {
                break;
            }

            current = parent;
        }

        return null;
    }
}
