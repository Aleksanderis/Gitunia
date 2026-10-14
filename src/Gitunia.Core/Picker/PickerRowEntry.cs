namespace Gitunia.Core.Picker;

/// <summary>A picker row with its position in the unfiltered list, used to keep the order stable when ranking.</summary>
/// <param name="Row">The row.</param>
/// <param name="Order">Zero-based position before filtering.</param>
public sealed record PickerRowEntry(PickerRow Row, int Order);
