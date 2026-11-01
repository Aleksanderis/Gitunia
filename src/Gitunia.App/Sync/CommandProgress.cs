using Gitunia.Core.Sync;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Remotes.Models;

namespace Gitunia.App.Sync;

/// <summary>Turns each report of git into a <see cref="SyncProgressed"/> command for the tab.</summary>
/// <param name="dispatcher">Where the commands go (any thread may report).</param>
/// <param name="tabId">The tab the transfer runs for.</param>
internal sealed class CommandProgress(IDispatcher dispatcher, TabId tabId) : IProgress<RemoteProgress>
{
    public void Report(RemoteProgress value) => dispatcher.Post(new SyncProgressed(tabId, value));
}
