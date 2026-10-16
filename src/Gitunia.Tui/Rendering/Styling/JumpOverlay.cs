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
}
