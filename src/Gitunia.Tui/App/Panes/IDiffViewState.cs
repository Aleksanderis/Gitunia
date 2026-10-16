using Gitunia.Core.Diff;
using Gitunia.Tui.Views.Lists;

namespace Gitunia.Tui.App.Panes;

/// <summary>
/// What the diff list behaviour needs of a tab's diff: the list source, the search and the remembered cursor.
/// Implemented by the history and the working-tree diff sessions.
/// </summary>
public interface IDiffViewState
{
    /// <summary>The list source of the diff pane.</summary>
    DiffListSource Source { get; }

    DiffSearchState Search { get; set; }

    /// <summary>The cursor row, remembered so switching tabs and back keeps the place.</summary>
    int SelectedRow { get; set; }

    /// <summary>The first visible row, remembered with <see cref="SelectedRow"/>.</summary>
    int TopRow { get; set; }

    /// <summary>
    /// The row the last search step revealed, so the next step continues from it; null after any other cursor move.
    /// </summary>
    int? SearchRow { get; set; }
}
