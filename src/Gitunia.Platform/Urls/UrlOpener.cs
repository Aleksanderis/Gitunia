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
            var process = Process.Start(startInfo);
            if (process is null)
            {
                if (OperatingSystem.IsWindows())
                {
                    return new UrlOpenResult(true);
                }

                return new UrlOpenResult(false, $"could not start {startInfo.FileName}");
            }

            if (startInfo.RedirectStandardOutput)
            {
                Drain(process);
            }
            else
            {
                process.Dispose();
            }

            return new UrlOpenResult(true);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return new UrlOpenResult(false, $"could not start {startInfo.FileName} ({exception.Message})");
        }
    }

    // A browser started by the opener inherits the pipes, so they stay open and read (and thrown away) until the opener
    // ends; closing them early could make the browser fail when it logs.
    private static void Drain(Process process)
    {
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _ = Task.Run(() =>
        {
            process.WaitForExit();
            process.Dispose();
        });
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
