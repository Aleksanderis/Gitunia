namespace Gitunia.Core.Overlays;

/// <summary>A palette entry with the sort keys used to order the list.</summary>
/// <param name="Entry">The entry.</param>
/// <param name="Rank">Distance of the action's view from the current view; lower sorts first.</param>
/// <param name="Order">Position in the binding table, used to keep the order stable.</param>
public readonly record struct RankedPaletteEntry(PaletteEntry Entry, int Rank, int Order);
