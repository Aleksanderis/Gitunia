using Gitunia.App.Status;
using Gitunia.App;
using Gitunia.Core.Status;
using Gitunia.Core;
using Gitunia.Platform.ExternalTools;
using Gitunia.Tui.App.ExternalTools;
using Gitunia.Tui.App.Settings;
using Xunit;

namespace Gitunia.Tui.Tests.App.Settings;

public sealed class ConfigEditorLauncherTests : IDisposable
{
    private readonly List<string> _events = [];
    private readonly Dictionary<string, string> _variables = [];
    private readonly StatusReporter _statuses = new(new Dispatcher(AppState.Empty));
    private readonly FakeLauncher _launcher;
    private readonly FakeTerminal _terminal;
    private readonly string _directory = Directory.CreateTempSubdirectory("gitunia-editor-").FullName;

    public ConfigEditorLauncherTests()
    {
        _launcher = new FakeLauncher(_events);
        _terminal = new FakeTerminal(_events);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void GivenVisualAndEditor_WhenLaunching_ThenVisualWinsAndTheFileIsTheLastArgument()
    {
        _variables["VISUAL"] = "code --wait";
        _variables["EDITOR"] = "nano";
        var file = Path.Combine(_directory, "config", "config.json");

        var notice = CreateLauncher().Launch(file, () => _events.Add("reload"));

        Assert.Empty(notice);
        Assert.Equal(
            ["suspend", $"run code --wait {file} in {Path.GetDirectoryName(file)}", "resume", "reload"],
            _events);
        Assert.True(Directory.Exists(Path.GetDirectoryName(file)), "The config folder is created for the editor.");
        Assert.Equal("Editor finished", _statuses.Log.Latest?.Text);
    }

    [Fact]
    public void GivenOnlyEditor_WhenLaunching_ThenEditorIsUsed()
    {
        _variables["EDITOR"] = "nano";

        CreateLauncher().Launch(Path.Combine(_directory, "config.json"), () => { });

        Assert.StartsWith("suspend", _events[0], StringComparison.Ordinal);
        Assert.StartsWith("run nano ", _events[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GivenNoEditorVariable_WhenLaunching_ThenTheReasonIsReturnedAndNothingIsSuspended(string blank)
    {
        _variables["VISUAL"] = blank;

        var notice = CreateLauncher().Launch(Path.Combine(_directory, "config.json"), () => { });

        Assert.Equal("Set $VISUAL or $EDITOR to open config.json", notice);
        Assert.Empty(_events);
    }

    [Fact]
    public void GivenAnEditorThatFails_WhenLaunching_ThenItIsAnErrorAndTheUserIsAskedToPressAKey()
    {
        _variables["EDITOR"] = "nano";
        _launcher.Result = new ExternalToolResult(2);

        CreateLauncher().Launch(Path.Combine(_directory, "config.json"), () => _events.Add("reload"));

        Assert.Contains("pause", _events);
        Assert.Contains("reload", _events);
        Assert.Equal("Editor exited with code 2", _statuses.Log.Latest?.Text);
        Assert.Equal(StatusSeverity.Error, _statuses.Log.Latest?.Severity);
    }

    [Fact]
    public void GivenAnEditorStillRunning_WhenLaunchingAgain_ThenItIsRefused()
    {
        _variables["EDITOR"] = "nano";
        _terminal.Deferred = true;
        var launcher = CreateLauncher();
        launcher.Launch(Path.Combine(_directory, "config.json"), () => { });

        var notice = launcher.Launch(Path.Combine(_directory, "config.json"), () => { });

        Assert.Equal("The editor is already running", notice);
        Assert.Equal(["suspend"], _events);
    }

    [Fact]
    public void GivenAnUnsupportedSystem_WhenLaunching_ThenNothingIsSuspended()
    {
        _variables["EDITOR"] = "nano";
        _launcher.IsSupported = false;

        var notice = CreateLauncher().Launch(Path.Combine(_directory, "config.json"), () => { });

        Assert.Contains("not supported", notice, StringComparison.Ordinal);
        Assert.Empty(_events);
    }

    private ConfigEditorLauncher CreateLauncher() =>
        new(_launcher, _terminal, _statuses, name => _variables.GetValueOrDefault(name), new RecordingPause(_events));

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
