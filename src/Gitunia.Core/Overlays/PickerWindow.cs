namespace Gitunia.Core.Overlays;

/// <summary>The visible rows of a candidate list in a prompt popup.</summary>
/// <param name="Rows">The rows shown.</param>
/// <param name="Highlight">The row drawn as the selection bar; -1 for none.</param>
/// <param name="Position">The position of the highlight in the whole list (<c>n/m</c>); null for none.</param>
public sealed record PickerWindow(IReadOnlyList<string> Rows, int Highlight = -1, string? Position = null);
