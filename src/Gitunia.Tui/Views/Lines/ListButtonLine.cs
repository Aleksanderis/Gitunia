using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Views.Lines;

/// <summary>One button on the title row of a file list in the Changes view.</summary>
/// <param name="buttons">The layout of the list title buttons.</param>
/// <param name="section">The list the button belongs to.</param>
/// <param name="kind">Which button it is.</param>
public sealed class ListButtonLine(WorkingListButtons buttons, WorkingTreeSection section, ListButtonKind kind)
    : ILineSource
{
    /// <inheritdoc />
    public StyledLine Line(int width) => buttons.Line(section, kind);
}
