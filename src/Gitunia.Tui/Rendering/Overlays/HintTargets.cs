using Gitunia.Core.Hints;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Overlays;

/// <summary>
/// The click targets of the hints a row drew (<see cref="HintBar.Append"/> gave their columns): the key of each
/// alternative of a hint (<c>j/k</c>) runs its own action, the rest of the hint (the label) runs the first. Hints without
/// actions (a note, a chord group) have no target.
/// </summary>
public static class HintTargets
{
    private const string Alternative = "/";

    /// <summary>The targets of the hints of one row.</summary>
    /// <param name="hints">The hints in the order they were drawn.</param>
    /// <param name="spans">The columns of each, as <see cref="HintBar.Append"/> returned them.</param>
    /// <param name="line">The frame line of the row.</param>
    /// <param name="offset">The column of the builder's first column in the frame (1 inside a box, 0 on the status
    /// bar).</param>
    public static List<FrameTarget> Of(
        IReadOnlyList<KeyHint> hints,
        IReadOnlyList<HintSpan> spans,
        int line,
        int offset)
    {
        var targets = new List<FrameTarget>();
        for (var index = 0; index < hints.Count && index < spans.Count; index++)
        {
            if (spans[index].Width > 0 && !hints[index].Actions.IsDefaultOrEmpty)
            {
                AddHint(targets, hints[index], spans[index], line, offset);
            }
        }

        return targets;
    }

    private static void AddHint(List<FrameTarget> targets, KeyHint hint, HintSpan span, int line, int offset)
    {
        var start = span.Start + offset;
        var end = start + span.Width;
        var alternatives = hint.Keys == Alternative ? [hint.Keys] : hint.Keys.Split(Alternative[0]);
        if (hint.Actions.Length == 1 || alternatives.Length != hint.Actions.Length)
        {
            targets.Add(new FrameTarget(line, start, end, hint.Actions[0]));

            return;
        }

        var column = start;
        for (var index = 0; index < alternatives.Length; index++)
        {
            var width = TextWidth.Of(alternatives[index]);
            targets.Add(new FrameTarget(line, column, Math.Min(end, column + width), hint.Actions[index]));
            column += width + Alternative.Length;
        }

        if (column < end)
        {
            targets.Add(new FrameTarget(line, column, end, hint.Actions[0]));
        }
    }
}
