namespace Gitunia.Core.Clipboard;

/// <summary>Outcome of building an OSC 52 sequence.</summary>
public enum Osc52Status
{
    Ready,
    Empty,
    TooLarge,
    Unsupported,
}
