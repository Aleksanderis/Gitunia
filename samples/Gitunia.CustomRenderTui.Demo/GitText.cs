using System.Diagnostics;
using System.Text;

namespace Gitunia.CustomRenderTui.Demo;

/// <summary>Runs a short `git -C repo ...` command and returns its stdout as text (async, cancellable, killed on cancel).</summary>
public static class GitText
{
    public static async Task<string> RunAsync(string repositoryPath, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(repositoryPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("git did not start");
        process.StandardInput.Close();
        await using var registration = cancellationToken.Register(ProcessKiller.KillQuietly, process);

        var text = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return text;
    }
}
