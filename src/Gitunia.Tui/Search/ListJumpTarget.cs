using Gitunia.Core.Jump.Models;
using Gitunia.Core.Workspaces.Panes;

namespace Gitunia.Tui.Search;

/// <summary>An <see cref="IJumpTarget"/> made of what a list view offers: how it labels rows, shows them and moves its cursor.</summary>
/// <param name="pane">The pane the list is.</param>
/// <param name="labels">Works out the labels of a typed text.</param>
/// <param name="showLabels">Starts or stops drawing the labels (null stops).</param>
/// <param name="jumpTo">Moves the cursor to a row.</param>
/// <param name="unavailableReason">Why the list cannot be jumped in right now, null when it can; null for always.</param>
public sealed class ListJumpTarget(
    PaneKind pane,
    Func<string, IReadOnlyList<JumpLabel>> labels,
    Action<string?> showLabels,
    Action<int> jumpTo,
    Func<string?>? unavailableReason = null) : IJumpTarget
{
    public PaneKind Pane => pane;

    public string? UnavailableReason => unavailableReason?.Invoke();

    public IReadOnlyList<JumpLabel> Labels(string text) => labels(text);

    public void ShowLabels(string? text) => showLabels(text);

    public void JumpTo(int row) => jumpTo(row);
}
