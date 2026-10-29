using Gitunia.Core.Diff;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Diff;

/// <summary>Draws a line of text whose changed words sit on the tint of their added or removed colour.</summary>
internal static class ChangedText
{
    /// <summary>
    /// Appends <paramref name="text"/> in <paramref name="role"/>, the stretches in <paramref name="changes"/> in the
    /// word variant of the role (roles other than added and removed have none and stay as they are).
    /// </summary>
    public static void Append(
        LineBuilder builder,
        string text,
        SegmentRole role,
        IReadOnlyList<ChangedSpan>? changes) =>
        Append(builder, text, role, changes, DiffRowRenderer.RoleOf(role, DiffRowRenderer.CellFlags.Changed));

    /// <summary>As above, with the role of the changed stretches given (the line is not added or removed).</summary>
    public static void Append(
        LineBuilder builder,
        string text,
        SegmentRole role,
        IReadOnlyList<ChangedSpan>? changes,
        SegmentRole changedRole)
    {
        if (changes is not { Count: > 0 })
        {
            builder.Append(text, role);

            return;
        }

        var position = 0;
        foreach (var span in changes)
        {
            var start = Math.Clamp(span.Start, position, text.Length);
            var end = Math.Clamp(span.End, start, text.Length);
            builder.Append(text[position..start], role);
            builder.Append(text[start..end], changedRole);
            position = end;
        }

        builder.Append(text[position..], role);
    }
}
