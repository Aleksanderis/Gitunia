using System.Text;

namespace Gitunia.Git;

/// <summary>Outcome of a finished git call.</summary>
public sealed record GitResult(int ExitCode, byte[] StandardOutput, string StandardError)
{
    public string StandardOutputText => Encoding.UTF8.GetString(StandardOutput);
}
