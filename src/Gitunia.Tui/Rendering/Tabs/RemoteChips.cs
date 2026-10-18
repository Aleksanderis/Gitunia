using Gitunia.Core.Mouse;

namespace Gitunia.Tui.Rendering.Tabs;

/// <summary>
/// The fetch, pull and push chips left of the commands button on the tab row, the mouse path of <c>&lt;space&gt;F</c>,
/// <c>&lt;space&gt;L</c> and <c>&lt;space&gt;P</c> (#456). While one of them runs they are replaced by one cancel chip
/// (the mouse path of <c>Esc</c>). Rows narrower than <see cref="MinimumWidth"/> have none (the keys and the command
/// palette stay). Pure layout: the same width gives the same columns, busy or not, so the tabs never jump.
/// </summary>
public static class RemoteChips
{
    /// <summary>Rows narrower than this show no chips.</summary>
    public const int MinimumWidth = 100;

    /// <summary>The idle chips, in order.</summary>
    public static IReadOnlyList<RemoteChip> Idle { get; } =
    [
        new(" fetch ", DefaultMouseMap.RemoteFetchButton),
        new(" pull ", DefaultMouseMap.RemotePullButton),
        new(" push ", DefaultMouseMap.RemotePushButton),
    ];

    /// <summary>The one chip shown while a fetch, pull or push of the active tab runs.</summary>
    public static RemoteChip Cancel { get; } = new(" Esc cancel ", DefaultMouseMap.RemoteCancelButton);

    private const int GapColumns = 1;

    /// <summary>The columns the idle chips take with the gaps between them, the most any state needs.</summary>
    public static int ReservedWidth => Idle.Sum(chip => chip.Label.Length) + (Idle.Count - 1) * GapColumns;

    /// <summary>The first column of the chips in a row of <paramref name="width"/> columns, null if none.</summary>
    public static int? Start(int width) =>
        width >= MinimumWidth && TabBarLayout.CommandsStart(width) is { } commands
            ? commands - GapColumns - ReservedWidth
            : null;

    /// <summary>The chips shown now, left to right.</summary>
    public static IReadOnlyList<RemoteChip> Shown(bool isBusy) => isBusy ? [Cancel] : Idle;

    /// <summary>The mouse view of the chip at <paramref name="column"/> of a row, null when it is on no chip.</summary>
    public static string? ViewAt(int width, bool isBusy, int column)
    {
        if (Start(width) is not { } start)
        {
            return null;
        }

        var left = start;
        foreach (var chip in Shown(isBusy))
        {
            if (column >= left && column < left + chip.Label.Length)
            {
                return chip.View;
            }

            left += chip.Label.Length + GapColumns;
        }

        return null;
    }
}
