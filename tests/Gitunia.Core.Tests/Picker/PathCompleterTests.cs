using Gitunia.Core.Picker;
using Xunit;

namespace Gitunia.Core.Tests.Picker;

public class PathCompleterTests
{
    private static PickerContext Context() => PickerFixtures.Context(
        directories: new Dictionary<string, string[]>
        {
            [PickerFixtures.PathOf("code")] = ["alpha", "alpine", "beta", ".hidden", "Zed"],
            [PickerFixtures.PathOf("code", "alpha")] = ["src"],
        },
        repositories: [PickerFixtures.PathOf("code", "alpha")]);

    [Fact]
    public void GivenTypedPrefix_WhenListingCandidates_ThenOnlyMatchingDirectoriesAreReturnedSorted()
    {
        var candidates = PathCompleter.Candidates("~/code/alp", Context());

        Assert.Equal(["alpha", "alpine"], candidates.Select(candidate => candidate.Name));
    }

    [Fact]
    public void GivenTrailingSeparator_WhenListingCandidates_ThenAllVisibleDirectoriesAreReturnedAndHiddenOnesAreNot()
    {
        var candidates = PathCompleter.Candidates("~/code/", Context());

        Assert.Equal(["alpha", "alpine", "beta", "Zed"], candidates.Select(candidate => candidate.Name));
    }

    [Fact]
    public void GivenDotPrefix_WhenListingCandidates_ThenHiddenDirectoriesAreIncluded()
    {
        var candidates = PathCompleter.Candidates("~/code/.", Context());

        Assert.Equal([".hidden"], candidates.Select(candidate => candidate.Name));
    }

    [Fact]
    public void GivenRepositoryDirectory_WhenListingCandidates_ThenItIsTaggedAsRepository()
    {
        var candidate = PathCompleter.Candidates("~/code/alpha", Context()).Single();

        Assert.True(candidate.Tags.HasFlag(PickerTags.Repository));
        Assert.True(candidate.Tags.HasFlag(PickerTags.Directory));
    }

    [Fact]
    public void GivenLowercasePrefix_WhenListingCandidates_ThenCaseIsIgnored()
    {
        var candidates = PathCompleter.Candidates("~/code/z", Context());

        Assert.Equal(["Zed"], candidates.Select(candidate => candidate.Name));
    }

    [Fact]
    public void GivenSingleCandidate_WhenCompleting_ThenNameAndSeparatorAreAdded()
    {
        var candidates = PathCompleter.Candidates("~/code/b", Context());

        Assert.Equal("~/code/beta/", PathCompleter.Complete("~/code/b", candidates, 0));
    }

    [Fact]
    public void GivenSharedPrefix_WhenCompleting_ThenTextIsExtendedToTheCommonPrefix()
    {
        var candidates = PathCompleter.Candidates("~/code/a", Context());

        Assert.Equal("~/code/alp", PathCompleter.Complete("~/code/a", candidates, 0));
    }

    [Fact]
    public void GivenNothingLeftToExtend_WhenCompleting_ThenTheHighlightedCandidateIsTaken()
    {
        var candidates = PathCompleter.Candidates("~/code/alp", Context());

        Assert.Equal("~/code/alpine/", PathCompleter.Complete("~/code/alp", candidates, 1));
    }

    [Fact]
    public void GivenNoCandidates_WhenCompleting_ThenTextIsUnchanged()
    {
        Assert.Equal("~/code/q", PathCompleter.Complete("~/code/q", [], 0));
    }

    [Fact]
    public void GivenBareTilde_WhenListingCandidates_ThenTheHomeDirectoryIsListed()
    {
        var context = PickerFixtures.Context(directories: new Dictionary<string, string[]> { [PickerFixtures.Home] = ["code"] });

        Assert.Equal(["code"], PathCompleter.Candidates("~", context).Select(candidate => candidate.Name));
    }
}
