using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.WorkingTree;

/// <summary>
/// The button at the right end of a file list's title row: <c>stage all</c> on the Unstaged list and <c>unstage all</c> on the
/// Staged one, drawn as a padded chip like the tabs. They are the mouse path of <c>&lt;space&gt;A</c> and <c>&lt;space&gt;U</c>. The
/// button is left out when the title and the button do not fit the row.
/// </summary>
public static class WorkingListTitleButtons
{
    private const string StageAllLabel = " stage all ";
    private const string UnstageAllLabel = " unstage all ";

    // The title row starts with a stretch of line and the title chip (" title ", padded), then at least one column of line follows it.
    private const int TitleOverhead = 4;

    // One column of line stays between the button and the corner of the box.
    private const int EndGap = 1;

    /// <summary>The chip of a button, in the ASCII tier with brackets in place of its padding.</summary>
    public static StyledLine Line(TitleButton button, bool isAscii) =>
        new([new StyledSegment(ButtonLabel.For(button.Label, isAscii), SegmentRole.RepositoryTab)]);

    /// <summary>The button of a list, or null when the row of <paramref name="paneWidth"/> columns is too narrow for it after <paramref name="title"/>.</summary>
    /// <param name="section">The list.</param>
    /// <param name="title">The title text of the list, as <see cref="WorkingTreePaneRenderer.ListTitle"/> gives it.</param>
    /// <param name="paneWidth">Columns of the list's content (the title row is one column wider on each side).</param>
    /// <returns>The button, with its start column counted from the first content column.</returns>
    public static TitleButton? Layout(WorkingTreeSection section, string title, int paneWidth)
    {
        var isStaged = section == WorkingTreeSection.Staged;
        var label = isStaged ? UnstageAllLabel : StageAllLabel;
        var start = paneWidth - TextWidth.Of(label) - EndGap;
        if (start < TextWidth.Of(title) + TitleOverhead + 1)
        {
            return null;
        }

        return new TitleButton(isStaged ? DefaultMouseMap.UnstageAllButton : DefaultMouseMap.StageAllButton, label, start);
    }
}
