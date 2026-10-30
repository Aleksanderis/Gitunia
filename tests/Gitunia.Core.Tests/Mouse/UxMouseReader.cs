using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Mouse;
using Gitunia.Core.Tests.Keymap;

namespace Gitunia.Core.Tests.Mouse;

/// <summary>Reads the mouse table in <c>docs/ux-mouse.md</c> (columns <c>View | Gesture | Action | Effect</c>) into bindings.</summary>
public static class UxMouseReader
{
    public static IReadOnlyList<UxMouseRow> ReadRows()
    {
        var rows = new List<UxMouseRow>();
        var inMouse = false;
        foreach (var line in File.ReadAllLines(RepoDocs.FindUxDocument("ux-mouse.md")))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inMouse = line.Trim() == "## Mouse";
                continue;
            }

            if (!inMouse || !line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
            if (cells.Length < 4 || cells[0] == "View" || cells[0].StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            rows.Add(new UxMouseRow(cells[0], ParseGesture(cells[1]), new ActionId(cells[2].Trim('`'))));
        }

        return rows;
    }

    private static MouseGesture ParseGesture(string text) => text switch
    {
        "Click" => MouseGesture.Click,
        "Double-click" => MouseGesture.DoubleClick,
        "Wheel up" => MouseGesture.WheelUp,
        "Sideways wheel back" => MouseGesture.WheelHorizontalBack,
        "Sideways wheel forward" => MouseGesture.WheelHorizontalForward,
        "Wheel down" => MouseGesture.WheelDown,
        "Drag start" => MouseGesture.DragStart,
        "Drag up" => MouseGesture.DragUp,
        "Drag down" => MouseGesture.DragDown,
        _ => throw new InvalidDataException($"Unknown mouse gesture in docs/ux-mouse.md: {text}"),
    };
}
