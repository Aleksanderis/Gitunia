namespace Gitunia.Core.Mouse;

/// <summary>What the user did with the mouse, independent of the terminal toolkit.</summary>
public enum MouseGesture
{
    /// <summary>A left click.</summary>
    Click,

    /// <summary>A left double-click (the first click of it was already reported as a <see cref="Click"/>).</summary>
    DoubleClick,

    /// <summary>One wheel notch away from the user (the selection moves up).</summary>
    WheelUp,

    /// <summary>One wheel notch toward the user (the selection moves down).</summary>
    WheelDown,
}
