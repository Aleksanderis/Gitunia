using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution;
using Gitunia.Git.Remotes;
using Gitunia.Git.Repositories;
using Gitunia.Git.Tests.Execution;
using Gitunia.Git.Tests.Fixtures;
using Gitunia.Tui.App;
using Gitunia.Tui.App.Creation;
using Gitunia.Tui.App.Overlays;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Creation;

public class RepositoryCreationActionsTests
{
    [Fact]
    public async Task GivenALocalBareRepository_WhenCloningFromThePicker_ThenTheCloneOpensInANewTab()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);
        var target = harness.Target("clone-me");

        harness.Run(ActionIds.PickerClone);
        Assert.Contains("URL or path", PopupText.Of(harness.Actions), StringComparison.Ordinal);
        Assert.Equal(KeyMode.Insert, harness.Mode);
        harness.Type(repository.OriginPath);
        harness.Run(ActionIds.PickerAccept);
        Assert.Contains(repository.OriginPath, PopupText.Of(harness.Actions), StringComparison.Ordinal);
        harness.Replace(target);
        harness.Run(ActionIds.PickerAccept);
        await harness.Pump.RunUntilAsync(() => harness.Host.Workspace.Tabs.Length == 1);

        Assert.Equal(Path.GetFullPath(target), harness.Host.Workspace.ActiveTab!.Location.RootPath);
        Assert.Equal(repository.Head, repository.GitIn(target, ["rev-parse", "HEAD"]).Trim());
        Assert.False(harness.Picker.IsOpen);
        Assert.False(harness.Actions.IsCloning);
        Assert.Equal("Cloned clone-me", harness.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public void GivenAPastedUrl_WhenInTheClonePrompt_ThenItIsInsertedAndALineBreakDoesNotSubmit()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);

        harness.Run(ActionIds.PickerClone);
        harness.Paste(repository.OriginPath + "\r\n");

        Assert.Contains($"> {repository.OriginPath}_", PopupText.Of(harness.Actions), StringComparison.Ordinal);
        Assert.Contains("URL or path", PopupText.Of(harness.Actions), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenASourceName_WhenAskingForTheDirectory_ThenItIsPrefilledWithTheRepositoryName()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);

        harness.Run(ActionIds.PickerClone);
        harness.Type(repository.OriginPath);
        harness.Run(ActionIds.PickerAccept);

        Assert.Contains(
            harness.Shown(Path.Combine(harness.DefaultParent, "origin")),
            PopupText.Of(harness.Actions),
            StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFolderTypedInThePicker_WhenAskingForTheDirectory_ThenTheCloneGoesBelowIt()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);
        var folder = harness.Target("workspace");
        Directory.CreateDirectory(folder);
        harness.Picker.Open(initialQuery: folder + Path.DirectorySeparatorChar);

        harness.Run(ActionIds.PickerClone);
        harness.Type(repository.OriginPath);
        harness.Run(ActionIds.PickerAccept);

        Assert.Contains(
            harness.Shown(Path.Combine(folder, "origin")),
            PopupText.Of(harness.Actions),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAClonePartwayThrough_WhenPressingEscape_ThenItStopsAndLeavesNothingAndNoTab()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake git is a shell script.");
        using var repository = TestRepositories.WithOrigin();
        using var fake = FakeGitExecutable.Create(
            "for last; do :; done\nmkdir -p \"$last/sub\"\necho partial > \"$last/sub/file\"\n"
            + "echo 'Receiving objects:  10% (1/10)' >&2\nsleep 30",
            "ping -n 30 127.0.0.1 >nul");
        using var harness = Harness.For(repository, fake);
        var target = harness.Target("slow");

        harness.StartClone("anywhere", target);
        await harness.Pump.RunUntilAsync(
            () => harness.Actions.ProgressLine().Contains("10%", StringComparison.Ordinal));
        Assert.Equal("Cloning: Receiving objects 10%", harness.Actions.ProgressLine());
        Assert.True(harness.Actions.TryCancelWithEscape(ActionIds.Cancel));
        await harness.Pump.RunUntilAsync(() => !harness.Actions.IsCloning);

        Assert.False(Directory.Exists(target));
        Assert.Empty(harness.Host.Workspace.Tabs);
        Assert.Equal(StatusSeverity.Warning, harness.Statuses.Log.Latest!.Severity);
        Assert.Contains("cancelled", harness.Statuses.Log.Latest.Text, StringComparison.Ordinal);
        Assert.True(harness.Picker.IsOpen);
    }

    [Fact]
    public async Task GivenAClonePartwayThrough_WhenTheCancelChipIsClicked_ThenItStops()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake git is a shell script.");
        using var repository = TestRepositories.WithOrigin();
        using var fake = FakeGitExecutable.Sleeping();
        using var harness = Harness.For(repository, fake);
        var target = harness.Target("slow");

        harness.StartClone("anywhere", target);
        Assert.True(harness.Actions.IsCloning);
        Assert.True(harness.Actions.TryExecute(ActionIds.RemoteCancel, null));
        Assert.EndsWith("(cancelling)", harness.Actions.ProgressLine(), StringComparison.Ordinal);
        await harness.Pump.RunUntilAsync(() => !harness.Actions.IsCloning);

        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void GivenNoClone_WhenPressingEscape_ThenItIsNotClaimed()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);

        Assert.False(harness.Actions.TryCancelWithEscape(ActionIds.Cancel));
        Assert.False(harness.Actions.TryExecute(ActionIds.RemoteCancel, null));
    }

    [Fact]
    public async Task GivenASourceThatDoesNotExist_WhenCloning_ThenTheFailureIsReportedAndThePickerStays()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);
        var target = harness.Target("never");

        harness.StartClone(harness.Target("no-such-source.git"), target);
        await harness.Pump.RunUntilAsync(() => !harness.Actions.IsCloning);

        Assert.StartsWith("Clone failed:", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest.Severity);
        Assert.False(Directory.Exists(target));
        Assert.Empty(harness.Host.Workspace.Tabs);
        Assert.True(harness.Picker.IsOpen);
    }

    [Fact]
    public async Task GivenADirectoryWithContent_WhenChoosingTheCloneTarget_ThenItIsRefusedAndTheFilesStay()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);
        var target = harness.Target("busy-dir");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "mine.txt"), "mine");

        harness.Run(ActionIds.PickerClone);
        harness.Type(repository.OriginPath);
        harness.Run(ActionIds.PickerAccept);
        harness.Replace(target);
        harness.Run(ActionIds.PickerAccept);

        await harness.Pump.RunUntilAsync(() => !harness.Actions.IsCloning);

        Assert.Contains("is not empty", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(StatusSeverity.Error, harness.Statuses.Log.Latest.Severity);
        Assert.Empty(harness.Host.Workspace.Tabs);
        Assert.Equal("mine", File.ReadAllText(Path.Combine(target, "mine.txt")));
    }

    [Fact]
    public void GivenAnEmptySource_WhenAccepting_ThenItAsksAgain()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);

        harness.Run(ActionIds.PickerClone);
        harness.Run(ActionIds.PickerAccept);

        Assert.Equal("Type something first", harness.Statuses.Log.Latest!.Text);
        Assert.Contains("URL or path", PopupText.Of(harness.Actions), StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnOpenPrompt_WhenPressingEscape_ThenOnlyThePromptClosesAndThePickerStays()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);

        harness.Run(ActionIds.PickerClone);
        harness.Run(ActionIds.Cancel);

        Assert.Null(harness.Actions.Popup);
        Assert.True(harness.Picker.IsOpen);
        Assert.Equal(KeyMode.Insert, harness.Mode);
    }

    [Fact]
    public void GivenAnOpenPrompt_WhenAPickerKeyIsPressed_ThenThePickerDoesNotActOnIt()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);

        harness.Run(ActionIds.PickerClone);

        Assert.True(harness.Actions.TryExecute(ActionIds.PickerNext, null));
        Assert.True(harness.Actions.TryExecute(ActionIds.PickerPin, null));
        Assert.False(harness.Actions.TryExecute(ActionIds.GraphJumpToHead, null));
    }

    [Fact]
    public void GivenTheLocateMode_WhenCloning_ThenItIsRefused()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);
        harness.Picker.Open(new TabId(1));

        harness.Run(ActionIds.PickerClone);

        Assert.Equal("Not while locating a repository", harness.Statuses.Log.Latest!.Text);
        Assert.Null(harness.Actions.Popup);
    }

    [Fact]
    public async Task GivenAnEmptyDirectory_WhenInitialising_ThenItBecomesARepositoryInANewTab()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);
        var target = harness.Target("fresh");
        Directory.CreateDirectory(target);

        harness.Run(ActionIds.PickerInit);
        harness.Replace(target);
        harness.Run(ActionIds.PickerAccept);
        await harness.Pump.RunUntilAsync(() => harness.Host.Workspace.Tabs.Length == 1);

        Assert.True(Directory.Exists(Path.Combine(target, ".git")));
        Assert.Equal(Path.GetFullPath(target), harness.Host.Workspace.ActiveTab!.Location.RootPath);
        Assert.Equal("Initialised fresh", harness.Statuses.Log.Latest!.Text);
        Assert.False(harness.Picker.IsOpen);
    }

    [Fact]
    public async Task GivenADirectoryWithFiles_WhenInitialising_ThenItOpensAndTheFilesStay()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);
        var target = harness.Target("project");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "notes.txt"), "keep me");

        harness.Run(ActionIds.PickerInit);
        harness.Replace(target);
        harness.Run(ActionIds.InputAccept);
        await harness.Pump.RunUntilAsync(() => harness.Host.Workspace.Tabs.Length == 1);

        Assert.Equal("keep me", File.ReadAllText(Path.Combine(target, "notes.txt")));
        Assert.True(Directory.Exists(Path.Combine(target, ".git")));
    }

    [Fact]
    public async Task GivenAMissingDirectory_WhenInitialising_ThenItIsCreated()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);
        var target = Path.Combine(harness.Target("outer"), "inner");

        harness.Run(ActionIds.PickerInit);
        harness.Replace(target);
        harness.Run(ActionIds.PickerAccept);
        await harness.Pump.RunUntilAsync(() => harness.Host.Workspace.Tabs.Length == 1);

        Assert.True(Directory.Exists(Path.Combine(target, ".git")));
    }

    [Fact]
    public void GivenAPathTypedInThePicker_WhenInitialising_ThenThePromptStartsWithIt()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);
        var folder = harness.Target("typed");
        harness.Picker.Open(initialQuery: folder);

        harness.Run(ActionIds.PickerInit);

        Assert.Contains($"> {harness.Shown(folder)}_", PopupText.Of(harness.Actions), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenARepositoryRoot_WhenInitialising_ThenItJustOpensIt()
    {
        using var repository = TestRepositories.Linear(1);
        using var harness = Harness.For(repository);
        var head = repository.Head;

        harness.Run(ActionIds.PickerInit);
        harness.Replace(repository.WorkingTreePath);
        harness.Run(ActionIds.PickerAccept);
        await harness.Pump.RunUntilAsync(() => harness.Host.Workspace.Tabs.Length == 1);

        Assert.Contains("already is a repository", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Equal(head, repository.Head);
    }

    [Fact]
    public async Task GivenAFileAsTheInitTarget_WhenInitialising_ThenTheFailureIsReportedAndNoTabOpens()
    {
        using var repository = TestRepositories.WithOrigin();
        using var harness = Harness.For(repository);
        var target = harness.Target("a-file");
        File.WriteAllText(target, "x");

        harness.Run(ActionIds.PickerInit);
        harness.Replace(target);
        harness.Run(ActionIds.PickerAccept);

        await harness.Pump.RunUntilAsync(() => harness.Statuses.Log.Latest is not null);

        Assert.Contains("is a file", harness.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
        Assert.Empty(harness.Host.Workspace.Tabs);
    }

    [Fact]
    public void GivenAClone_WhenStartingAnotherOne_ThenItIsRefusedAsBusy()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake git is a shell script.");
        using var repository = TestRepositories.WithOrigin();
        using var fake = FakeGitExecutable.Sleeping();
        using var harness = Harness.For(repository, fake);

        harness.StartClone("anywhere", harness.Target("first"));
        harness.Run(ActionIds.PickerClone);

        Assert.Equal("Busy: a clone or init is still running", harness.Statuses.Log.Latest!.Text);
        Assert.Null(harness.Actions.Popup);
        harness.Actions.CancelAndWait();
    }

    private sealed class Harness : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("gitunia-create-").FullName;

        private Harness(IRepositoryCreator creator)
        {
            Host = new WorkspaceHost(Workspace.Empty);
            Pump = new UiPump(TestContext.Current.CancellationToken);
            Picker = new PickerController(
                TestGit.Services.Discovery,
                Host,
                _root,
                Pump.Post,
                scanningDelay: Timeout.InfiniteTimeSpan,
                scanRoots: []);
            Picker.Open();
            DefaultParent = Path.Combine(_root, "code");
            Actions = new RepositoryCreationActions(
                creator,
                Picker,
                Statuses,
                Pump.Post,
                mode => Mode = mode,
                () => { },
                _root,
                () => DefaultParent);
            Mode = KeyMode.Insert;
        }

        public WorkspaceHost Host { get; }

        public UiPump Pump { get; }

        public PickerController Picker { get; }

        public RepositoryCreationActions Actions { get; }

        public StatusChannel Statuses { get; } = new();

        public KeyMode Mode { get; private set; }

        public string DefaultParent { get; }

        public static Harness For(TestRepository repository, FakeGitExecutable? fake = null)
        {
            var runner = fake is null
                ? repository.CreateIsolatedRunner()
                : new GitRunner(new GitRunnerOptions(fake.ExecutablePath));
            var remote = new RemoteRunner(runner, name => fake is not null && name == "GIT_SSH_COMMAND" ? "ssh" : null);
            var discovery = new RepositoryDiscovery(repository.CreateIsolatedRunner());
            return new Harness(new RepositoryCreator(remote, runner, discovery));
        }

        public string Target(string name)
        {
            Directory.CreateDirectory(Path.Combine(_root, "targets"));
            return Path.Combine(_root, "targets", name);
        }

        // The prompts show a path below the home folder (the test root) as ~/..., except on Windows (native path).
        public string Shown(string path) =>
            OperatingSystem.IsWindows() ? path : $"~{Path.DirectorySeparatorChar}{Path.GetRelativePath(_root, path)}";

        public void Run(ActionId action) => Assert.True(Actions.TryExecute(action, null), action.Value);

        // Opens the clone prompts and answers them, as the user does.
        public void StartClone(string source, string target)
        {
            Run(ActionIds.PickerClone);
            Type(source);
            Run(ActionIds.PickerAccept);
            Replace(target);
            Run(ActionIds.PickerAccept);
        }

        public void Type(string text)
        {
            foreach (var character in text)
            {
                Assert.True(Actions.HandleTextKey(new KeyEvent(character.ToString())));
            }
        }

        public void Paste(string text)
        {
            foreach (var key in PasteKeys.FromText(text, multiLine: false))
            {
                Assert.True(Actions.HandleTextKey(key));
            }
        }

        public void Replace(string text)
        {
            Assert.True(Actions.HandleTextKey(new KeyEvent("u", KeyModifiers.Ctrl)));
            Type(text);
        }

        public void Dispose()
        {
            Actions.CancelAndWait();
            Directory.Delete(_root, recursive: true);
        }
    }
}
