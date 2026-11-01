using System.Globalization;

namespace Gitunia.Core.Text;

/// <summary>A byte count as short text: <c>512 B</c>, <c>1.5 KB</c>, <c>12 MB</c> (powers of 1024).</summary>
public static class FileSizeText
{
    private const int Step = 1024;
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Of(long bytes)
    {
        double value = bytes;
        var unit = 0;
        while (value >= Step && unit < Units.Length - 1)
        {
            value /= Step;
            unit++;
        }

        var format = unit == 0 || value >= 10 ? "0" : "0.0";

        return $"{value.ToString(format, CultureInfo.InvariantCulture)} {Units[unit]}";
    }
}
