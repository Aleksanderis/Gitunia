namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>The result of looking up a click on the settings overlay.</summary>
/// <param name="Kind">What was hit.</param>
/// <param name="Index">The category number or the row number in the settings column; 0 for the other kinds.</param>
public sealed record SettingsHit(SettingsHitKind Kind, int Index = 0)
{
    /// <summary>A click that lands on nothing.</summary>
    public static SettingsHit None { get; } = new(SettingsHitKind.None);
}
