using Gitunia.App.Status;
using Gitunia.App;
using Gitunia.App.Tabs;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Builds <see cref="CommitActions"/> over fakes, for tests whose subject is another part of the Changes view.</summary>
public static class TestCommitActions
{
    public static CommitActions Create(Dispatcher host, StatusReporter statuses) => new(
        host,
        new FakeCommitWriter(),
        new FakeOperationDetector(),
        statuses,
        action => action(),
        (_, done) =>
        {
            done();
            return true;
        },
        _ => CancellationToken.None,
        new TabOperationGate(),
        () => KeyMode.Normal,
        (_, _) => { },
        () => { });
}
