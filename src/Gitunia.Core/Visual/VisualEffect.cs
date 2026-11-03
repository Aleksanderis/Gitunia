using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Core.Visual;

/// <summary>Copy the rows (or characters) of a selection to the clipboard (the terminal layer owns the text); answers with <see cref="VisualCopied"/>.</summary>
/// <param name="Pane">The pane the selection is in.</param>
/// <param name="Range">The rows.</param>
/// <param name="Chars">The characters of a character-wise selection, null for whole rows.</param>
public sealed record CopyVisualRangeEffect(PaneKind Pane, RowRange Range, TextSelection? Chars = null) : AppEffect;
