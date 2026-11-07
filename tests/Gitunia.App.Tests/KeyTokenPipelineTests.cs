using Gitunia.Core.Workspaces.Tabs;
using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Operations;
using Gitunia.Core.Overlays;
using Gitunia.Core.Rebase;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.App.Tests;

// The runner resolves the key tokens of the texts against the running keymap before they are shown (#1196).
[Trait("Os", "Independent")]
public sealed class KeyTokenPipelineTests : IDisposable
{
    private const string Expected = "Afterwards the message history (<space>zz) shows how to bring it back.";

    private static readonly RebaseTarget Target = new(new TabId(1), "abc", "main", "topic", "def");

    private readonly Dispatcher _host;
    private readonly EffectRunner<TabSession> _runner;

    public KeyTokenPipelineTests()
    {
        var pump = new UiPump(TestContext.Current.CancellationToken);
        _host = new Dispatcher(AppState.Empty, pump);
        var table = new KeyBindingTable(
        [
            new KeyBinding(
                KeyMode.Normal,
                KeyBindingTable.GlobalView,
                KeySequenceParser.Parse("<space>zz"),
                ActionIds.StatusHistory),
            new KeyBinding(
                KeyMode.Normal,
                KeyBindingTable.GlobalView,
                KeySequenceParser.Parse("<space>zc"),
                ActionIds.OperationContinue),
            new KeyBinding(
                KeyMode.Normal,
                KeyBindingTable.GlobalView,
                KeySequenceParser.Parse("<space>za"),
                ActionIds.OperationAbort),
        ]);
        _runner = new EffectRunner<TabSession>(
            new FakeGitServices().Services,
            _host,
            pump,
            new TabSessionFactory(),
            new EffectRunnerOptions<TabSession>
            {
                Statuses = new StatusReporter(_host),
                Watcher = TestGit.QuietWatcherOptions,
                Keymap = table,
            });
    }

    [Fact]
    public void GivenARemappedHistoryKey_WhenAStatusIsPosted_ThenTheMessageNamesTheNewKey()
    {
        _runner.Run(new PostStatusEffect(UndoNotes.FromHistory, StatusSeverity.Info));

        var message = _host.State.Shell.Status.Current(_host.Clock.GetUtcNow());
        Assert.Equal(Expected, message!.Text);
    }

    [Fact]
    public void GivenARemappedHistoryKey_WhenAConfirmIsShown_ThenTheDialogNamesTheNewKey()
    {
        var prompt = new ConfirmPrompt("Title", [UndoNotes.FromHistory], UndoNotes.FromHistory, "Yes");

        _runner.Run(new ShowConfirmEffect(prompt, new StatusDismissedCommand(DateTimeOffset.UnixEpoch), "too small"));

        var dialog = _host.State.Shell.Confirm!.Prompt;
        Assert.Equal(Expected, dialog.Consequence);
        Assert.Equal(Expected, Assert.Single(dialog.Lines));
    }

    [Fact]
    public void GivenARemappedContinueKey_WhenAnOperationRefusalIsPosted_ThenTheMessageNamesTheNewKeys()
    {
        _runner.Run(new PostStatusEffect(OperationKeys.InProgress("merge"), StatusSeverity.Warning));

        var message = _host.State.Shell.Status.Current(_host.Clock.GetUtcNow());
        Assert.Equal(
            "A merge is in progress: finish or abort the merge first: continue (<space>zc) or abort (<space>za)",
            message!.Text);
    }

    [Fact]
    public void GivenARemappedContinueKey_WhenARebaseIsConfirmed_ThenTheDialogNamesTheNewKeys()
    {
        var prompt = new ConfirmPrompt("Rebase", ["Lines"], RebaseTexts.Confirm(Target, null, 0, null).Consequence, "Rebase");

        _runner.Run(new ShowConfirmEffect(prompt, new StatusDismissedCommand(DateTimeOffset.UnixEpoch), "too small"));

        Assert.Contains(
            "you can continue (<space>zc) or abort (<space>za) it",
            _host.State.Shell.Confirm!.Prompt.Consequence,
            StringComparison.Ordinal);
    }

    public void Dispose() => _runner.Dispose();
}
