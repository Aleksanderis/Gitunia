namespace Gitunia.Tui.Input;

/// <summary>The keymap view of the focused pane.</summary>
public interface IFocusedView
{
    /// <summary>The name of the view the keymap looks bindings up in.</summary>
    string Name { get; }
}
