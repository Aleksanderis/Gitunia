namespace Gitunia.Core.Search;

/// <summary>A parsed graph search: what to match and the text to look for (never empty).</summary>
/// <param name="Field">The field the text is matched against.</param>
/// <param name="Text">The text; compared without regard to case.</param>
public sealed record SearchQuery(SearchField Field, string Text);
