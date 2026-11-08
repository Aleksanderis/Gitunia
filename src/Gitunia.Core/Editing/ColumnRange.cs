namespace Gitunia.Core.Editing;

/// <summary>A range of UTF-16 indexes in one line.</summary>
/// <param name="Start">The first index.</param>
/// <param name="End">The index after the last one.</param>
public readonly record struct ColumnRange(int Start, int End);
