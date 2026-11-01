using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Status;
using Gitunia.Tui.App;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>Builds <see cref="CommitActions"/> over fakes, for tests whose subject is another part of the Changes view.</summary>
public static class TestCommitActions
{
    public static CommitActions Create(WorkspaceHost host, StatusChannel statuses) => new(
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
        (_, _, _) => { },
        () => KeyMode.Normal,
        _ => { },
        (_, _) => { },
        () => { });
}
