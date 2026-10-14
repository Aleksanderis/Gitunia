using System.Globalization;
using Gitunia.Git;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Makes distinct, readable object ids for fakes that need no real objects.</summary>
public static class FakeObjectIds
{
    private const int HexDigitCount = 40;

    /// <summary>The id with this number: the number in hexadecimal, padded with zeros.</summary>
    public static ObjectId Of(int number)
    {
        var text = number.ToString("x", CultureInfo.InvariantCulture).PadLeft(HexDigitCount, '0');
        return ObjectId.TryParse(text, out var id) ? id : throw new InvalidOperationException(text);
    }
}
