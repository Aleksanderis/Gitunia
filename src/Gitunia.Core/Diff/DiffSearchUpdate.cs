namespace Gitunia.Core.Diff;

/// <summary>The result of a search transition: the new state and a row to move the cursor to, if any.</summary>
public sealed record DiffSearchUpdate(DiffSearchState State, int? RevealRow);
