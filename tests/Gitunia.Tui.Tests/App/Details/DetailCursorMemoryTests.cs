using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Details;
using Xunit;

namespace Gitunia.Tui.Tests.App.Details;

[Trait("Os", "Independent")]
public sealed class DetailCursorMemoryTests
{
    [Fact]
    public void GivenAnotherCommit_WhenDeciding_ThenTheCursorLandsOnTheLandingRow()
    {
        var memory = new DetailCursorMemory();

        var row = memory.Decide(Request(previousHash: "aaa", hash: "bbb", currentRow: 7, landing: 4));

        Assert.Equal(4, row);
    }

    [Fact]
    public void GivenTheSameCommitWhoseFilesJustArrived_WhenDeciding_ThenTheCursorMovesToTheFirstFile()
    {
        var memory = new DetailCursorMemory();

        var row = memory.Decide(Request(previousFileCount: 0, currentRow: 1, landing: 5));

        Assert.Equal(5, row);
    }

    [Fact]
    public void GivenTheSameCommitWithFilesAlreadyListed_WhenDeciding_ThenTheCursorStays()
    {
        var memory = new DetailCursorMemory();

        var row = memory.Decide(Request(previousFileCount: 3, currentRow: 8, landing: 5));

        Assert.Equal(8, row);
    }

    [Fact]
    public void GivenATabSwitchBack_WhenDeciding_ThenTheRowOfThatTabIsRestored()
    {
        var memory = new DetailCursorMemory();
        memory.Decide(Request(previousTab: BottomTab.Changes, tab: BottomTab.Details, currentRow: 9, landing: 0));

        var row = memory.Decide(Request(previousTab: BottomTab.Details, tab: BottomTab.Changes, currentRow: 2, landing: 4));

        Assert.Equal(9, row);
    }

    [Fact]
    public void GivenATabNeverVisited_WhenSwitchingToIt_ThenTheCursorLandsOnTheLandingRow()
    {
        var memory = new DetailCursorMemory();

        var row = memory.Decide(Request(previousTab: BottomTab.Details, tab: BottomTab.Changes, currentRow: 2, landing: 4));

        Assert.Equal(4, row);
    }

    [Fact]
    public void GivenAnotherCommit_WhenDeciding_ThenTheRememberedRowsAreForgotten()
    {
        var memory = new DetailCursorMemory();
        memory.Decide(Request(previousTab: BottomTab.Changes, tab: BottomTab.Details, currentRow: 9, landing: 0));
        memory.Decide(Request(previousHash: "aaa", hash: "bbb", previousTab: BottomTab.Details, tab: BottomTab.Details, currentRow: 3, landing: 0));

        var row = memory.Decide(Request(previousHash: "bbb", hash: "bbb", previousTab: BottomTab.Details, tab: BottomTab.Changes, currentRow: 1, landing: 4));

        Assert.Equal(4, row);
    }

    [Fact]
    public void GivenARowPastTheEnd_WhenDeciding_ThenTheCursorIsClampedToTheLastRow()
    {
        var memory = new DetailCursorMemory();

        var row = memory.Decide(Request(currentRow: 30, rowCount: 10));

        Assert.Equal(9, row);
    }

    [Fact]
    public void GivenNoRows_WhenDeciding_ThenThereIsNoCursor()
    {
        var memory = new DetailCursorMemory();

        var row = memory.Decide(Request(previousHash: "aaa", hash: string.Empty, rowCount: 0));

        Assert.Null(row);
    }

    private static DetailCursorRequest Request(
        string previousHash = "aaa",
        string hash = "aaa",
        BottomTab previousTab = BottomTab.Changes,
        BottomTab tab = BottomTab.Changes,
        int previousFileCount = 3,
        int landing = 0,
        int? currentRow = 0,
        int rowCount = 20) =>
        new(previousHash, hash, previousTab, tab, previousFileCount, landing, currentRow, rowCount);
}
