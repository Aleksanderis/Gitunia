using System.Diagnostics;

namespace Gitunia.CustomRenderTui.Demo;

/// <summary>Streams `git -C repo log` stdout in chunks (async, cancellable). Never uses the process cwd.</summary>
public static class GitLogRunner
{
    public static async Task RunAsync(string repo, Action<byte[]> onChunk, Action onDone, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(repo);
        psi.ArgumentList.Add("log");
        psi.ArgumentList.Add("--no-color");
        psi.ArgumentList.Add("--format=%H%x09%P%x09%s");

        try
        {
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("git did not start");
            p.StandardInput.Close();
            using var reg = ct.Register(() =>
            {
                try
                {
                    p.Kill();
                }
                catch (InvalidOperationException)
                {
                }
            });
            var stream = p.StandardOutput.BaseStream;
            var buf = new byte[1 << 16];
            int n;
            while ((n = await stream.ReadAsync(buf, ct).ConfigureAwait(false)) > 0)
            {
                onChunk(buf.AsSpan(0, n).ToArray());
            }

            await p.WaitForExitAsync(ct).ConfigureAwait(false);
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
