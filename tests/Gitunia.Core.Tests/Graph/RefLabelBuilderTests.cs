using Gitunia.Core.Graph;
using Gitunia.Git;
using Xunit;

namespace Gitunia.Core.Tests.Graph;

public class RefLabelBuilderTests
{
    [Fact]
    public void GivenLocalAndRemoteOfSameNameOnCommit_WhenBuilding_ThenOneLabelWithRemoteMarker()
    {
        var labels = RefLabelBuilder.Build([Local("master", true), Remote("origin/master"), Remote("origin/HEAD")]);

        var label = Assert.Single(labels);
        Assert.Equal(new RefLabel("master", RefKind.LocalBranch, true, true), label);
    }

    [Fact]
    public void GivenRemoteWithoutLocalOnCommit_WhenBuilding_ThenRemoteStaysSeparate()
    {
        var labels = RefLabelBuilder.Build([Local("topic", false), Remote("origin/master")]);

        Assert.Equal(["topic", "origin/master"], labels.Select(label => label.Name));
        Assert.All(labels, label => Assert.False(label.HasRemote));
    }

    [Fact]
    public void GivenRemoteHeadOnly_WhenBuilding_ThenNothingIsShown()
    {
        Assert.Empty(RefLabelBuilder.Build([Remote("origin/HEAD")]));
    }

    [Fact]
    public void GivenDetachedHeadAndTag_WhenBuilding_ThenKeptInOrder()
    {
        var labels = RefLabelBuilder.Build(
        [
            new CommitDecoration("HEAD", "HEAD", RefKind.Head, true),
            new CommitDecoration("refs/tags/v1", "v1", RefKind.Tag, false),
        ]);

        Assert.Equal([RefKind.Head, RefKind.Tag], labels.Select(label => label.Kind));
    }

    [Fact]
    public void GivenLocalNameThatOnlyEndsLikeRemote_WhenBuilding_ThenNotMerged()
    {
        var labels = RefLabelBuilder.Build([Local("ster", false), Remote("origin/master")]);

        Assert.Equal(2, labels.Count);
    }

    private static CommitDecoration Local(string name, bool isCurrent) =>
        new("refs/heads/" + name, name, RefKind.LocalBranch, isCurrent);

    private static CommitDecoration Remote(string name) =>
        new("refs/remotes/" + name, name, RefKind.RemoteBranch, false);
}
