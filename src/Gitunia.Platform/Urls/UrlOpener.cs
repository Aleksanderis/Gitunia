using System.ComponentModel;
using System.Diagnostics;

namespace Gitunia.Platform.Urls;

/// <summary>
/// Linux runs <c>xdg-open</c>, macOS runs <c>open</c> and Windows hands the address to the shell; every other OS is
/// unsupported. The opener's output is discarded so it cannot draw over the terminal UI.
/// </summary>
public sealed class UrlOpener : IUrlOpener
{
    private const string LinuxOpener = "xdg-open";
    private const string MacOpener = "open";
    private const string HttpPrefix = "http://";
    private const string HttpsPrefix = "https://";

    public bool IsSupported => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();

    public UrlOpenResult Open(string url)
    {
        if (!url.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new UrlOpenResult(false, "only http and https addresses are opened");
        }

        if (!IsSupported)
        {
            return new UrlOpenResult(false, "opening addresses is not supported on this operating system");
        }

        var startInfo = OperatingSystem.IsWindows() ? WindowsStartInfo(url) : OpenerStartInfo(url);
        try
        {
            using var process = Process.Start(startInfo);

            return process is null && !OperatingSystem.IsWindows()
                ? new UrlOpenResult(false, $"could not start {startInfo.FileName}")
                : new UrlOpenResult(true);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return new UrlOpenResult(false, $"could not start {startInfo.FileName} ({exception.Message})");
        }
    }

    // The shell resolves the default browser; no cmd.exe is involved, so the address is never parsed as a command.
    private static ProcessStartInfo WindowsStartInfo(string url) => new(url) { UseShellExecute = true };

    private static ProcessStartInfo OpenerStartInfo(string url)
    {
        var startInfo = new ProcessStartInfo(OperatingSystem.IsMacOS() ? MacOpener : LinuxOpener)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(url);

        return startInfo;
    }
}
