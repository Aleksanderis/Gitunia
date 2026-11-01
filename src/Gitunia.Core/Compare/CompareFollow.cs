using Gitunia.Git;

namespace Gitunia.Core.Compare;

/// <summary>What the comparison of one tab remembers between details changes.</summary>
/// <param name="Followed">The commit a comparison was asked for, so the same one is not asked for twice.</param>
/// <param name="ShownChanges">The Changes tab was shown for this mark already, so moving does not keep switching to it.</param>
public sealed record CompareFollow(ObjectId? Followed, bool ShownChanges)
{
    /// <summary>Nothing asked for, Changes not shown yet.</summary>
    public static CompareFollow None { get; } = new(null, false);
}
