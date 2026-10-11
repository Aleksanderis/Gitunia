using Gitunia.Core.Keymap;

namespace Gitunia.Core.Lists;

/// <summary>Pure cursor movement for list views (<c>j</c>, <c>gg</c>, <c>5G</c>, <c>Ctrl-d</c> ...), shared by every list.</summary>
public static class ListMotion
{
    /// <summary>
    /// Computes the selection after a list action. Returns false when <paramref name="action"/> is not a list motion.
    /// </summary>
    /// <param name="action">The bound action.</param>
    /// <param name="count">The numeric prefix, or null when none was typed.</param>
    /// <param name="selected">The current selection (0 for an empty list).</param>
    /// <param name="itemCount">Number of items; the result is clamped to the list.</param>
    /// <param name="pageHeight">Visible rows, for the half-page motions.</param>
    /// <param name="target">The new selection, always inside the list when it has items.</param>
    public static bool TryMove(ActionId action, int? count, int selected, int itemCount, int pageHeight, out int target)
    {
        target = selected;
        var last = Math.Max(0, itemCount - 1);
        var steps = Math.Max(1, count ?? 1);
        var halfPage = Math.Max(1, pageHeight / 2) * steps;

        if (action == ActionIds.ListDown)
        {
            target = selected + steps;
        }
        else if (action == ActionIds.ListUp)
        {
            target = selected - steps;
        }
        else if (action == ActionIds.ListHalfPageDown)
        {
            target = selected + halfPage;
        }
        else if (action == ActionIds.ListHalfPageUp)
        {
            target = selected - halfPage;
        }
        else if (action == ActionIds.ListTop)
        {
            target = count is { } line ? line - 1 : 0;
        }
        else if (action == ActionIds.ListBottom)
        {
            target = count is { } lineNumber ? lineNumber - 1 : last;
        }
        else
        {
            return false;
        }

        target = Math.Clamp(target, 0, last);
        return true;
    }
}
