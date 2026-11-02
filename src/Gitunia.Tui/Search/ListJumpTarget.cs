using Gitunia.Core.Jump.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.Views;

namespace Gitunia.Tui.Search;

/// <summary>An <see cref="IJumpTarget"/> over a list view and its source: the source labels rows, the list moves its cursor.</summary>
/// <param name="pane">The pane the list is.</param>
/// <param name="source">Labels the rows and draws the typed text.</param>
/// <param name="list">The list that shows the rows.</param>
/// <param name="leadingRows">List items before the first jumpable row.</param>
public sealed class ListJumpTarget(PaneKind pane, IJumpSource source, ListView list, int leadingRows)
    : IJumpTarget
{
    /// <inheritdoc />
    public PaneKind Pane => pane;

    /// <inheritdoc />
    public string? UnavailableReason => null;

    /// <inheritdoc />
    public IReadOnlyList<JumpLabel> Labels(string text) => source.JumpLabels(list, text);

    /// <inheritdoc />
    public void ShowLabels(string? text)
    {
        source.JumpText = text;
        list.SetNeedsDraw();
    }

    /// <inheritdoc />
    public void JumpTo(int row)
    {
        list.SelectedItem = row + leadingRows;
        list.EnsureSelectedItemVisible();
    }
}
