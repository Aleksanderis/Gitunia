namespace Gitunia.Core.Mouse;

/// <summary>What the user did with the mouse, independent of the terminal toolkit.</summary>
public enum MouseGesture
{
    /// <summary>A left click.</summary>
    Click,

    /// <summary>A left double-click (the first click of it was already reported as a <see cref="Click"/>).</summary>
    DoubleClick,

    /// <summary>A right click: opens the actions menu of the row under the pointer.</summary>
    RightClick,

    /// <summary>A middle-button click: the same menu as <see cref="RightClick"/>, for terminals or multiplexers that keep the right button.</summary>
    MiddleClick,

    /// <summary>A left click with Ctrl held: a second right-click alternative.</summary>
    CtrlClick,

    /// <summary>One wheel notch away from the user (the selection moves up).</summary>
    WheelUp,

    /// <summary>One wheel notch toward the user (the selection moves down).</summary>
    WheelDown,

    /// <summary>The pointer moved with the left button held and left the row it pressed: a range selection starts at that row.</summary>
    DragStart,

    /// <summary>While dragging, the pointer is above the cursor row; the binding's count is how many rows above.</summary>
    DragUp,

    /// <summary>While dragging, the pointer is below the cursor row; the binding's count is how many rows below.</summary>
    DragDown,
}
