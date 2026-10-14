namespace Gitunia.Core.Overlays;

/// <summary>A palette entry with its fuzzy match score for the current query.</summary>
/// <param name="Entry">The entry.</param>
/// <param name="Score">The best score, null when the entry does not match.</param>
public readonly record struct ScoredPaletteEntry(PaletteEntry Entry, int? Score);
