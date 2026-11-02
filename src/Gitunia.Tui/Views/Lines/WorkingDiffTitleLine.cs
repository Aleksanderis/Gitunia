using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Views.Lines;

/// <summary>The title row of the diff box in the Changes view.</summary>
/// <param name="parts">What the chrome is drawn from.</param>
public sealed class WorkingDiffTitleLine(ChromeParts parts) : ILineSource
{
    /// <inheritdoc />
    public StyledLine Line(int width)
    {
        var rect = parts.Geometry.WorkingRects().Diff;
        var line = DiffPaneRenderer.RenderWorkingTitle(
            parts.Content.WorkingDiffTitle(),
            WorkingPanes.IsDiff(parts.Geometry.WorkingFocus),
            parts.Geometry.WorkingDiffButtons(),
            width,
            parts.Panes.Content.Glyphs,
            parts.Panes.Content.Keys);

        return parts.Grids.Current().ResolveRules(line, rect.Y - 1, rect.X);
    }
}
