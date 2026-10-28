namespace Gitunia.Core.Diff;

/// <summary>The changed stretches of two paired lines.</summary>
/// <param name="Old">Changed stretches of the old line, ascending and not overlapping.</param>
/// <param name="New">Changed stretches of the new line, ascending and not overlapping.</param>
public sealed record WordDiffResult(IReadOnlyList<ChangedSpan> Old, IReadOnlyList<ChangedSpan> New)
{
    /// <summary>Nothing to highlight on either side.</summary>
    public static WordDiffResult None { get; } = new([], []);
}
