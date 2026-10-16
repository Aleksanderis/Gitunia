namespace Gitunia.Core.Overlays;

/// <summary>Which modal overlay is open.</summary>
public enum OverlayKind
{
    None,
    Help,
    Palette,
    Messages,

    /// <summary>The output of a failed command (a failing commit hook), scrollable like the message history.</summary>
    Output,

    /// <summary>A yes/no question before a destructive action (<c>y</c>, <c>n</c>, <c>Esc</c>).</summary>
    Confirm,
}
