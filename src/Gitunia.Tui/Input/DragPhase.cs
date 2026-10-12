namespace Gitunia.Tui.Input;

/// <summary>What a mouse event means for a drag in progress, as <see cref="DragTracker"/> sees it.</summary>
public enum DragPhase
{
    /// <summary>Not part of a drag; handle the event as usual.</summary>
    None,

    /// <summary>The left button went down.</summary>
    Press,

    /// <summary>The pointer left the cell where the button went down for the first time.</summary>
    Start,

    /// <summary>The pointer moved again with the button held.</summary>
    Move,

    /// <summary>The button was released after a drag.</summary>
    End,

    /// <summary>The click the terminal reports after the release of a drag; it is no click and must be ignored.</summary>
    SwallowClick,
}
