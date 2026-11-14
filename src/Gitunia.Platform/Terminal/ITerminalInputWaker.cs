namespace Gitunia.Platform.Terminal;

/// <summary>
/// Ends a blocking read of the console input that the toolkit's input thread started and cannot be asked to leave
/// (#1369: on Windows its read waits for a byte after an availability check, and the one wake-up the toolkit sends while
/// it shuts down is lost when it arrives before the read has begun).
/// </summary>
public interface ITerminalInputWaker
{
    /// <summary>
    /// Makes a pending or imminent blocking read of the console input return; does nothing where reads can be cancelled.
    /// </summary>
    void Wake();
}
