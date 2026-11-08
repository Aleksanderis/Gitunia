using System.Text;
using Gitunia.Core.Editing;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Text;

namespace Gitunia.Core.Jump;

/// <summary>
/// Finds the quick-jump targets among the visible rows and gives them keys (flash/easymotion style). Only visible
/// rows are looked at, so the cost does not depend on the length of the log. A key that would continue some match
/// in view is never a label, so typing more text and pressing a label cannot be confused.
/// </summary>
public static class QuickJumpLabeler
{
    /// <summary>Label keys, home row first.</summary>
    public const string Alphabet = "asdfghjklqwertyuiopzxcvbnm";

    // Marks a target as able to take a letter; AssignLetters hands the real letters out in order of distance.
    private const char Eligible = '\u0001';

    private static readonly JumpLabel[] None = [];

    /// <summary>
    /// The targets for <paramref name="text"/> among the rows in <paramref name="window"/>, nearest to the cursor first.
    /// Empty text has no targets. A match whose label would fall outside the columns of its row gets no letter.
    /// </summary>
    /// <param name="text">The typed text.</param>
    /// <param name="rows">The rows of the view.</param>
    /// <param name="window">The rows in view.</param>
    public static IReadOnlyList<JumpLabel> Compute(string text, IJumpRows rows, JumpWindow window)
    {
        if (text.Length == 0 || window.RowCount <= 0)
        {
            return None;
        }

        var first = Math.Max(0, window.FirstRow);
        var last = Math.Min(rows.Count, window.FirstRow + window.RowCount);
        var targets = new List<JumpLabel>();
        var blocked = new HashSet<char>();
        for (var row = first; row < last; row++)
        {
            var shown = rows.TextOf(row);
            var start = shown.IndexOf(text, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                continue;
            }

            var letter = FitsInRow(shown, start + text.Length, rows.ColumnsOf(row), rows.EllipsisWidth) ? Eligible : JumpLabel.NoLetter;
            targets.Add(new JumpLabel(row, start, text.Length, letter));
            BlockContinuations(shown, text, start, blocked);
        }

        targets.Sort((left, right) => CompareDistance(left.Row, right.Row, window.CursorRow));
        AssignLetters(targets, blocked);
        return targets;
    }

    /// <summary>Applies a key to the prompt text: a label key jumps, any other key edits the text.</summary>
    public static QuickJumpUpdate Apply(string text, int caret, KeyEvent key, IReadOnlyList<JumpLabel> labels)
    {
        var normalized = key.Normalize();
        if (text.Length > 0 && normalized.IsPrintable && normalized.Name.Length == 1)
        {
            var letter = char.ToLowerInvariant(normalized.Name[0]);
            foreach (var label in labels)
            {
                if (label.HasLetter && label.Letter == letter)
                {
                    return new QuickJumpUpdate(text, label.Row, caret);
                }
            }
        }

        if (PromptTextEditor.Apply(text, caret, key) is { } edit)
        {
            return new QuickJumpUpdate(edit.Text, null, edit.Caret);
        }

        return new QuickJumpUpdate(text, null, caret);
    }

    // Every character that follows an occurrence of the text in this subject (lowercase and uppercase forms).
    private static void BlockContinuations(string subject, string text, int firstStart, HashSet<char> blocked)
    {
        var start = firstStart;
        while (start >= 0)
        {
            var next = start + text.Length;
            if (next >= subject.Length)
            {
                return;
            }

            blocked.Add(char.ToLowerInvariant(subject[next]));
            blocked.Add(char.ToUpperInvariant(subject[next]));
            start = subject.IndexOf(text, start + 1, StringComparison.OrdinalIgnoreCase);
        }
    }

    // The label sits over the character after the match (or after the text when the match ends it): that column, and the
    // whole character when it is wide, must be inside the columns of the row, short of the ellipsis of a cut text.
    private static bool FitsInRow(string shown, int labelIndex, int columns, int ellipsisWidth)
    {
        var column = DisplayWidth.Of(shown[..Math.Min(labelIndex, shown.Length)]);
        var replacedWidth = 1;
        if (labelIndex < shown.Length)
        {
            Rune.DecodeFromUtf16(shown.AsSpan(labelIndex), out var replaced, out _);
            replacedWidth = Math.Max(1, DisplayWidth.Of(replaced));
        }

        var appended = labelIndex >= shown.Length ? 1 : 0;
        var limit = DisplayWidth.Of(shown) + appended <= columns ? columns : columns - ellipsisWidth;
        return column + replacedWidth <= limit;
    }

    private static void AssignLetters(List<JumpLabel> targets, HashSet<char> blocked)
    {
        var letters = Alphabet.Where(letter => !blocked.Contains(letter)).ToArray();
        var next = 0;
        for (var index = 0; index < targets.Count; index++)
        {
            if (targets[index].Letter != Eligible)
            {
                continue;
            }

            targets[index] = targets[index] with { Letter = next < letters.Length ? letters[next++] : JumpLabel.NoLetter };
        }
    }

    private static int CompareDistance(int left, int right, int cursorRow)
    {
        var byDistance = Math.Abs(left - cursorRow).CompareTo(Math.Abs(right - cursorRow));
        return byDistance != 0 ? byDistance : left.CompareTo(right);
    }
}
