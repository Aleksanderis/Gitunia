using Gitunia.Core.Updates;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>
/// The update hint (#1292) at the right end of the status bar, in the longest of its forms that leaves the rest of the
/// bar <see cref="MinimumLeftColumns"/>: the version and the command, the version alone, nothing. Pure.
/// </summary>
public static class UpdateHintText
{
    /// <summary>What the mode, position, message and hints keep before the hint may take columns.</summary>
    public const int MinimumLeftColumns = 48;

    /// <summary>Columns between the rest of the bar and the hint.</summary>
    public const int Gap = 2;

    /// <summary>The text to draw for a bar of <paramref name="width"/> columns, empty when it does not fit.</summary>
    /// <param name="notice">The newer release.</param>
    /// <param name="width">Terminal columns.</param>
    public static string For(UpdateNotice notice, int width)
    {
        string[] forms = [$"Gitunia {notice.Version} available: {notice.Command}", $"Gitunia {notice.Version} available"];
        foreach (var text in forms)
        {
            if (width - TextWidth.Of(text) - Gap >= MinimumLeftColumns)
            {
                return text;
            }
        }

        return string.Empty;
    }
}
