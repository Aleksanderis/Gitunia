using Gitunia.App.Tests.Picker;
using Gitunia.Core.Creation;
using Gitunia.Core.Picker;
using Gitunia.Core.Prompts;
using Gitunia.Core.Status;
using Gitunia.Git.Execution;
using Gitunia.Git.Remotes;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Execution;
using Gitunia.Git.Tests.Fixtures;
using Xunit;

namespace Gitunia.App.Tests.Creation;

// Clone and init from the picker through the real runner and the real creator on temporary repositories.
public sealed class CreationEffectHandlerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("gitunia-create-").FullName;

    [Fact]
    public async Task GivenALocalBareRepository_WhenCloningFromThePicker_ThenTheCloneOpensInANewTab()
    {
        using var repository = TestRepositories.WithOrigin();
        using var pipeline = Pipeline(repository);
        var target = Target("clone-me");

        pipeline.Open();
        pipeline.Dispatch(new CreationRequested(CreationKind.Clone));
        Assert.Contains("URL or path", pipeline.Prompt!.Context, StringComparison.Ordinal);
        pipeline.TypePrompt(repository.OriginPath);
        pipeline.Dispatch(new PromptAcceptedCommand());
        await pipeline.Pump.RunUntilAsync(() => pipeline.Prompt is not null);
        Assert.Contains(repository.OriginPath, pipeline.Prompt!.Context, StringComparison.Ordinal);
        pipeline.ReplacePrompt(target);
        pipeline.Dispatch(new PromptAcceptedCommand());
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.Workspace.Tabs.Length == 1);

        Assert.Equal(Path.GetFullPath(target), pipeline.Host.Workspace.ActiveTab!.Location.RootPath);
        Assert.Equal(repository.Head, repository.GitIn(target, ["rev-parse", "HEAD"]).Trim());
        Assert.False(pipeline.IsOpen);
        Assert.False(pipeline.IsCloning);
        Assert.Equal("Cloned clone-me", pipeline.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public async Task GivenASourceName_WhenAskingForTheDirectory_ThenItIsPrefilledWithTheRepositoryName()
    {
        using var repository = TestRepositories.WithOrigin();
        using var pipeline = Pipeline(repository);
        var code = Path.Combine(_root, "code");
        Directory.CreateDirectory(code);
        pipeline.Dispatch(new PickerScanRootsChanged([code]));

        pipeline.Open();
        await AskForTarget(pipeline, repository.OriginPath);

        Assert.Equal(Shown(Path.Combine(_root, "code", "origin")), pipeline.Prompt!.Text);
    }

    [Fact]
    public async Task GivenNoScanRootThatExists_WhenAskingForTheDirectory_ThenTheCloneGoesIntoHome()
    {
        using var repository = TestRepositories.WithOrigin();
        using var pipeline = Pipeline(repository);

        pipeline.Open();
        await AskForTarget(pipeline, repository.OriginPath);

        Assert.Equal(Shown(Path.Combine(_root, "origin")), pipeline.Prompt!.Text);
    }

    [Fact]
    public async Task GivenAFolderTypedInThePicker_WhenAskingForTheDirectory_ThenTheCloneGoesBelowIt()
    {
        using var repository = TestRepositories.WithOrigin();
        using var pipeline = Pipeline(repository);
        var folder = Target("workspace");
        Directory.CreateDirectory(folder);
        pipeline.Open(folder + Path.DirectorySeparatorChar);

        await AskForTarget(pipeline, repository.OriginPath);

        Assert.Equal(Shown(Path.Combine(folder, "origin")), pipeline.Prompt!.Text);
    }

    [Fact]
    public async Task GivenATypedNameThatIsNoFolder_WhenAskingForTheDirectory_ThenTheCloneGoesNextToIt()
    {
        using var repository = TestRepositories.WithOrigin();
        using var pipeline = Pipeline(repository);
        Directory.CreateDirectory(Target("workspace"));
        pipeline.Open(Target("workspace") + Path.DirectorySeparatorChar + "typed-name");

        await AskForTarget(pipeline, repository.OriginPath);

        Assert.Equal(Shown(Path.Combine(Target("workspace"), "origin")), pipeline.Prompt!.Text);
    }

    [Fact]
    public async Task GivenAClonePartwayThrough_WhenCancelling_ThenItStopsAndLeavesNothingAndNoTab()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake git is a shell script.");
        using var repository = TestRepositories.WithOrigin();
        using var fake = FakeGitExecutable.Create(
            "for last; do :; done\nmkdir -p \"$last/sub\"\necho partial > \"$last/sub/file\"\n"
            + "echo 'Receiving objects:  10% (1/10)' >&2\nsleep 30",
            "ping -n 30 127.0.0.1 >nul");
        using var pipeline = Pipeline(repository, fake);
        var target = Target("slow");

        pipeline.Open();
        await StartClone(pipeline, "anywhere", target);
        await pipeline.Pump.RunUntilAsync(() => ProgressLine(pipeline).Contains("10%", StringComparison.Ordinal));
        Assert.Equal("Cloning: Receiving objects 10%", ProgressLine(pipeline));
        pipeline.Dispatch(new CloneCancelRequested());
        Assert.EndsWith("(cancelling)", ProgressLine(pipeline), StringComparison.Ordinal);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsCloning);

        Assert.False(Directory.Exists(target));
        Assert.Empty(pipeline.Host.Workspace.Tabs);
        Assert.Equal(StatusSeverity.Warning, pipeline.Statuses.Log.Latest!.Severity);
        Assert.Contains("cancelled", pipeline.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.True(pipeline.IsOpen);
    }

    [Fact]
    public async Task GivenASourceThatDoesNotExist_WhenCloning_ThenTheFailureIsReportedAndThePickerStays()
    {
        using var repository = TestRepositories.WithOrigin();
        using var pipeline = Pipeline(repository);
        var target = Target("never");

        pipeline.Open();
        await StartClone(pipeline, Target("no-such-source.git"), target);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsCloning);

        Assert.StartsWith("Clone failed:", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Error, pipeline.Statuses.Log.Latest.Severity);
        Assert.False(Directory.Exists(target));
        Assert.Empty(pipeline.Host.Workspace.Tabs);
        Assert.True(pipeline.IsOpen);
    }

    [Fact]
    public async Task GivenADirectoryWithContent_WhenChoosingTheCloneTarget_ThenItIsRefusedAndTheFilesStay()
    {
        using var repository = TestRepositories.WithOrigin();
        using var pipeline = Pipeline(repository);
        var target = Target("busy-dir");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "mine.txt"), "mine");

        pipeline.Open();
        await StartClone(pipeline, repository.OriginPath, target);
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsCloning);

        Assert.Contains("is not empty", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Error, pipeline.Statuses.Log.Latest.Severity);
        Assert.Empty(pipeline.Host.Workspace.Tabs);
        Assert.Equal("mine", File.ReadAllText(Path.Combine(target, "mine.txt")));
    }

    [Fact]
    public async Task GivenAnEmptyDirectory_WhenInitialising_ThenItBecomesARepositoryInANewTab()
    {
        using var repository = TestRepositories.WithOrigin();
        using var pipeline = Pipeline(repository);
        var target = Target("fresh");
        Directory.CreateDirectory(target);

        pipeline.Open();
        InitAt(pipeline, target);
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.Workspace.Tabs.Length == 1);

        Assert.True(Directory.Exists(Path.Combine(target, ".git")));
        Assert.Equal(Path.GetFullPath(target), pipeline.Host.Workspace.ActiveTab!.Location.RootPath);
        Assert.Equal("Initialised fresh", pipeline.Statuses.Log.Latest!.Text);
        Assert.False(pipeline.IsOpen);
    }

    [Fact]
    public async Task GivenADirectoryWithFiles_WhenInitialising_ThenItOpensAndTheFilesStay()
    {
        using var repository = TestRepositories.WithOrigin();
        using var pipeline = Pipeline(repository);
        var target = Target("project");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "notes.txt"), "keep me");

        pipeline.Open();
        InitAt(pipeline, target);
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.Workspace.Tabs.Length == 1);

        Assert.Equal("keep me", File.ReadAllText(Path.Combine(target, "notes.txt")));
        Assert.True(Directory.Exists(Path.Combine(target, ".git")));
    }

    [Fact]
    public async Task GivenAMissingDirectory_WhenInitialising_ThenItIsCreated()
    {
        using var repository = TestRepositories.WithOrigin();
        using var pipeline = Pipeline(repository);
        var target = Path.Combine(Target("outer"), "inner");

        pipeline.Open();
        InitAt(pipeline, target);
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.Workspace.Tabs.Length == 1);

        Assert.True(Directory.Exists(Path.Combine(target, ".git")));
    }

    [Fact]
    public async Task GivenARepositoryRoot_WhenInitialising_ThenItJustOpensIt()
    {
        using var repository = TestRepositories.Linear(1);
        using var pipeline = Pipeline(repository);
        var head = repository.Head;

        pipeline.Open();
        InitAt(pipeline, repository.WorkingTreePath);
        await pipeline.Pump.RunUntilAsync(() => pipeline.Host.Workspace.Tabs.Length == 1);

        Assert.Contains("already is a repository", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenAFileAsTheInitTarget_WhenInitialising_ThenTheFailureIsReportedAndNoTabOpens()
    {
        using var repository = TestRepositories.WithOrigin();
        using var pipeline = Pipeline(repository);
        var target = Target("a-file");
        File.WriteAllText(target, "x");

        pipeline.Open();
        InitAt(pipeline, target);
        await pipeline.Pump.RunUntilAsync(() => pipeline.Statuses.Log.Latest is not null && !IsInitRunning(pipeline));

        Assert.Contains("is a file", pipeline.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.StartsWith("Init failed:", pipeline.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.Empty(pipeline.Host.Workspace.Tabs);
    }

    [Fact]
    public async Task GivenAClone_WhenStartingAnotherOne_ThenItIsRefusedAsBusy()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake git is a shell script.");
        using var repository = TestRepositories.WithOrigin();
        using var fake = FakeGitExecutable.Sleeping();
        using var pipeline = Pipeline(repository, fake);

        pipeline.Open();
        await StartClone(pipeline, "anywhere", Target("first"));
        pipeline.Dispatch(new CreationRequested(CreationKind.Clone));

        Assert.Equal("Busy: a clone or init is still running", pipeline.Statuses.Log.Latest!.Text);
        Assert.Null(pipeline.Prompt);
        pipeline.Dispatch(new CloneCancelRequested());
        await pipeline.Pump.RunUntilAsync(() => !pipeline.IsCloning);
    }

    [Fact]
    public async Task GivenARunningClone_WhenTheRunnerIsDisposed_ThenTheCloneIsStoppedAndCleanedUp()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake git is a shell script.");
        using var repository = TestRepositories.WithOrigin();
        using var fake = FakeGitExecutable.Sleeping();
        var pipeline = Pipeline(repository, fake);
        var target = Target("quit-while-cloning");
        pipeline.Open();
        await StartClone(pipeline, "anywhere", target);

        pipeline.Dispose();

        Assert.False(Directory.Exists(target));
    }

    public void Dispose() => TestRepository.DeleteDirectory(_root);

    private static string ProgressLine(PickerPipeline pipeline) =>
        pipeline.Host.State.Shell.Creation.RunningClone?.ProgressLine ?? string.Empty;

    private static bool IsInitRunning(PickerPipeline pipeline) => pipeline.Host.State.Shell.Creation.IsInitRunning;

    private PickerPipeline Pipeline(TestRepository repository, FakeGitExecutable? fake = null)
    {
        var runner = repository.CreateIsolatedRunner();
        if (fake is not null)
        {
            runner = new GitRunner(new GitRunnerOptions(fake.ExecutablePath));
        }

        var remote = new RemoteRunner(runner, name => fake is not null && name == "GIT_SSH_COMMAND" ? "ssh" : null);
        var discovery = new RepositoryDiscovery(repository.CreateIsolatedRunner());
        var creator = new RepositoryCreator(remote, runner, discovery);

        return PickerPipeline.Create(_root, creator: creator);
    }

    private string Target(string name)
    {
        Directory.CreateDirectory(Path.Combine(_root, "targets"));

        return Path.Combine(_root, "targets", name);
    }

    // The prompts show a path below the home folder (the test root) as ~/..., except on Windows (native path).
    private string Shown(string path) =>
        OperatingSystem.IsWindows() ? path : $"~{Path.DirectorySeparatorChar}{Path.GetRelativePath(_root, path)}";

    private static async Task AskForTarget(PickerPipeline pipeline, string source)
    {
        pipeline.Dispatch(new CreationRequested(CreationKind.Clone));
        pipeline.TypePrompt(source);
        pipeline.Dispatch(new PromptAcceptedCommand());
        await pipeline.Pump.RunUntilAsync(() => pipeline.Prompt is not null);
    }

    // Opens the clone prompts and answers them, as the user does.
    private static async Task StartClone(PickerPipeline pipeline, string source, string target)
    {
        await AskForTarget(pipeline, source);
        pipeline.ReplacePrompt(target);
        pipeline.Dispatch(new PromptAcceptedCommand());
    }

    private static void InitAt(PickerPipeline pipeline, string directory)
    {
        pipeline.Dispatch(new CreationRequested(CreationKind.Init));
        pipeline.ReplacePrompt(directory);
        pipeline.Dispatch(new PromptAcceptedCommand());
    }
}
