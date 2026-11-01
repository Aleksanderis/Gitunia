namespace Gitunia.Core.Staging;

/// <summary>What a stage or unstage will do, or why it will not.</summary>
/// <param name="Paths">Paths to hand to git; empty when refused.</param>
/// <param name="Name">The printable name of the one file, empty for several.</param>
/// <param name="Count">Files affected.</param>
/// <param name="Skipped">Conflicted files left out.</param>
/// <param name="Notice">Why nothing runs, empty when something does.</param>
public sealed record StagingPlan(IReadOnlyList<string> Paths, string Name, int Count, int Skipped, string Notice)
{
    /// <summary>The plan marks a conflicted file resolved (an <c>add</c>), so the report says so.</summary>
    public bool IsResolve { get; init; }

    /// <summary>The plan stages a file the conflict editor just saved with every conflict resolved.</summary>
    public bool IsSavedResolve { get; init; }

    /// <summary>A plan that refuses with <paramref name="notice"/>.</summary>
    public static StagingPlan Refused(string notice) => new([], string.Empty, 0, 0, notice);

    /// <summary>A plan that runs.</summary>
    public static StagingPlan For(IReadOnlyList<string> paths, string name, int count, int skipped) =>
        new(paths, name, count, skipped, string.Empty);
}
