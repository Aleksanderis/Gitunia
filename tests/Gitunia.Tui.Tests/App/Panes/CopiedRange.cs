using Gitunia.Core.Lists;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Tui.Tests.App.Panes;

/// <summary>A range the controller asked to copy.</summary>
/// <param name="Pane">The pane it was taken from.</param>
/// <param name="Range">The rows.</param>
public sealed record CopiedRange(PaneKind Pane, RowRange Range);
