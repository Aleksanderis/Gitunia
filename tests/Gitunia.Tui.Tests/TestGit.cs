using Gitunia.App;
using Gitunia.App.Loading.Graph;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Git.Watching.Models;

namespace Gitunia.Tui.Tests;

/// <summary>The real git services (the git CLI behind every reader) for tests whose subject is the code on top of git.</summary>
public static class TestGit
{
    public static GitServices Services { get; } = GitServices.Create(IsolatedGit.CreateRunner(), AllLaneColors.Instance);

    /// <summary>Watcher options for tests on fake locations: nothing to watch, and a polling interval too long to ever fire.</summary>
    public static RepositoryWatcherOptions QuietWatcherOptions { get; } = new(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(500), TimeSpan.FromHours(1));
}
