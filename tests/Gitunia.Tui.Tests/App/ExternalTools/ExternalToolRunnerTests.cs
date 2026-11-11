using Gitunia.App.Status;
using Gitunia.App;
using Gitunia.Core.Status;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Workspaces;
using Gitunia.Git.ExternalTools;
using Gitunia.Git.Repositories.Models;
using Gitunia.Platform.ExternalTools;
using Gitunia.Tui.App.ExternalTools;
using Xunit;

namespace Gitunia.Tui.Tests.App.ExternalTools;

public class ExternalToolRunnerTests
{
    private static readonly RepositoryLocation Location = new("/r", "/r/.git", "/r/.git", false, false);

    private readonly List<string> _events = [];
    private readonly Dispatcher _host = new(AppState.Empty);
    private readonly StatusReporter _statuses;
    private readonly FakeLauncher _launcher;
    private readonly FakeTerminal _terminal;

    public ExternalToolRunnerTests()
    {
        _statuses = new StatusReporter(_host);
        _launcher = new FakeLauncher(_events);
        _terminal = new FakeTerminal(_events);
        _host.Dispatch(new OpenRepositoryCommand(Location));
        _host.EffectRaised += effect =>
        {
            if (effect is RefreshTabEffect)
            {
                _events.Add("refresh");
            }
        };
    }

    [Fact]
    public void GivenADifftool_WhenLaunching_ThenTheTerminalIsSuspendedAroundTheToolAndTheTabRefreshed()
    {
        var runner = CreateRunner();

        var notice = runner.Launch(ExternalToolKind.Difftool, Location, "abc");

        Assert.Empty(notice);
        Assert.DoesNotContain("pause", _events);
        Assert.Equal(["suspend", "run git -C /r difftool --no-prompt abc^1 abc in /r", "resume", "refresh"], _events);
        Assert.Equal("difftool finished", _statuses.Log.Latest?.Text);
    }

    [Fact]
    public void GivenAGitCommand_WhenLaunchingItInTheTerminal_ThenItRunsInTheRepositoryAndIsNamedInTheStatus()
    {
        var notice = CreateRunner().LaunchGit("fetch", ["fetch", "--prune"], Location);

        Assert.Empty(notice);
        Assert.Equal(["suspend", "run git -C /r fetch --prune in /r", "resume", "refresh"], _events);
        Assert.Equal("fetch finished", _statuses.Log.Latest?.Text);
    }

    [Fact]
    public void GivenALocatedGit_WhenLaunchingInTheTerminal_ThenTheToolRunsThatExecutable()
    {
        var located = Path.Combine(Path.GetTempPath(), "located", "git");
        var runner = new ExternalToolRunner(
            _launcher,
            _terminal,
            _statuses,
            _host,
            new RecordingPause(_events),
            located);

        runner.LaunchGit("fetch", ["fetch"], Location);

        Assert.Contains($"run {located} -C /r fetch in /r", _events);
    }

    [Fact]
    public void GivenAFailingTool_WhenLaunching_ThenTheExitCodeIsAnError()
    {
        _launcher.Result = new ExternalToolResult(3);

        CreateRunner().Launch(ExternalToolKind.Mergetool, Location);

        Assert.Contains("pause", _events);
        Assert.Equal("mergetool exited with code 3", _statuses.Log.Latest?.Text);
        Assert.Equal(StatusSeverity.Error, _statuses.Log.Latest?.Severity);
    }

    [Fact]
    public void GivenAToolThatIsStillRunning_WhenLaunchingAgain_ThenItIsRefused()
    {
        _terminal.Deferred = true;
        var runner = CreateRunner();
        runner.Launch(ExternalToolKind.CommitWithEditor, Location);

        var notice = runner.Launch(ExternalToolKind.CommitWithEditor, Location);

        Assert.Equal("An external tool is already running", notice);
        Assert.Equal(["suspend"], _events);
    }

    [Fact]
    public void GivenAnUnsupportedSystem_WhenLaunching_ThenNothingIsSuspended()
    {
        _launcher.IsSupported = false;

        var notice = CreateRunner().Launch(ExternalToolKind.CommitWithEditor, Location);

        Assert.Contains("not supported", notice);
        Assert.Empty(_events);
    }

    private ExternalToolRunner CreateRunner() =>
        new(_launcher, _terminal, _statuses, _host, new RecordingPause(_events));

    private sealed class FakeLauncher(List<string> events) : IExternalToolLauncher
    {
        public bool IsSupported { get; set; } = true;

        public ExternalToolResult Result { get; set; } = new(0);

        public ExternalToolResult Run(ExternalToolRequest request)
        {
            events.Add($"run {request.FileName} {string.Join(' ', request.Arguments)} in {request.WorkingDirectory}");

            return Result;
        }
    }

    private sealed class FakeTerminal(List<string> events) : ITerminalSuspender
    {
        public bool Deferred { get; set; }

        public void Suspend(Action work, Action resumed)
        {
            events.Add("suspend");
            if (Deferred)
            {
                return;
            }

            work();
            events.Add("resume");
            resumed();
        }
    }

    private sealed class RecordingPause(List<string> events) : IUserPause
    {
        public void Wait() => events.Add("pause");
    }
}
