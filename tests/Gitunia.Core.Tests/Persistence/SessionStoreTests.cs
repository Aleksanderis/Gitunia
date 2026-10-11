using Gitunia.Core.Persistence;
using Gitunia.Core.Workspaces;
using Xunit;

namespace Gitunia.Core.Tests.Persistence;

public class SessionStoreTests
{
    [Fact]
    public async Task GivenNoFile_WhenLoading_ThenEmptyWorkspaceAndMissingStatus()
    {
        using var directory = new TemporaryDirectory();
        var store = new SessionStore(Path.Combine(directory.Path, "does-not-exist"));

        var result = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SessionLoadStatus.Missing, result.Status);
        Assert.Empty(result.Update.Workspace.Tabs);
    }

    [Fact]
    public async Task GivenSavedSession_WhenLoadingInNewStore_ThenSessionIsRestored()
    {
        using var directory = new TemporaryDirectory();
        var workspace = SessionFixtures.Open(Workspace.Empty, "alpha", "beta");

        var saved = await new SessionStore(directory.Path).SaveAsync(workspace, TestContext.Current.CancellationToken);
        var result = await new SessionStore(directory.Path, _ => true).LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(saved);
        Assert.Equal(SessionLoadStatus.Restored, result.Status);
        Assert.Equal(["alpha", "beta"], result.Update.Workspace.Tabs.Select(tab => tab.DisplayName));
        Assert.Equal(2, result.Update.Effects.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("null")]
    [InlineData("{\"version\": 1, \"tabs\": \"oops\"}")]
    public async Task GivenCorruptFile_WhenLoading_ThenEmptyWorkspaceAndFileSetAside(string content)
    {
        using var directory = new TemporaryDirectory();
        var store = new SessionStore(directory.Path);
        await File.WriteAllTextAsync(store.FilePath, content, TestContext.Current.CancellationToken);

        var result = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SessionLoadStatus.Discarded, result.Status);
        Assert.NotNull(result.Detail);
        Assert.Empty(result.Update.Workspace.Tabs);
        Assert.False(File.Exists(store.FilePath));
        Assert.True(File.Exists(store.FilePath + ".corrupt"));
    }

    [Fact]
    public async Task GivenBinaryGarbage_WhenLoading_ThenStartupIsNotBlocked()
    {
        using var directory = new TemporaryDirectory();
        var store = new SessionStore(directory.Path);
        await File.WriteAllBytesAsync(store.FilePath, [0xFF, 0xFE, 0x00, 0x01, 0x80], TestContext.Current.CancellationToken);

        var result = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SessionLoadStatus.Discarded, result.Status);
    }

    [Fact]
    public async Task GivenFileFromNewerFieldsAndComments_WhenLoading_ThenUnknownPartsAreIgnored()
    {
        using var directory = new TemporaryDirectory();
        var store = new SessionStore(directory.Path);
        var json = "{ // note\n \"version\": 1, \"futureSetting\": true, \"tabs\": [], \"recents\": [" + "\"" + SessionFixtures.AbsolutePath("a").Replace("\\", "\\\\") + "\"" + "], }";
        await File.WriteAllTextAsync(store.FilePath, json, TestContext.Current.CancellationToken);

        var result = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SessionLoadStatus.Restored, result.Status);
        Assert.Single(result.Update.Workspace.Recents);
    }

    [Fact]
    public async Task GivenUnwritableTarget_WhenSaving_ThenFalseIsReturnedAndExistingFileIsIntact()
    {
        using var directory = new TemporaryDirectory();
        var blocker = Path.Combine(directory.Path, "blocker");
        await File.WriteAllTextAsync(blocker, "x", TestContext.Current.CancellationToken);
        var store = new SessionStore(Path.Combine(blocker, "nested"));

        var saved = await store.SaveAsync(SessionFixtures.Open(Workspace.Empty, "alpha"), TestContext.Current.CancellationToken);

        Assert.False(saved);
        Assert.Equal("x", await File.ReadAllTextAsync(blocker, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GivenExistingSession_WhenSavingAgain_ThenFileIsReplacedAndNoTemporaryFilesRemain()
    {
        using var directory = new TemporaryDirectory();
        var store = new SessionStore(directory.Path);
        await store.SaveAsync(SessionFixtures.Open(Workspace.Empty, "alpha"), TestContext.Current.CancellationToken);

        await store.SaveAsync(SessionFixtures.Open(Workspace.Empty, "beta"), TestContext.Current.CancellationToken);

        var result = await new SessionStore(directory.Path, _ => true).LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal("beta", Assert.Single(result.Update.Workspace.Tabs).DisplayName);
        Assert.Equal([store.FilePath], Directory.GetFiles(directory.Path));
    }

    [Theory]
    [InlineData("{\"version\": 99, \"tabs\": []}", ".v99.bak")]
    [InlineData("{\"tabs\": []}", ".v0.bak")]
    public async Task GivenOtherSchemaVersion_WhenLoading_ThenFileIsBackedUpWithItsVersion(string content, string suffix)
    {
        using var directory = new TemporaryDirectory();
        var store = new SessionStore(directory.Path);
        await File.WriteAllTextAsync(store.FilePath, content, TestContext.Current.CancellationToken);

        var result = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SessionLoadStatus.UnsupportedVersion, result.Status);
        Assert.Empty(result.Update.Workspace.Tabs);
        Assert.Equal(content, await File.ReadAllTextAsync(store.FilePath + suffix, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GivenEarlierCorruptBackup_WhenAnotherCorruptFileIsLoaded_ThenEarlierBackupIsKept()
    {
        using var directory = new TemporaryDirectory();
        var store = new SessionStore(directory.Path);
        await File.WriteAllTextAsync(store.FilePath + ".corrupt", "first", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(store.FilePath, "{ second", TestContext.Current.CancellationToken);

        await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("first", await File.ReadAllTextAsync(store.FilePath + ".corrupt", TestContext.Current.CancellationToken));
        Assert.Equal(2, Directory.GetFiles(directory.Path).Length);
    }

    [Fact]
    public async Task GivenFileLockedForReading_WhenLoading_ThenFileIsNotMovedAside()
    {
        if (OperatingSystem.IsWindows() == false)
        {
            // Exclusive locks are only enforced on Windows; elsewhere an unreadable file is simulated with a directory below.
            using var unixDirectory = new TemporaryDirectory();
            var unixStore = new SessionStore(unixDirectory.Path);
            Directory.CreateDirectory(unixStore.FilePath);

            var unixResult = await unixStore.LoadAsync(TestContext.Current.CancellationToken);

            Assert.True(Directory.Exists(unixStore.FilePath));
            Assert.NotEqual(SessionLoadStatus.Restored, unixResult.Status);
            return;
        }

        using var directory = new TemporaryDirectory();
        var store = new SessionStore(directory.Path);
        await File.WriteAllTextAsync(store.FilePath, "{\"version\":1}", TestContext.Current.CancellationToken);
        using var held = new FileStream(store.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SessionLoadStatus.Discarded, result.Status);
        Assert.False(File.Exists(store.FilePath + ".corrupt"));
    }
}
