namespace Gitunia.Platform.Urls;

/// <summary>Whether the system handed an address to the default browser.</summary>
/// <param name="Opened">True when the opener program started.</param>
/// <param name="Error">Why it did not, null when it did.</param>
public sealed record UrlOpenResult(bool Opened, string? Error = null);
