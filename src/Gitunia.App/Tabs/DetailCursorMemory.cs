using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tabs;

/// <summary>
/// Decides the cursor row of the detail pane and remembers, per bottom tab, the row last used for the commit shown. A different commit
/// starts at the landing row; the same commit whose details just arrived keeps the user's place; switching tabs restores that tab's row.
/// </summary>
public sealed class DetailCursorMemory
{
    private readonly Dictionary<BottomTab, int> _rows = [];

    /// <summary>The row the cursor goes to, or null when there are no rows.</summary>
    public int? Decide(DetailCursorRequest request)
    {
        var selected = request.CurrentRow ?? 0;
        if (request.ShortHash != request.PreviousShortHash)
        {
            _rows.Clear();
            selected = request.Landing;
        }
        else if (request.PreviousTab != request.Tab)
        {
            _rows[request.PreviousTab] = selected;
            selected = _rows.TryGetValue(request.Tab, out var remembered) ? remembered : request.Landing;
        }
        else if (request.PreviousFileCount == 0 && selected < request.Landing)
        {
            // The files of the commit just arrived: the cursor goes to the first one, whose diff is shown.
            selected = request.Landing;
        }

        return request.RowCount == 0 ? null : Math.Min(selected, request.RowCount - 1);
    }
}
