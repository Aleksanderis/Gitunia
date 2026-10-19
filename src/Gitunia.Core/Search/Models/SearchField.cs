namespace Gitunia.Core.Search.Models;

/// <summary>What a graph search query is matched against.</summary>
public enum SearchField
{
    /// <summary>The commit subject (plain text, no prefix).</summary>
    Message,

    /// <summary>The author name (<c>author:</c> prefix).</summary>
    Author,

    /// <summary>The start of the commit hash (<c>hash:</c> prefix).</summary>
    Hash,

    /// <summary>
    /// The whole commit message, subject and body (<c>body:</c> prefix); answered by git, not by the loaded rows.
    /// </summary>
    Body,
}
