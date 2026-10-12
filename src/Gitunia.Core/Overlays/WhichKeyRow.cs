namespace Gitunia.Core.Overlays;

/// <summary>One line of the which-key popup: a key that can follow the pending prefix.</summary>
/// <param name="Key">The key as shown.</param>
/// <param name="Description">The action it completes, or <c>+more</c> when it only opens a longer chord.</param>
public sealed record WhichKeyRow(string Key, string Description);
