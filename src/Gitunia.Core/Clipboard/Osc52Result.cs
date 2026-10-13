namespace Gitunia.Core.Clipboard;

/// <summary>The built sequence, or why none was built.</summary>
/// <param name="Status">Whether a sequence is ready.</param>
/// <param name="Sequence">The bytes to write to the terminal when ready, otherwise empty.</param>
/// <param name="Message">A user-facing explanation when not ready, otherwise empty.</param>
public sealed record Osc52Result(Osc52Status Status, string Sequence, string Message);
