namespace Gitunia.Git.Commits.Models;

/// <summary>The checked-out branch as the graph layout sees it.</summary>
/// <param name="Head">The commit the checked-out local branch points at; null for a detached or unborn HEAD.</param>
/// <param name="FirstParentDescendants">
/// The commits whose first-parent chain reaches <paramref name="Head"/> (not including it); empty when nothing is
/// above it, null when the read failed or there is no head (the layout then falls back to its plain rules).
/// </param>
public sealed record HeadLayoutInfo(ObjectId? Head, IReadOnlySet<ObjectId>? FirstParentDescendants)
{
    /// <summary>No head: the layout uses the leftmost free lane for everything.</summary>
    public static readonly HeadLayoutInfo None = new(null, null);
}
