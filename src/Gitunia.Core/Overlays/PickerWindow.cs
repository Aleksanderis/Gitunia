namespace Gitunia.Core.Overlays;

/// <summary>The visible rows of a candidate list in a prompt popup.</summary>
/// <param name="Rows">The rows shown.</param>
/// <param name="Highlight">The row drawn as the selection bar; -1 for none.</param>
/// <param name="Position">The position of the highlight in the whole list (<c>n/m</c>); null for none.</param>
/// <param name="EmptyText">What the list says instead of rows (no match, loading, failure), drawn in the empty-text role; null when there are rows.</param>
public sealed record PickerWindow(
    IReadOnlyList<string> Rows,
    int Highlight = -1,
    string? Position = null,
    string? EmptyText = null);
