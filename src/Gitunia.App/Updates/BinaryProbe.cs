using System.ComponentModel;
using System.Diagnostics;
using Gitunia.Core.Updates;

namespace Gitunia.App.Updates;

/// <summary>Runs a downloaded binary with <c>--version</c> to learn that it starts and which version it is.</summary>
public static class BinaryProbe
{
    private const string VersionArgument = "--version";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The version the binary reports (its output is <c>gitunia &lt;version&gt;</c>), null when it does not start, times out
    /// or prints something else.
    /// </summary>
    public static async Task<ReleaseVersion?> ReadVersionAsync(string binaryPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(binaryPath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(VersionArgument);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            return await ReadAsync(process, timeout.Token);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<ReleaseVersion?> ReadAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            await errors;

            return process.ExitCode == 0 ? ParseVersion(output) : null;
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
    }

    private static ReleaseVersion? ParseVersion(string output)
    {
        var words = output.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        return words.Length >= 2 && ReleaseVersion.TryParse(words[1], out var version) ? version : null;
    }
}
