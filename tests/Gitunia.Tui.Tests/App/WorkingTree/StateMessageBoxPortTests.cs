using Gitunia.App;
using Gitunia.Core;
using Gitunia.Tui.App.WorkingTree;
using Xunit;

namespace Gitunia.Tui.Tests.App.WorkingTree;

public class StateMessageBoxPortTests
{
    [Fact]
    public void GivenNoTab_WhenReadingThePort_ThenThereIsNoDraftCursorOrCommit()
    {
        var port = new StateMessageBoxPort(new Dispatcher(AppState.Empty));

        Assert.Null(port.Draft);
        Assert.False(port.ShowsCursor);
        Assert.False(port.IsCommitting);
    }
}
