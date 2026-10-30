namespace Gitunia.Core.Overlays;

/// <summary>One line of the which-key popup: a key that can follow the pending prefix.</summary>
/// <param name="Key">The key as shown.</param>
/// <param name="Description">The action it completes, or <c>+more</c> when it only opens a longer chord.</param>
/// <param name="GapBefore">Whether a blank line precedes the row: it starts a new cluster of related actions (#838).</param>
public sealed record WhichKeyRow(string Key, string Description, bool GapBefore = false);
