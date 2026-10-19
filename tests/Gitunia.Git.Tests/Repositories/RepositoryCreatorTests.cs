using Gitunia.Git.Execution;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Tests.Execution;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.Git.Tests.Repositories;

public class RepositoryCreatorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GivenALocalBareRepository_WhenCloning_ThenTheWorkingTreeHasItsContent()
    {
        using var repository = TestRepositories.WithOrigin();
        var target = TargetIn(repository, "dest");

        var location = await CreatorFor(repository)
            .CloneAsync(new CloneRequest(repository.OriginPath, target), null, Token);

        Assert.Equal(Path.GetFullPath(target), location.WorkingTreePath);
        Assert.Equal(repository.Head, repository.GitIn(target, ["rev-parse", "HEAD"]).Trim());
    }

    [Fact]
    public async Task GivenAFileUrl_WhenCloning_ThenProgressIsReported()
    {
        using var repository = TestRepositories.WithOrigin();
        var target = TargetIn(repository, "dest");
        var progress = new CollectingProgress();

        await CreatorFor(repository)
            .CloneAsync(new CloneRequest(new Uri(repository.OriginPath).AbsoluteUri, target), progress, Token);

        Assert.NotEmpty(progress.Reports);
        Assert.True(Directory.Exists(Path.Combine(target, ".git")));
    }

    [Fact]
    public async Task GivenAnEmptyExistingDirectory_WhenCloning_ThenItIsUsed()
    {
        using var repository = TestRepositories.WithOrigin();
        var target = TargetIn(repository, "empty");
        Directory.CreateDirectory(target);

        var location = await CreatorFor(repository)
            .CloneAsync(new CloneRequest(repository.OriginPath, target), null, Token);

        Assert.Equal(Path.GetFullPath(target), location.WorkingTreePath);
    }

    [Fact]
    public async Task GivenADirectoryWithContent_WhenCloning_ThenItIsRefusedAndKept()
    {
        using var repository = TestRepositories.WithOrigin();
        var target = TargetIn(repository, "full");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "mine.txt"), "mine", Token);

        await Assert.ThrowsAsync<CloneTargetException>(() => CreatorFor(repository)
            .CloneAsync(new CloneRequest(repository.OriginPath, target), null, Token));

        Assert.Equal("mine", await File.ReadAllTextAsync(Path.Combine(target, "mine.txt"), Token));
        Assert.Single(Directory.GetFileSystemEntries(target));
    }

    [Fact]
    public async Task GivenAFileAsTarget_WhenCloning_ThenItIsRefused()
    {
        using var repository = TestRepositories.WithOrigin();
        var target = TargetIn(repository, "afile");
        await File.WriteAllTextAsync(target, "x", Token);

        await Assert.ThrowsAsync<CloneTargetException>(() => CreatorFor(repository)
            .CloneAsync(new CloneRequest(repository.OriginPath, target), null, Token));
    }

    [Fact]
    public async Task GivenASourceThatDoesNotExist_WhenCloning_ThenItFailsAndLeavesNothingBehind()
    {
        using var repository = TestRepositories.WithOrigin();
        var target = TargetIn(repository, Path.Combine("parents", "dest"));

        await Assert.ThrowsAsync<GitCommandFailedException>(() => CreatorFor(repository)
            .CloneAsync(new CloneRequest(TargetIn(repository, "nothing-here.git"), target), null, Token));

        Assert.False(Directory.Exists(TargetIn(repository, "parents")));
    }

    [Fact]
    public async Task GivenACancelBeforeTheStart_WhenCloning_ThenNothingIsCreated()
    {
        using var repository = TestRepositories.WithOrigin();
        var target = TargetIn(repository, "dest");
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreatorFor(repository)
            .CloneAsync(new CloneRequest(repository.OriginPath, target), null, source.Token));

        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public async Task GivenAClonePartwayThrough_WhenCancelling_ThenTheDirectoryAndItsNewParentsAreGone()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake git is a shell script.");
        using var repository = TestRepositories.WithOrigin();
        using var fake = PartialCloneFake();
        var target = TargetIn(repository, Path.Combine("new-parent", "dest"));
        using var source = new CancellationTokenSource();

        var clone = CreatorFor(fake).CloneAsync(
            new CloneRequest("anywhere", target),
            new CancelOnProgress(source),
            source.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clone);
        Assert.False(Directory.Exists(TargetIn(repository, "new-parent")));
    }

    [Fact]
    public async Task GivenAnEmptyExistingDirectory_WhenCancellingPartwayThrough_ThenItStaysAndIsEmpty()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake git is a shell script.");
        using var repository = TestRepositories.WithOrigin();
        using var fake = PartialCloneFake();
        var target = TargetIn(repository, "empty");
        Directory.CreateDirectory(target);
        using var source = new CancellationTokenSource();

        var clone = CreatorFor(fake).CloneAsync(
            new CloneRequest("anywhere", target),
            new CancelOnProgress(source),
            source.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => clone);
        Assert.True(Directory.Exists(target));
        Assert.Empty(Directory.GetFileSystemEntries(target));
    }

    [Fact]
    public async Task GivenAnEmptyDirectory_WhenInitialising_ThenItBecomesARepository()
    {
        using var repository = TestRepositories.WithOrigin();
        var target = TargetIn(repository, "fresh");
        Directory.CreateDirectory(target);

        var result = await CreatorFor(repository).InitAsync(target, Token);

        Assert.False(result.WasRepository);
        Assert.Equal(Path.GetFullPath(target), result.Location.WorkingTreePath);
        Assert.True(Directory.Exists(Path.Combine(target, ".git")));
    }

    [Fact]
    public async Task GivenADirectoryWithFiles_WhenInitialising_ThenTheFilesStayUntouched()
    {
        using var repository = TestRepositories.WithOrigin();
        var target = TargetIn(repository, "project");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "notes.txt"), "keep me", Token);

        var result = await CreatorFor(repository).InitAsync(target, Token);

        Assert.False(result.WasRepository);
        Assert.Equal("keep me", await File.ReadAllTextAsync(Path.Combine(target, "notes.txt"), Token));
        Assert.Contains("notes.txt", repository.GitIn(target, ["status", "--porcelain"]), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAMissingNestedDirectory_WhenInitialising_ThenItIsCreated()
    {
        using var repository = TestRepositories.WithOrigin();
        var target = TargetIn(repository, Path.Combine("a", "b"));

        var result = await CreatorFor(repository).InitAsync(target, Token);

        Assert.Equal(Path.GetFullPath(target), result.Location.WorkingTreePath);
    }

    [Fact]
    public async Task GivenARepositoryRoot_WhenInitialising_ThenItIsReturnedUnchanged()
    {
        using var repository = TestRepositories.Linear(1);
        var head = repository.Head;

        var result = await CreatorFor(repository).InitAsync(repository.WorkingTreePath, Token);

        Assert.True(result.WasRepository);
        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenAFile_WhenInitialising_ThenItIsRefused()
    {
        using var repository = TestRepositories.WithOrigin();
        var target = TargetIn(repository, "afile");
        await File.WriteAllTextAsync(target, "x", Token);

        await Assert.ThrowsAsync<CloneTargetException>(() => CreatorFor(repository).InitAsync(target, Token));
    }

    // Below a folder that exists, so a test can tell what a clone created from what was there.
    private static string TargetIn(TestRepository repository, string relative)
    {
        var folder = Path.Combine(Path.GetDirectoryName(repository.OriginPath)!, "created");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, relative);
    }

    private static RepositoryCreator CreatorFor(TestRepository repository)
    {
        var runner = repository.CreateIsolatedRunner();
        return new RepositoryCreator(new RemoteRunner(runner, _ => null), runner, new RepositoryDiscovery(runner));
    }

    // The fake git is told ssh is set, so the creator does not ask it for core.sshCommand.
    private static RepositoryCreator CreatorFor(FakeGitExecutable fake)
    {
        var runner = new GitRunner(new GitRunnerOptions(fake.ExecutablePath));
        var remoteRunner = new RemoteRunner(runner, name => name == "GIT_SSH_COMMAND" ? "ssh" : null);
        return new RepositoryCreator(remoteRunner, runner, new RepositoryDiscovery(runner));
    }

    // Makes the target (the last argument) with content, reports progress, then hangs until it is killed.
    private static FakeGitExecutable PartialCloneFake() => FakeGitExecutable.Create(
        "for last; do :; done\nmkdir -p \"$last/sub\"\necho partial > \"$last/sub/file\"\n"
        + "echo 'Receiving objects:  10% (1/10)' >&2\nsleep 30",
        "ping -n 30 127.0.0.1 >nul");

    private sealed class CancelOnProgress(CancellationTokenSource source) : IProgress<RemoteProgress>
    {
        public void Report(RemoteProgress value) => source.Cancel();
    }

    private sealed class CollectingProgress : IProgress<RemoteProgress>
    {
        private readonly List<RemoteProgress> _reports = [];

        public IReadOnlyList<RemoteProgress> Reports
        {
            get
            {
                lock (_reports)
                {
                    return [.. _reports];
                }
            }
        }

        public void Report(RemoteProgress value)
        {
            lock (_reports)
            {
                _reports.Add(value);
            }
        }
    }
}
