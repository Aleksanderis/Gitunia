namespace Gitunia.Core.Overlays;

/// <summary>The screen cell of the row an actions menu belongs to; the menu hangs just under it.</summary>
/// <param name="Column">Screen column of the row's left edge.</param>
/// <param name="Row">Screen row of the row.</param>
public readonly record struct MenuAnchor(int Column, int Row);
