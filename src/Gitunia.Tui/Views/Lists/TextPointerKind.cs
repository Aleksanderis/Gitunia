namespace Gitunia.Tui.Views.Lists;

/// <summary>What the pointer did on the text of a list pane.</summary>
public enum TextPointerKind
{
    /// <summary>A click (a single one, or the first of a burst).</summary>
    Click,

    /// <summary>A double-click.</summary>
    DoubleClick,

    /// <summary>A triple-click.</summary>
    TripleClick,

    /// <summary>The pointer left the pressed spot with the button held.</summary>
    DragStart,

    /// <summary>The pointer moved on with the button held.</summary>
    DragMove,
}
