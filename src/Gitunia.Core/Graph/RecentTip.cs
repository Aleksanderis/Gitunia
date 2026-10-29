namespace Gitunia.Core.Graph;

/// <summary>A branch tip the layout saw lately: the row it sits on and the colour slot of its branch.</summary>
/// <param name="Row">Index of the row of the tip commit.</param>
/// <param name="Slot">Colour slot of the branch.</param>
public readonly record struct RecentTip(int Row, int Slot);
