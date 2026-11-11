using System.Globalization;
using System.Text;
using System.Text.Unicode;

namespace Gitunia.Git.Execution;

/// <summary>
/// Turns git's stderr bytes into message text: UTF-8 when valid, otherwise (on Windows) the OEM code page that hooks and
/// credential helpers write in, and always with LF instead of CRLF line ends.
/// </summary>
public static class ErrorTextDecoder
{
    /// <summary>Decode with the running OS and its OEM code page.</summary>
    public static string Decode(ReadOnlySpan<byte> bytes) => Decode(bytes, OperatingSystem.IsWindows() ? OemEncoding() : null);

    /// <summary>Decode with an explicit fallback encoding (null: replace invalid bytes as UTF-8 does).</summary>
    public static string Decode(ReadOnlySpan<byte> bytes, Encoding? fallback)
    {
        var text = fallback is not null && !Utf8.IsValid(bytes) ? fallback.GetString(bytes) : Encoding.UTF8.GetString(bytes);
        // Only CRLF: a lone carriage return is git's progress redraw and stays.
        return text.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static Encoding? OemEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
