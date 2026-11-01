using Gitunia.Core.Workspaces;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Repositories.Models;
using Gitunia.Tui.App.Remote;
using Gitunia.Tui.Tests.Fakes;
using Xunit;

namespace Gitunia.Tui.Tests.App.Remote;

public class TerminalRetryTests
{
    private static readonly string[] FetchArguments = ["fetch", "--prune"];

    private readonly TestShell _shell = new();
    private readonly FakeTerminal _terminal = new();
    private readonly TerminalRetry _retry;
    private readonly TabId _tabId;

    public TerminalRetryTests()
    {
        _shell.Host.Dispatch(new OpenRepositoryCommand(Location("gitunia-terminal-retry-tests")));
        _tabId = _shell.Host.Workspace.ActiveTabId!.Value;
        _retry = new TerminalRetry(_shell.Host, _shell.Statuses, _terminal);
    }

    [Fact]
    public void GivenAnOffer_WhenAnsweringYes_ThenTheTerminalRunsTheSameCommand()
    {
        _retry.Offer(_tabId, "Fetch", FetchArguments);

        var prompt = Assert.Single(_shell.Prompts);
        Assert.Equal("Retry in terminal", prompt.YesLabel);
        Assert.Contains("Runs: git fetch --prune", prompt.Consequence, StringComparison.Ordinal);
        Assert.Empty(_terminal.Calls);

        _shell.Answer();

        Assert.Equal(["fetch|fetch --prune"], _terminal.Calls);
    }

    [Fact]
    public void GivenTheConfirmCannotShow_WhenOffering_ThenTheStatusNamesTheCommandToRun()
    {
        _shell.CanShowDialog = false;

        _retry.Offer(_tabId, "Fetch", FetchArguments);

        Assert.Equal(
            "Fetch in gitunia-terminal-retry-tests needs credentials: run `git fetch --prune` in a terminal",
            _shell.Statuses.Log.Latest!.Text);
    }

    [Fact]
    public void GivenAnotherOverlayIsOpen_WhenOffering_ThenNoConfirmReplacesItAndTheStatusSaysHow()
    {
        _terminal.OverlayOpen = true;

        _retry.Offer(_tabId, "Fetch", FetchArguments);

        Assert.Empty(_shell.Prompts);
        Assert.Contains(
            "run `git fetch --prune` in a terminal",
            _shell.Statuses.Log.Latest!.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheTabIsNotActive_WhenOffering_ThenNoConfirmIsShown()
    {
        _shell.Host.Dispatch(new OpenRepositoryCommand(Location("gitunia-terminal-retry-other")));

        _retry.Offer(_tabId, "Fetch", FetchArguments);

        Assert.Empty(_shell.Prompts);
        Assert.Contains("needs credentials", _shell.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheTabIsClosed_WhenOffering_ThenNothingHappens()
    {
        _shell.Host.Dispatch(new CloseTabCommand(_tabId));

        _retry.Offer(_tabId, "Fetch", FetchArguments);

        Assert.Empty(_shell.Prompts);
        Assert.Null(_shell.Statuses.Log.Latest);
    }

    [Fact]
    public void GivenAnOperationStartedWhileTheConfirmWasOpen_WhenAnswering_ThenTheTerminalDoesNotRaceIt()
    {
        _retry.Offer(_tabId, "Fetch", FetchArguments);
        _terminal.Busy = true;

        _shell.Answer();

        Assert.Empty(_terminal.Calls);
        Assert.StartsWith("Busy", _shell.Statuses.Log.Latest!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheTabIsClosedWhileTheConfirmIsOpen_WhenAnswering_ThenTheTerminalIsNotStarted()
    {
        _retry.Offer(_tabId, "Fetch", FetchArguments);

        _shell.Host.Dispatch(new CloseTabCommand(_tabId));
        _shell.Answer();

        Assert.Empty(_terminal.Calls);
    }

    [Fact]
    public void GivenTheTerminalCannotStart_WhenRetrying_ThenTheNoticeIsShown()
    {
        _terminal.Notice = "External tools are not supported on this operating system";
        _retry.Offer(_tabId, "Fetch", FetchArguments);

        _shell.Answer();

        Assert.Equal(_terminal.Notice, _shell.Statuses.Log.Latest!.Text);
    }

    private static RepositoryLocation Location(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), name);
        var gitDirectory = Path.Combine(root, ".git");
        return new RepositoryLocation(root, gitDirectory, gitDirectory, false, false);
    }

    private sealed class FakeTerminal : ITerminalRetryHost
    {
        public List<string> Calls { get; } = [];

        public bool OverlayOpen { get; set; }

        public bool Busy { get; set; }

        public string Notice { get; set; } = string.Empty;

        public bool IsOverlayOpen() => OverlayOpen;

        public bool IsBusy(TabId tabId) => Busy;

        public string RunGitInTerminal(string name, IReadOnlyList<string> arguments, RepositoryLocation location)
        {
            Calls.Add($"{name}|{string.Join(' ', arguments)}");
            return Notice;
        }
    }
}
