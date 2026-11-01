using Gitunia.Core.Status;
using Gitunia.Git.Remotes.Models;

namespace Gitunia.Core.Creation;

/// <summary>A running clone: what the status bar line says.</summary>
/// <param name="Latest">The latest report of git, null before the first one.</param>
/// <param name="IsCancelling">The cancel was asked for and git is being stopped.</param>
public sealed record CloneState(RemoteProgress? Latest, bool IsCancelling)
{
    private const string Activity = "Cloning";

    /// <summary>The status bar line, such as <c>Cloning: Receiving objects 45%</c>.</summary>
    public string ProgressLine
    {
        get
        {
            var line = RemoteProgressText.Format(Activity, Latest);

            return IsCancelling ? $"{line} (cancelling)" : line;
        }
    }
}
