using Gitunia.Core.Graph;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Xunit;

namespace Gitunia.Core.Tests.Graph;

[Trait("Os", "Independent")]
public class UpstreamGoneLabelTests
{
    [Fact]
    public void GivenGoneLocalBranch_WhenBuildingLabels_ThenOnlyThatLocalBranchIsFlagged()
    {
        CommitDecoration[] decorations =
        [
            new("refs/heads/old", "old", RefKind.LocalBranch, false),
            new("refs/heads/live", "live", RefKind.LocalBranch, false),
            new("refs/tags/old", "old", RefKind.Tag, false),
        ];

        var labels = RefLabelBuilder.Build(decorations, new HashSet<string> { "old" });

        Assert.Equal([true, false, false], labels.Select(label => label.IsUpstreamGone));
    }

    [Fact]
    public void GivenNoGoneSet_WhenBuildingLabels_ThenNothingIsFlagged()
    {
        CommitDecoration[] decorations = [new("refs/heads/old", "old", RefKind.LocalBranch, false)];

        Assert.False(Assert.Single(RefLabelBuilder.Build(decorations)).IsUpstreamGone);
    }
}
