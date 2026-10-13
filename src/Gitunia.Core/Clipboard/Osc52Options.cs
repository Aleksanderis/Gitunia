namespace Gitunia.Core.Clipboard;

/// <summary>Delivery settings for OSC 52, decided once from the environment.</summary>
/// <param name="Mode">How the sequence is wrapped.</param>
/// <param name="MaximumEncodedLength">Largest base64 payload to send; bigger copies are refused (xterm drops more than 100000).</param>
/// <param name="UnsupportedReason">Why <see cref="Osc52Mode.Unsupported"/> was chosen, otherwise null.</param>
public sealed record Osc52Options(Osc52Mode Mode, int MaximumEncodedLength, string? UnsupportedReason = null);
