namespace Gitunia.Git.Conflicts.Models;

/// <summary>What <see cref="ConflictParser"/> made of a file: the parts, or the reason it cannot.</summary>
/// <param name="Parts">The file as common text and hunks in file order; null when the markers are not usable.</param>
/// <param name="Problem">Why the markers are not usable; null when <paramref name="Parts"/> is set.</param>
public sealed record ConflictParseResult(IReadOnlyList<ConflictPart>? Parts, string? Problem);
