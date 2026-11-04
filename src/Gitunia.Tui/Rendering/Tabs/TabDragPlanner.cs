namespace Gitunia.Tui.Rendering.Tabs;

/// <summary>
/// Decides, while a tab chip is dragged along the tab row, whether the pointer has crossed the edge of a neighbour and the
/// dragged tab must swap places with it. The swap needs the pointer to be on the neighbour and beyond the part of it the
/// moved chip will cover, so chips of different widths never swap back and forth under a still pointer.
/// </summary>
public static class TabDragPlanner
{
    /// <summary>Which way the dragged tab moves for a pointer at <paramref name="column"/>: -1 left, 1 right, 0 stays.</summary>
    /// <param name="cells">The visible cells of the row, in order.</param>
    /// <param name="dragged">The tab number of the dragged chip.</param>
    /// <param name="column">The pointer column in the row.</param>
    public static int Step(IReadOnlyList<TabCell> cells, int dragged, int column)
    {
        var index = -1;
        for (var position = 0; position < cells.Count; position++)
        {
            if (cells[position].Number == dragged)
            {
                index = position;
            }
        }

        if (index < 0)
        {
            return 0;
        }

        var chip = cells[index];
        if (column < chip.Start && index > 0)
        {
            var previous = cells[index - 1];

            return column < previous.Start + Math.Min(chip.Width, previous.Width) ? -1 : 0;
        }

        if (column >= chip.Start + chip.Width && index < cells.Count - 1)
        {
            var next = cells[index + 1];

            return column > chip.Start + Math.Min(chip.Width, next.Width) ? 1 : 0;
        }

        return 0;
    }
}
