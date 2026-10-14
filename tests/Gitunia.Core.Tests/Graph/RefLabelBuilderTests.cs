using Gitunia.Core.Graph;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
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

    [Fact]
    public void GivenNestedRemoteBranchWithOtherLocalName_WhenBuilding_ThenNotMergedNorHidden()
    {
        var labels = RefLabelBuilder.Build([Local("topic", false), Remote("origin/feature/topic"), Remote("origin/feature/HEAD")]);

        Assert.Equal(["topic", "origin/feature/topic", "origin/feature/HEAD"], labels.Select(label => label.Name));
        Assert.All(labels, label => Assert.False(label.HasRemote));
    }

    [Fact]
    public void GivenDetachedHeadWithRemoteOnSameCommit_WhenBuilding_ThenBothStay()
    {
        var labels = RefLabelBuilder.Build([new CommitDecoration("HEAD", "HEAD", RefKind.Head, true), Remote("origin/main")]);

        Assert.Equal(["HEAD", "origin/main"], labels.Select(label => label.Name));
    }

    [Fact]
    public void GivenCurrentBranchWithoutRemote_WhenBuilding_ThenNoRemoteMarker()
    {
        var label = Assert.Single(RefLabelBuilder.Build([Local("main", true)]));

        Assert.False(label.HasRemote);
    }

    private static CommitDecoration Local(string name, bool isCurrent) =>
        new("refs/heads/" + name, name, RefKind.LocalBranch, isCurrent);

    private static CommitDecoration Remote(string name) =>
        new("refs/remotes/" + name, name, RefKind.RemoteBranch, false);
}
