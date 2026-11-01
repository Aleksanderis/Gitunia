using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Views;

/// <summary>What the main view draws in its chrome: the model of the screen and the text of the pane titles, read on each draw.</summary>
public interface IScreenContent
{
    /// <summary>The model of the whole screen (tab row, status bar, banner, picker).</summary>
    ScreenModel Model();

    /// <summary>The title row of the detail pane.</summary>
    /// <param name="width">The columns of the row.</param>
    StyledLine DetailTitle(int width);

    /// <summary>The title row of the diff pane.</summary>
    /// <param name="width">The columns of the row.</param>
    StyledLine DiffTitle(int width);

    /// <summary>The filter line of the sidebar.</summary>
    SidebarChrome SidebarFilter();

    /// <summary>The title of the diff box of the Changes view: side, hunk and file.</summary>
    string WorkingDiffTitle();
}
