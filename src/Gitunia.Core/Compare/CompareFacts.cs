using Gitunia.Git;

namespace Gitunia.Core.Compare;

/// <summary>What the details store and the graph show when the details changed.</summary>
/// <param name="IsLoaded">The details of <paramref name="Commit"/> are loaded.</param>
/// <param name="Commit">The commit the details are for, null when none.</param>
/// <param name="IsComparison">The shown details are already a comparison.</param>
/// <param name="MarkIndex">The graph index of the mark, negative when it is not in the graph.</param>
/// <param name="CommitIndex">The graph index of <paramref name="Commit"/>, negative when it is not in the graph.</param>
public sealed record CompareFacts(bool IsLoaded, ObjectId? Commit, bool IsComparison, int MarkIndex, int CommitIndex);
