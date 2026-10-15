namespace Gitunia.Core.Diff;

/// <summary>The rows of a <see cref="DiffDocument"/> arranged in side-by-side lines.</summary>
/// <param name="Lines">The lines, top to bottom.</param>
/// <param name="LineOfRow">For every row of the document the index of the line it is shown on.</param>
public sealed record DiffSplit(IReadOnlyList<DiffSplitLine> Lines, IReadOnlyList<int> LineOfRow);
