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
    [InlineData("{\"version\": 99, \"tabs\": []}")]
    [InlineData("{\"tabs\": []}")]
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
}
