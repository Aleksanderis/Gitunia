using Gitunia.Git;

namespace Gitunia.Core.Search.Models;

/// <summary>A parsed graph search: what to match and the text to look for (never empty).</summary>
/// <param name="Field">The field the text is matched against.</param>
/// <param name="Text">The text; compared without regard to case.</param>
/// <param name="BodyHits">
/// For <see cref="SearchField.Body"/>: the commits whose message contains the text, null until git has answered.
/// </param>
public sealed record SearchQuery(SearchField Field, string Text, IReadOnlySet<ObjectId>? BodyHits = null)
{
    /// <summary>A body query that still waits for git's answer; no row matches it yet.</summary>
    public bool IsAwaitingBodyHits => Field == SearchField.Body && BodyHits is null;
}
