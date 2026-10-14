namespace Gitunia.Core.Picker;

/// <summary>A picker row that matched the query, with the sort keys used to rank it.</summary>
/// <param name="Row">The row.</param>
/// <param name="Score">Fuzzy match score; higher sorts first.</param>
/// <param name="Order">Zero-based position before filtering.</param>
public readonly record struct ScoredPickerRow(PickerRow Row, int Score, int Order);
