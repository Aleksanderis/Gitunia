using Gitunia.Core.Staging;

namespace Gitunia.App;

/// <summary>Moves of a list cursor.</summary>
public interface ICursorEffects
{
    /// <summary>Moves the cursor of the Changes view diff of the tab after a staged hunk or range was read again; does
    /// nothing when the tab is not the one shown.</summary>
    void MoveDiffCursor(MoveDiffCursorEffect effect);

    /// <summary>Moves the cursor of the detail list to a row.</summary>
    void MoveDetailCursor(int row);

    /// <summary>Moves the cursor of the list the quick jump labelled to a row and scrolls it into view.</summary>
    void JumpToRow(int row);

    /// <summary>Moves the graph cursor to a graph index and scrolls it into view.</summary>
    void SelectGraphIndex(int index);
}
