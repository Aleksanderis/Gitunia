using System.Diagnostics;

namespace Gitunia.CustomRenderTui.Demo;

/// <summary>Streams `git -C repo log` stdout in chunks (async, cancellable). Never uses the process cwd.</summary>
public static class GitLogRunner
{
    public static async Task RunAsync(string repositoryPath, Action<byte[]> onChunk, Action onDone, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(repositoryPath);
        startInfo.ArgumentList.Add("log");
        startInfo.ArgumentList.Add("--no-color");
        startInfo.ArgumentList.Add("--format=%H%x09%P%x09%s");

        try
        {
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("git did not start");
            process.StandardInput.Close();
            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                }
            });
            var stream = process.StandardOutput.BaseStream;
            var buffer = new byte[1 << 16];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                onChunk(buffer.AsSpan(0, bytesRead).ToArray());
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            onDone();
        }
    }
}
