using Gitunia.Core.Status;
using Gitunia.Git.Remotes.Models;

namespace Gitunia.Core.Sync;

/// <summary>A fetch, pull or push that runs for a tab: what the status bar line says. Null on the tab while none runs.</summary>
/// <param name="Activity">The operation as a gerund (<c>Fetching</c>).</param>
/// <param name="Noun">The operation as a noun (<c>Fetch</c>).</param>
/// <param name="Latest">The latest report of git, null before the first one.</param>
/// <param name="IsCancelling">The cancel was asked for and git is being stopped.</param>
/// <param name="IsBackground">The background timer started it: nothing shows in the status bar unless it is cancelled, and a failure stays quiet.</param>
public sealed record SyncState(
    string Activity,
    string Noun,
    RemoteProgress? Latest,
    bool IsCancelling,
    bool IsBackground = false)
{
    /// <summary>The status bar line, such as <c>Fetching: Receiving objects 45%</c>; empty while a background fetch runs.</summary>
    public string ProgressLine
    {
        get
        {
            if (IsBackground && !IsCancelling)
            {
                return string.Empty;
            }

            var line = RemoteProgressText.Format(Activity, Latest);
            return IsCancelling ? $"{line} (cancelling)" : line;
        }
    }
}
