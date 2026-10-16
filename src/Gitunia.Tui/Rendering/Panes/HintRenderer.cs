using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Panes;

/// <summary>Draws key hints (bold key, then its description) in the status bar style; shared by the status bar and the popup footers.</summary>
public static class HintRenderer
{
    /// <summary>Appends the hints separated by <paramref name="gap"/>, starting with a one-column margin.</summary>
    public static void Append(LineBuilder builder, IReadOnlyList<KeyHint> hints, string gap)
    {
        for (var index = 0; index < hints.Count; index++)
        {
            var hint = hints[index];
            builder.Append(index == 0 ? " " : gap, SegmentRole.Status);
            builder.Append(hint.Keys, SegmentRole.StatusKey);
            builder.Append($" {hint.Description}", SegmentRole.Status);
        }
    }
}
