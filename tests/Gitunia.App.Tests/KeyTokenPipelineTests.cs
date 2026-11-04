using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;
using Xunit;

namespace Gitunia.App.Tests;

// The runner resolves the key tokens of the texts against the running keymap before they are shown (#1196).
[Trait("Os", "Independent")]
public sealed class KeyTokenPipelineTests : IDisposable
{
    private const string Expected = "Afterwards the message history (<space>zz) shows how to bring it back.";

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

    public void Dispose() => _runner.Dispose();
}
