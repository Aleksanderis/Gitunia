using Gitunia.App.Status;
using Gitunia.App.Tabs;
using Gitunia.App.Tests.Fakes;
using Gitunia.Core;
using Gitunia.Core.Shell;
using Xunit;

namespace Gitunia.App.Tests.Shell;

public sealed class ShellRolesTests
{
    [Fact]
    public void GivenSeparateRoleObjects_WhenQuitRequested_ThenOnlyTheSessionRoleQuits()
    {
        var session = new RecordingShell();
        var others = new RecordingShell();
        var roles = RolesOf(session, others);

        Run(new EffectRunnerOptions<TabSession> { ShellParts = roles }, new QuitRequested(false, false));

        Assert.Equal(1, session.Quits);
        Assert.Equal(0, others.Quits);
    }

    [Fact]
    public void GivenAnUnsentMessage_WhenQuitRequested_ThenTheSessionRoleFlushes()
    {
        var session = new RecordingShell();
        var others = new RecordingShell();
        var roles = RolesOf(session, others);

        Run(new EffectRunnerOptions<TabSession> { ShellParts = roles }, new QuitRequested(true, false));

        Assert.Equal(1, session.Flushed);
        Assert.Equal(0, others.Flushed);
    }

    [Fact]
    public void GivenBothOptions_WhenQuitRequested_ThenShellPartsWinOverShell()
    {
        var parts = new RecordingShell();
        var single = new RecordingShell();

        Run(
            new EffectRunnerOptions<TabSession> { ShellParts = ShellRoles.From(parts), Shell = single },
            new QuitRequested(false, false));

        Assert.Equal(1, parts.Quits);
        Assert.Equal(0, single.Quits);
    }

    [Fact]
    public void GivenOnlyShell_WhenQuitRequested_ThenItIsUsedForEveryRole()
    {
        var single = new RecordingShell();

        Run(new EffectRunnerOptions<TabSession> { Shell = single }, new QuitRequested(false, false));

        Assert.Equal(1, single.Quits);
    }

    private static ShellRoles RolesOf(RecordingShell session, RecordingShell others) =>
        new()
        {
            Copy = others,
            Cursor = others,
            Flow = others,
            Session = session,
            Stash = others,
            Tools = others,
            Route = others,
        };

    private static void Run(EffectRunnerOptions<TabSession> options, QuitRequested command)
    {
        var pump = new UiPump(TestContext.Current.CancellationToken);
        var host = new Dispatcher(AppState.Empty, pump);
        using var runner = new EffectRunner<TabSession>(
            TestGit.Services,
            host,
            pump,
            new TabSessionFactory(),
            options with { Statuses = new StatusReporter(host), Watcher = TestGit.QuietWatcherOptions });

        host.Dispatch(command);
    }
}
