using Gitunia.Tui.Rendering.Styling;
using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Overlays;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.Views;

/// <summary>Where the <c>view ▾</c> dropdowns hang: under their title buttons.</summary>
/// <param name="geometry">Where the panes are.</param>
/// <param name="buttons">The title buttons of the Changes view lists.</param>
/// <param name="panes">The lists and the keys and glyphs the buttons are drawn with.</param>
public sealed class MenuAnchors(PaneGeometry geometry, WorkingListButtons buttons, PaneViews panes)
{
    /// <summary>
    /// Where the <c>view ▾</c> dropdown hangs: under its title button, or under the right end of the title row when
    /// the row is too narrow for the button (the keys still open the menu).
    /// </summary>
    /// <param name="isWorking">The Changes view diff rather than the history diff.</param>
    public MenuAnchor ViewMenuAnchor(bool isWorking)
    {
        var keys = panes.Content.Keys;
        var rect = isWorking ? geometry.WorkingRects().Diff : geometry.Rects().DiffTitle;
        var row = isWorking ? rect.Y - 1 : rect.Y;
        var layout = DiffTitleButtons.Layout(rect.Width, keys);
        if (isWorking)
        {
            layout = DiffTitleButtons.Layout(rect.Width, geometry.WorkingDiffButtons(), keys);
        }

        var button = layout.FirstOrDefault(candidate => candidate.View == DefaultMouseMap.DiffViewButton);
        var column = button is null ? rect.X + Math.Max(0, rect.Width - 1) : rect.X + button.Start;

        return new MenuAnchor(column - 2, row);
    }

    /// <summary>
    /// Where the <c>view ▾</c> dropdown of a file list hangs: under its title chip, or under the right end of the
    /// title row when the row is too narrow for the chip (the keys still open the menu).
    /// </summary>
    /// <param name="view">The keymap view of the list: the Changes or Files tab, or a Changes view list.</param>
    public MenuAnchor ListViewMenuAnchor(string view)
    {
        if (view == DefaultKeymap.WorkingUnstagedView || view == DefaultKeymap.WorkingStagedView)
        {
            var section = WorkingTreeSection.Unstaged;
            if (view == DefaultKeymap.WorkingStagedView)
            {
                section = WorkingTreeSection.Staged;
            }

            var rect = geometry.ListRect(section);
            var chip = buttons.Button(section, ListButtonKind.View);

            return new MenuAnchor(
                rect.X + (chip?.Start ?? Math.Max(0, rect.Width - 1)) - 2,
                rect.Y - 1);
        }

        var title = geometry.Rects().DetailTitle;
        var button = default(TitleButton);
        if ((panes.Detail.Source as DetailListSource)?.Content is { } content)
        {
            button = DetailPaneRenderer.ViewButton(content, title.Width, panes.Content.Glyphs, panes.Content.Keys);
        }

        return new MenuAnchor(title.X + (button?.Start ?? Math.Max(0, title.Width - 1)) - 2, title.Y);
    }
}
