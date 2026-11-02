using Gitunia.App.Tabs;
using Gitunia.Core.Workspaces.Tabs;
using Xunit;

namespace Gitunia.App.Tests.Tabs;

[Trait("Os", "Independent")]
public sealed class DetailCursorMemoryTests
{
    [Theory]
    [InlineData("aaa", "bbb", BottomTab.Changes, BottomTab.Changes, 3, 4, 7, 20, 4)] // Another commit: the landing row.
    [InlineData("aaa", "aaa", BottomTab.Changes, BottomTab.Changes, 0, 5, 1, 20, 5)] // The files just arrived: first file.
    [InlineData("aaa", "aaa", BottomTab.Changes, BottomTab.Changes, 3, 5, 8, 20, 8)] // Files already listed: it stays.
    [InlineData("aaa", "aaa", BottomTab.Details, BottomTab.Changes, 3, 4, 2, 20, 4)] // A tab never visited: landing row.
    [InlineData("aaa", "aaa", BottomTab.Changes, BottomTab.Changes, 3, 0, 30, 10, 9)] // Past the end: the last row.
    [InlineData("aaa", "", BottomTab.Changes, BottomTab.Changes, 3, 0, 0, 0, null)] // No rows: no cursor.
    public void GivenARequest_WhenDeciding_ThenTheCursorLandsWhereTheRulesSay(
        string previousHash,
        string hash,
        BottomTab previousTab,
        BottomTab tab,
        int previousFileCount,
        int landing,
        int currentRow,
        int rowCount,
        int? expected)
    {
        var memory = new DetailCursorMemory();

        var row = memory.Decide(
            Request(previousHash, hash, previousTab, tab, previousFileCount, landing, currentRow, rowCount));

        Assert.Equal(expected, row);
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
    public void GivenAnotherCommit_WhenDeciding_ThenTheRememberedRowsAreForgotten()
    {
        var memory = new DetailCursorMemory();
        memory.Decide(Request(previousTab: BottomTab.Changes, tab: BottomTab.Details, currentRow: 9, landing: 0));
        memory.Decide(Request(previousHash: "aaa", hash: "bbb", previousTab: BottomTab.Details, tab: BottomTab.Details, currentRow: 3, landing: 0));

        var row = memory.Decide(Request(previousHash: "bbb", hash: "bbb", previousTab: BottomTab.Details, tab: BottomTab.Changes, currentRow: 1, landing: 4));

        Assert.Equal(4, row);
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
