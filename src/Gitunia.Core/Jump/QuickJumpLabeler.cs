using Gitunia.Core.Graph;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Search;

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

    private static readonly JumpLabel[] None = [];

    /// <summary>
    /// The targets for <paramref name="text"/> in rows <paramref name="firstRow"/> to <paramref name="firstRow"/> +
    /// <paramref name="rowCount"/> - 1, nearest to <paramref name="cursorRow"/> first. Empty text has no targets.
    /// </summary>
    public static IReadOnlyList<JumpLabel> Compute(string text, CommitGraph graph, int firstRow, int rowCount, int cursorRow)
    {
        if (text.Length == 0 || rowCount <= 0)
        {
            return None;
        }

        var first = Math.Max(0, firstRow);
        var last = Math.Min(graph.Count, firstRow + rowCount);
        var targets = new List<JumpLabel>();
        var blocked = new HashSet<char>();
        for (var row = first; row < last; row++)
        {
            var subject = graph[row].Subject;
            var start = subject.IndexOf(text, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                continue;
            }

            targets.Add(new JumpLabel(row, start, text.Length, JumpLabel.NoLetter));
            BlockContinuations(subject, text, start, blocked);
        }

        targets.Sort((left, right) => CompareDistance(left.Row, right.Row, cursorRow));
        AssignLetters(targets, blocked);
        return targets;
    }

    /// <summary>Applies a key to the prompt text: a label key jumps, any other key edits the text.</summary>
    public static QuickJumpUpdate Apply(string text, KeyEvent key, IReadOnlyList<JumpLabel> labels)
    {
        var normalized = key.Normalize();
        if (text.Length > 0 && normalized.IsPrintable && normalized.Name.Length == 1)
        {
            var letter = char.ToLowerInvariant(normalized.Name[0]);
            foreach (var label in labels)
            {
                if (label.HasLetter && label.Letter == letter)
                {
                    return new QuickJumpUpdate(text, label.Row);
                }
            }
        }

        return new QuickJumpUpdate(PromptTextEditor.Apply(text, key) ?? text, null);
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

    private static void AssignLetters(List<JumpLabel> targets, HashSet<char> blocked)
    {
        var letters = Alphabet.Where(letter => !blocked.Contains(letter)).ToArray();
        for (var index = 0; index < targets.Count && index < letters.Length; index++)
        {
            targets[index] = targets[index] with { Letter = letters[index] };
        }
    }

    private static int CompareDistance(int left, int right, int cursorRow)
    {
        var byDistance = Math.Abs(left - cursorRow).CompareTo(Math.Abs(right - cursorRow));
        return byDistance != 0 ? byDistance : left.CompareTo(right);
    }
}
