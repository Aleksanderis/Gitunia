using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;

namespace Gitunia.Core.Tests.Mouse;

/// <summary>Reads the mouse table in <c>docs/ux.md</c> (columns <c>View | Gesture | Action | Effect</c>) into bindings.</summary>
public static class UxMouseReader
{
    public static IReadOnlyList<(string View, MouseGesture Gesture, ActionId Action)> ReadRows()
    {
        var rows = new List<(string, MouseGesture, ActionId)>();
        var inMouse = false;
        foreach (var line in File.ReadAllLines(UxKeymapReader.FindUxDocument()))
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

            rows.Add((cells[0], ParseGesture(cells[1]), new ActionId(cells[2].Trim('`'))));
        }

        return rows;
    }

    private static MouseGesture ParseGesture(string text) => text switch
    {
        "Click" => MouseGesture.Click,
        "Double-click" => MouseGesture.DoubleClick,
        "Wheel up" => MouseGesture.WheelUp,
        "Wheel down" => MouseGesture.WheelDown,
        "Drag start" => MouseGesture.DragStart,
        "Drag up" => MouseGesture.DragUp,
        "Drag down" => MouseGesture.DragDown,
        _ => throw new InvalidDataException("Unknown mouse gesture in docs/ux.md: " + text),
    };
}
