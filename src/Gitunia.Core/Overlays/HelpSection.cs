namespace Gitunia.Core.Overlays;

/// <summary>The help rows of one view in the focused view's inheritance chain.</summary>
/// <param name="Title">The view name, such as <c>Graph</c> or <c>Global</c>.</param>
/// <param name="Rows">One row per action.</param>
public sealed record HelpSection(string Title, IReadOnlyList<HelpRow> Rows);
