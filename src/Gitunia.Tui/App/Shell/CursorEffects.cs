using Gitunia.Core.Staging;
using Gitunia.App;
using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.Selection;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Search;

namespace Gitunia.Tui.App.Shell;

/// <summary>The shell effects that move a list cursor.</summary>
/// <param name="host">Tells which tab is shown.</param>
/// <param name="workingDiff">The Changes view diff.</param>
/// <param name="quickJump">The list the quick jump labelled.</param>
/// <param name="graph">The graph list.</param>
/// <param name="detail">The detail list.</param>
public sealed class CursorEffects(
    Dispatcher host,
    WorkingDiffController workingDiff,
    QuickJumpInput quickJump,
    GraphSelection graph,
    DetailPresenter detail) : ICursorEffects
{
    /// <summary>Moves the cursor of the Changes view diff of the tab; nothing when the tab is not the one shown.</summary>
    /// <param name="effect">The move.</param>
    public void MoveDiffCursor(MoveDiffCursorEffect effect)
    {
        if (host.Workspace.ActiveTabId == effect.TabId)
        {
            workingDiff.MoveAfterReload(effect.Move);
        }
    }

    /// <summary>Moves the cursor of the list the quick jump labelled to a row.</summary>
    /// <param name="row">The row.</param>
    public void JumpToRow(int row) => quickJump.JumpTo(row);

    /// <summary>Moves the graph cursor to a graph index.</summary>
    /// <param name="index">The index.</param>
    public void SelectGraphIndex(int index) => graph.SelectIndex(index);

    /// <summary>Moves the cursor of the detail list to a row.</summary>
    /// <param name="row">The row.</param>
    public void MoveDetailCursor(int row) => detail.MoveCursor(row);
}
