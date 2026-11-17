using System.Globalization;

namespace Gitunia.Core.Text;

/// <summary>The <c>n/m</c> position of the highlighted row in a list: the one format, at the right end of a list's title.</summary>
public static class PositionText
{
    /// <summary>The text for a 0-based <paramref name="index"/> in a list of <paramref name="total"/> rows.</summary>
    public static string Of(int index, int total) =>
        $"{(index + 1).ToString(CultureInfo.InvariantCulture)}/{total.ToString(CultureInfo.InvariantCulture)}";
}
