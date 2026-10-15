using Gitunia.Git.ExternalTools;
using Xunit;

namespace Gitunia.Git.Tests.ExternalTools;

public class ExternalToolCommandsTests
{
    [Fact]
    public void GivenACommit_WhenBuildingDifftool_ThenItComparesWithTheFirstParentWithoutPrompting() =>
        Assert.Equal(
            ["-C", "/r", "difftool", "--no-prompt", "abc^1", "abc"],
            ExternalToolCommands.Build(ExternalToolKind.Difftool, "/r", "abc"));

    [Fact]
    public void GivenNoCommit_WhenBuildingDifftool_ThenItThrows() =>
        Assert.Throws<ArgumentException>(() => ExternalToolCommands.Build(ExternalToolKind.Difftool, "/r"));

    [Fact]
    public void WhenBuildingMergetool_ThenItIsPlainMergetool() =>
        Assert.Equal(["-C", "/r", "mergetool"], ExternalToolCommands.Build(ExternalToolKind.Mergetool, "/r"));

    [Fact]
    public void WhenBuildingCommitWithEditor_ThenGitCommitIsUsedSoGitPicksTheEditor() =>
        Assert.Equal(["-C", "/r", "commit"], ExternalToolCommands.Build(ExternalToolKind.CommitWithEditor, "/r"));
}
