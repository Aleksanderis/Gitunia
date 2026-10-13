using Gitunia.Core.Status;
using Gitunia.Core.Persistence;
using Gitunia.Core.Workspaces;
using Gitunia.Git;
using Gitunia.Tui.App;
using Xunit;

namespace Gitunia.Tui.Tests.App;

public class SessionPersistenceTests
{
    private static RepositoryLocation Location(string root) =>
        new(root, Path.Combine(root, ".git"), Path.Combine(root, ".git"), false, false);

    [Fact]
    public async Task GivenOpenedTab_WhenFlushing_ThenTheSessionFileHoldsTheTab()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gitunia-session-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionStore(directory);
            var host = new WorkspaceHost(Workspace.Empty);
            using var persistence = new SessionPersistence(store, host, TimeSpan.FromHours(1), new StatusChannel(), action => action());
            persistence.Start();
            host.Dispatch(new OpenRepositoryCommand(Location(Path.Combine(directory, "alpha"))));

            persistence.Flush();

            var loaded = await store.LoadAsync(CancellationToken.None);
            Assert.Equal(SessionLoadStatus.Restored, loaded.Status);
            Assert.Equal(Path.Combine(directory, "alpha"), Assert.Single(loaded.Update.Workspace.Tabs).Location.RootPath);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void GivenNothingChangedSinceStart_WhenFlushing_ThenNoFileIsWritten()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gitunia-session-" + Guid.NewGuid().ToString("N"));
        var store = new SessionStore(directory);
        var host = new WorkspaceHost(Workspace.Empty);
        using var persistence = new SessionPersistence(store, host, TimeSpan.FromHours(1), new StatusChannel(), action => action());
        persistence.Start();

        persistence.Flush();

        Assert.False(File.Exists(store.FilePath));
    }

    [Fact]
    public void GivenUnwritableSessionLocation_WhenSavingRepeatedly_ThenOneErrorMessageIsPosted()
    {
        var blocker = Path.Combine(Path.GetTempPath(), "gitunia-session-blocker-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(blocker, "not a directory");
        try
        {
            var statuses = new StatusChannel();
            var host = new WorkspaceHost(Workspace.Empty);
            using var persistence = new SessionPersistence(new SessionStore(blocker), host, TimeSpan.FromHours(1), statuses, action => action());
            persistence.Start();

            host.Dispatch(new OpenRepositoryCommand(Location(Path.Combine(blocker, "alpha"))));
            persistence.Flush();
            host.Dispatch(new OpenRepositoryCommand(Location(Path.Combine(blocker, "beta"))));
            persistence.Flush();

            var message = Assert.Single(statuses.Log.History);
            Assert.Equal(StatusSeverity.Error, message.Severity);
            Assert.Contains("save the session", message.Text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(blocker);
        }
    }

    [Fact]
    public async Task GivenChangeAndShortDebounce_WhenWaiting_ThenTheSessionIsSavedWithoutAFlush()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gitunia-session-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionStore(directory);
            var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Directory.CreateDirectory(directory);
            using var watcher = new FileSystemWatcher(directory);
            watcher.Created += (_, _) => { if (File.Exists(store.FilePath)) { saved.TrySetResult(); } };
            watcher.Renamed += (_, _) => { if (File.Exists(store.FilePath)) { saved.TrySetResult(); } };
            watcher.Changed += (_, _) => { if (File.Exists(store.FilePath)) { saved.TrySetResult(); } };
            watcher.EnableRaisingEvents = true;
            var host = new WorkspaceHost(Workspace.Empty);
            using var persistence = new SessionPersistence(store, host, TimeSpan.FromMilliseconds(30), new StatusChannel(), action => action());
            persistence.Start();

            host.Dispatch(new OpenRepositoryCommand(Location(Path.Combine(directory, "alpha"))));

            await saved.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.True(File.Exists(store.FilePath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GivenChangedPaneSizes_WhenFlushingAndLoading_ThenTheTabHasTheSizes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gitunia-session-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SessionStore(directory, _ => true);
            var host = new WorkspaceHost(Workspace.Empty);
            using var persistence = new SessionPersistence(store, host, TimeSpan.FromHours(1), new StatusChannel(), action => action());
            persistence.Start();
            host.Dispatch(new OpenRepositoryCommand(Location(Path.Combine(directory, "alpha"))));
            host.Dispatch(new TabCommand(host.Workspace.ActiveTabId!.Value, new SetPaneSizesCommand(30, 12, 40)));

            persistence.Flush();

            var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);
            var view = Assert.Single(loaded.Update.Workspace.Tabs).View;
            Assert.Equal((30, 12, 40), (view.SidebarWidth, view.BottomHeight, view.DetailWidth));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GivenSessionFileWithoutSizes_WhenLoading_ThenTheTabHasAutomaticSizes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gitunia-session-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var root = Path.Combine(directory, "alpha");
            var gitDirectory = Path.Combine(root, ".git");
            var store = new SessionStore(directory, _ => true);
            var escapedRoot = root.Replace("\\", "\\\\", StringComparison.Ordinal);
            var escapedGitDirectory = gitDirectory.Replace("\\", "\\\\", StringComparison.Ordinal);
            var json = $$"""
                {"version":1,"activeTabIndex":0,"tabs":[{"workingTreePath":"{{escapedRoot}}","gitDirectory":"{{escapedGitDirectory}}","commonDirectory":"{{escapedGitDirectory}}","scrollOffset":3,"sidebarWidth":-5}]}
                """;
            await File.WriteAllTextAsync(store.FilePath, json, TestContext.Current.CancellationToken);

            var loaded = await store.LoadAsync(TestContext.Current.CancellationToken);

            Assert.Equal(SessionLoadStatus.Restored, loaded.Status);
            var view = Assert.Single(loaded.Update.Workspace.Tabs).View;
            Assert.Equal(3, view.ScrollOffset);
            Assert.Equal((null, null, null), (view.SidebarWidth, view.BottomHeight, view.DetailWidth));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
