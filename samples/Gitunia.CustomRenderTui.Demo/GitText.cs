using System.Diagnostics;
using System.Text;

namespace Gitunia.CustomRenderTui.Demo;

/// <summary>Runs a short `git -C repo ...` command and returns its stdout as text (async, cancellable, killed on cancel).</summary>
public static class GitText
{
    public static async Task<string> RunAsync(string repo, IReadOnlyList<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(repo);
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

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
        var text = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);
        return text;
    }
}
