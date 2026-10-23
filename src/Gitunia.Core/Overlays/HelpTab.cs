namespace Gitunia.Core.Overlays;

/// <summary>One page of the help overlay: the keys of one source (the focused view, <c>Lists</c>, <c>Global</c>) or the markers.</summary>
/// <param name="Title">The tab label.</param>
/// <param name="Sections">The groups of the page, each under its subheader.</param>
public sealed record HelpTab(string Title, IReadOnlyList<HelpSection> Sections)
{
    /// <summary>Every row of the page, across its groups.</summary>
    public IEnumerable<HelpRow> Rows => Sections.SelectMany(section => section.Rows);
}
