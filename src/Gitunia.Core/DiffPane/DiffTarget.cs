using Gitunia.Git;

namespace Gitunia.Core.DiffPane;

/// <summary>The diff the selected commit's details ask for.</summary>
/// <param name="Commit">The commit the diff shows: the selected one, or the newer commit of a comparison.</param>
/// <param name="Parent">The parent the diff is read against, null for a root commit.</param>
/// <param name="IsAlternateParent">The parent is a later parent of a merge or the older commit of a comparison.</param>
/// <param name="IsComparison">The diff compares two commits, so the neighbours are not prefetched.</param>
public sealed record DiffTarget(ObjectId Commit, ObjectId? Parent, bool IsAlternateParent, bool IsComparison);
