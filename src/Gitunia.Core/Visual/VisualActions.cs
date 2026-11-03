using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Text;

namespace Gitunia.Core.Visual;

/// <summary>The actions of character-wise selection and what each does to the cursor column. Pure.</summary>
public static class VisualActions
{
    /// <summary>The character-wise key, the motions and the word and line selections.</summary>
    /// <param name="action">The action.</param>
    public static bool IsTextAction(ActionId action) =>
        action == ActionIds.VisualToggleChars
        || action == ActionIds.VisualSelectWord
        || action == ActionIds.VisualSelectLine
        || IsMotion(action);

    /// <summary>One of the motions inside a line (<c>h</c> <c>l</c> <c>w</c> <c>b</c> <c>0</c> <c>$</c>).</summary>
    /// <param name="action">The action.</param>
    public static bool IsMotion(ActionId action) =>
        action == ActionIds.VisualCharLeft
        || action == ActionIds.VisualCharRight
        || action == ActionIds.VisualWordNext
        || action == ActionIds.VisualWordPrevious
        || action == ActionIds.VisualLineStart
        || action == ActionIds.VisualLineEnd;

    /// <summary>The column a motion leads to.</summary>
    /// <param name="action">A motion action.</param>
    /// <param name="text">The text of the cursor line.</param>
    /// <param name="column">The cursor column now.</param>
    /// <param name="count">How many steps (characters or words).</param>
    public static int MoveColumn(ActionId action, string text, int column, int count)
    {
        if (action == ActionIds.VisualCharLeft)
        {
            return TextMotions.Left(text, column, count);
        }

        if (action == ActionIds.VisualCharRight)
        {
            return TextMotions.Right(text, column, count);
        }

        if (action == ActionIds.VisualWordNext)
        {
            return TextMotions.WordForward(text, column, count);
        }

        if (action == ActionIds.VisualWordPrevious)
        {
            return TextMotions.WordBack(text, column, count);
        }

        return action == ActionIds.VisualLineEnd ? TextMotions.LineEnd(text) : 0;
    }
}
