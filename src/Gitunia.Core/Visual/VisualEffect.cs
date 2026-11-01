using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Core.Visual;

/// <summary>Copy the rows of a range to the clipboard (the terminal layer owns the rows' text); answers with <see cref="VisualCopied"/>.</summary>
/// <param name="Pane">The pane the range is in.</param>
/// <param name="Range">The rows.</param>
public sealed record CopyVisualRangeEffect(PaneKind Pane, RowRange Range) : AppEffect;
