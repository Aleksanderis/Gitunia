using Gitunia.Core.Picker;
using Gitunia.Core.Workspaces;
using Xunit;

namespace Gitunia.Core.Tests.Picker;

[Trait("Os", "Independent")]
public class PickerRowBuilderTests
{
    [Fact]
    public void GivenEmptyQuery_WhenBuilding_ThenOpenPinnedRecentAndDiscoveredComeInThatOrderWithoutDuplicates()
    {
        var workspace = PickerFixtures.Open(Workspace.Empty, "recent-one", "open-one");
        // "open-one" was opened last, so it is the most recent; close "recent-one" so it stays only in recents.
        workspace = WorkspaceReducer.Reduce(workspace, new CloseTabCommand(workspace.Tabs[0].Id)).Workspace;
        workspace = WorkspaceReducer.Reduce(workspace, new TogglePinCommand(PickerFixtures.PathOf("code", "pinned-one"))).Workspace;
        var context = PickerFixtures.Context(
            workspace,
            discovered: [PickerFixtures.PathOf("code", "found-one"), PickerFixtures.PathOf("code", "open-one")]);

        var rows = PickerRowBuilder.Build(string.Empty, context);

        Assert.Equal(["open-one", "pinned-one", "recent-one", "found-one"], rows.Select(row => row.Name));
        Assert.Equal(PickerTags.Open | PickerTags.Recent | PickerTags.Discovered, rows[0].Tags);
        Assert.Equal(PickerTags.Pinned, rows[1].Tags);
    }

    [Fact]
    public void GivenQuery_WhenBuilding_ThenOnlyMatchesRemainBestFirst()
    {
        var context = PickerFixtures.Context(discovered:
        [
            PickerFixtures.PathOf("code", "alphabet"),
            PickerFixtures.PathOf("code", "alpha"),
            PickerFixtures.PathOf("code", "beta"),
        ]);

        var rows = PickerRowBuilder.Build("alpha", context);

        Assert.Equal(["alpha", "alphabet"], rows.Select(row => row.Name));
    }

    [Fact]
    public void GivenQueryMatchingOnlyTheParentDirectory_WhenBuilding_ThenThePathStillMatches()
    {
        var context = PickerFixtures.Context(discovered: [PickerFixtures.PathOf("client-work", "site"), PickerFixtures.PathOf("code", "tool")]);

        var rows = PickerRowBuilder.Build("client", context);

        Assert.Equal(["site"], rows.Select(row => row.Name));
    }
}
