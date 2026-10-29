using System.Text;
using Gitunia.Core.Jump.Models;

namespace Gitunia.Tui.Rendering.Styling;

/// <summary>Draws the quick-jump label of a row over its text, whatever view the row belongs to.</summary>
public static class JumpOverlay
{
    /// <summary>
    /// Appends <paramref name="shown"/> with the match highlighted and the label letter over the character after it (appended
    /// when the match ends the text). A wide character under the label leaves a blank so the rest of the row stays where it was.
    /// </summary>
    /// <param name="builder">The line the text goes into.</param>
    /// <param name="shown">The text as shown, the one the label was worked out for.</param>
    /// <param name="role">The role of the text around the match.</param>
    /// <param name="jump">The label.</param>
    public static void Append(LineBuilder builder, string shown, SegmentRole role, JumpLabel jump)
    {
        var start = Math.Min(jump.MatchStart, shown.Length);
        var end = Math.Min(shown.Length, jump.MatchStart + jump.MatchLength);
        builder.Append(shown[..start], role);
        builder.Append(shown[start..end], SegmentRole.SearchMatch);
        if (!jump.HasLetter)
        {
            builder.Append(shown[end..], role);
            return;
        }

        builder.Append(jump.Letter.ToString(), SegmentRole.JumpLabel);
        if (end >= shown.Length)
        {
            return;
        }

        Rune.DecodeFromUtf16(shown.AsSpan(end), out var replaced, out var consumed);
        var padding = TextWidth.Of(replaced) - 1;
        if (padding > 0)
        {
            builder.Append(new string(' ', padding), role);
        }

        builder.Append(shown[(end + consumed)..], role);
    }

    /// <summary>
    /// Appends the <paramref name="parts"/> (one row's text in several roles) as <see cref="Append(LineBuilder, string, SegmentRole, JumpLabel)"/> does for
    /// one run: the label offsets count over the parts joined.
    /// </summary>
    /// <param name="builder">The line the text goes into.</param>
    /// <param name="parts">The text as shown, the one the label was worked out for.</param>
    /// <param name="jump">The label.</param>
    public static void Append(LineBuilder builder, IReadOnlyList<StyledSegment> parts, JumpLabel jump)
    {
        var shown = string.Concat(parts.Select(part => part.Text));
        var start = Math.Min(jump.MatchStart, shown.Length);
        var end = Math.Min(shown.Length, jump.MatchStart + jump.MatchLength);
        var replaced = 0;
        var padding = 0;
        if (jump.HasLetter && end < shown.Length)
        {
            Rune.DecodeFromUtf16(shown.AsSpan(end), out var rune, out replaced);
            padding = Math.Max(0, TextWidth.Of(rune) - 1);
        }

        var offset = 0;
        foreach (var part in parts)
        {
            var cuts = new[] { 0, start - offset, end - offset, end + replaced - offset, part.Text.Length }
                .Select(cut => Math.Clamp(cut, 0, part.Text.Length)).Distinct().Order().ToArray();
            for (var index = 0; index + 1 < cuts.Length; index++)
            {
                var from = offset + cuts[index];
                var piece = part.Text[cuts[index]..cuts[index + 1]];
                if (jump.HasLetter && replaced > 0 && from == end)
                {
                    builder.Append(jump.Letter.ToString(), SegmentRole.JumpLabel);
                    builder.Append(new string(' ', padding), part.Role, part.ColorIndex);
                }
                else if (from >= start && from < end)
                {
                    builder.Append(piece, SegmentRole.SearchMatch);
                }
                else
                {
                    builder.Append(piece, part.Role, part.ColorIndex);
                }
            }

            offset += part.Text.Length;
        }

        if (jump.HasLetter && replaced == 0)
        {
            builder.Append(jump.Letter.ToString(), SegmentRole.JumpLabel);
        }
    }
}
