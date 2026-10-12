using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces;
using Gitunia.Tui.Rendering;

namespace Gitunia.Tui.App;

/// <summary>
/// The keyboard path of pane resizing (<c>Ctrl-w &gt;</c>, <c>Ctrl-w &lt;lt&gt;</c>, <c>Ctrl-w +</c>, <c>Ctrl-w -</c>, <c>Ctrl-w =</c>): turns the action into new
/// <see cref="PaneSizes"/> with <see cref="PaneResizer"/> and hands them to <paramref name="apply"/>. Dragging a divider with the mouse ends in the same sizes.
/// </summary>
/// <param name="arrangement">The active tab's pane arrangement.</param>
/// <param name="focusedPane">The pane that has focus.</param>
/// <param name="screenSize">The terminal size in columns and rows.</param>
/// <param name="apply">Stores the new sizes and lays the panes out again.</param>
public sealed class PaneSizeController(Func<PaneArrangement> arrangement, Func<PaneKind> focusedPane, Func<(int Width, int Height)> screenSize, Action<PaneSizes> apply)
{
    /// <summary>A short notice for the status bar from the last action, empty when none.</summary>
    public string Notice { get; private set; } = string.Empty;

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (!IsResizeAction(action))
        {
            return false;
        }

        var current = arrangement();
        if (current.Zoomed is not null)
        {
            Notice = "Unzoom the pane to resize";
            return true;
        }

        var (width, height) = screenSize();
        if (PaneResizer.Apply(current.Sizes ?? PaneSizes.Automatic, action, count, focusedPane(), width, height, current.IsDiffOpen) is { } sizes)
        {
            apply(sizes);
        }
        else
        {
            Notice = action == ActionIds.PaneResetSizes ? "Pane sizes are already automatic" : "This pane cannot be resized further";
        }

        return true;
    }

    private static bool IsResizeAction(ActionId action) =>
        action == ActionIds.PaneWider
        || action == ActionIds.PaneNarrower
        || action == ActionIds.PaneTaller
        || action == ActionIds.PaneShorter
        || action == ActionIds.PaneResetSizes;
}
