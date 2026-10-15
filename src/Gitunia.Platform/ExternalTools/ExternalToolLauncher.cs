using System.ComponentModel;
using System.Diagnostics;

namespace Gitunia.Platform.ExternalTools;

/// <summary>
/// Spawns the program without a shell and without redirecting anything, so it inherits the terminal: the console on
/// Windows, the controlling terminal on Linux and macOS. Terminal modes are restored by the caller (the UI shuts its
/// driver down before and starts it again after), on every OS.
/// </summary>
public sealed class ExternalToolLauncher : IExternalToolLauncher
{
    public bool IsSupported => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsWindows();

    public ExternalToolResult Run(ExternalToolRequest request)
    {
        if (!IsSupported)
        {
            return NotStarted("external tools are not supported on this operating system");
        }

        var startInfo = new ProcessStartInfo(request.FileName)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
        };
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return NotStarted($"could not start {request.FileName}");
            }

            process.WaitForExit();

            return new ExternalToolResult(process.ExitCode);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return NotStarted($"could not start {request.FileName} ({exception.Message})");
        }
    }

    private static ExternalToolResult NotStarted(string reason) =>
        new(ExternalToolResult.NotStartedExitCode, reason);
}
