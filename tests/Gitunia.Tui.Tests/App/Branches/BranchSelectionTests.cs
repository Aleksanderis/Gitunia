using Gitunia.Core.Sidebar;
using Gitunia.Tui.App.Branches;
using Xunit;

namespace Gitunia.Tui.Tests.App.Branches;

[Trait("Os", "Independent")]
public sealed class BranchSelectionTests
{
    [Theory]
    [InlineData(SidebarRowKind.Folder, SidebarSection.Remotes)]
    [InlineData(SidebarRowKind.Folder, SidebarSection.Tags)]
    [InlineData(SidebarRowKind.Section, SidebarSection.Remotes)]
    [InlineData(SidebarRowKind.Section, SidebarSection.Tags)]
    public void GivenAContainerRowOutsideBranches_WhenSelecting_ThenOnlyTheGraphCommitIsCarried(
        SidebarRowKind kind,
        SidebarSection section)
    {
        var selection = BranchSelection.Of(Row(kind, section), () => "graphhash");

        Assert.Equal(new BranchSelection(SidebarRowKind.Section, null, "graphhash"), selection);
    }

    [Fact]
    public void GivenNoSidebarRow_WhenSelecting_ThenTheGraphCommitIsUsed() =>
        Assert.Equal(new BranchSelection(null, null, "graphhash"), BranchSelection.Of(null, () => "graphhash"));

    [Fact]
    public void GivenALocalBranchRow_WhenSelecting_ThenTheRowIsUsed() =>
        Assert.Equal(
            new BranchSelection(SidebarRowKind.LocalBranch, "topic", "abc"),
            BranchSelection.Of(Row(SidebarRowKind.LocalBranch, SidebarSection.Branches, "topic"), () => "graphhash"));

    private static SidebarRow Row(SidebarRowKind kind, SidebarSection section, string label = "x") =>
        new(kind, section, label, string.Empty, "abc", false, false, 0, null, null);
}
