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
    public void GivenRealTreeWithMixedCase_WhenTypingLowercaseDirectoryPart_ThenRealDirectoriesAreListed()
    {
        var root = Path.Combine(Path.GetTempPath(), $"picker-case-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Projects", "MyRepo", "Alpha"));
            Directory.CreateDirectory(Path.Combine(root, "Projects", "MyRepo", "alpine"));
            var context = PickerFixtures.Context() with
            {
                HomeDirectory = root,
                ListDirectories = directory => Directory.Exists(directory) ? [.. Directory.EnumerateDirectories(directory)] : [],
            };

            var candidates = PathCompleter.Candidates("~/projects/myrepo/al", context);

            Assert.Equal(["Alpha", "alpine"], candidates.Select(candidate => candidate.Name));
            Assert.Equal(Path.Combine(root, "Projects", "MyRepo", "Alpha"), candidates[0].Path);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GivenWrongCaseDirectoryPart_WhenListingCandidates_ThenRealDirectoryIsListed()
    {
        var context = PickerFixtures.Context(directories: new Dictionary<string, string[]>
        {
            [PickerFixtures.Home] = ["Projects"],
            [PickerFixtures.PathOf("Projects")] = ["Work"],
            [PickerFixtures.PathOf("Projects", "Work")] = ["Xylo", "xray"],
        });

        var candidates = PathCompleter.Candidates("~/projects/WORK/x", context);

        Assert.Equal(["xray", "Xylo"], candidates.Select(candidate => candidate.Name));
        Assert.All(candidates, candidate => Assert.StartsWith(PickerFixtures.PathOf("Projects", "Work"), candidate.Path, StringComparison.Ordinal));
    }

    [Fact]
    public void GivenWrongCaseDirectoryPart_WhenCompleting_ThenTextUsesRealCaseSoEnterCanOpenIt()
    {
        var context = PickerFixtures.Context(directories: new Dictionary<string, string[]>
        {
            [PickerFixtures.Home] = ["Projects"],
            [PickerFixtures.PathOf("Projects")] = ["Work"],
            [PickerFixtures.PathOf("Projects", "Work")] = ["MyRepo"],
        });
        const string typed = "~/projects/work/my";

        var completed = PathCompleter.Complete(typed, PathCompleter.Candidates(typed, context), 0);

        Assert.Equal("~/Projects/Work/MyRepo/", completed);
    }

    [Fact]
    public void GivenCaseInsensitiveFileSystem_WhenTypingWrongCaseDirectory_ThenRealCaseIsListed()
    {
        var context = PickerFixtures.Context() with
        {
            // A case-insensitive file system answers for the typed spelling and echoes it back.
            ListDirectories = directory => ListIgnoringCase(directory, PickerFixtures.PathOf("code")),
        };

        var candidates = PathCompleter.Candidates("~/CODE/a", context);

        Assert.Equal(Path.Combine(PickerFixtures.PathOf("code"), "alpha"), candidates[0].Path, ignoreCase: true);
    }

    [Fact]
    public void GivenTwoDirectoriesDifferingInCase_WhenExactSpellingTyped_ThenExactOneIsListed()
    {
        var context = PickerFixtures.Context(directories: new Dictionary<string, string[]>
        {
            [PickerFixtures.Home] = ["Docs", "docs"],
            [PickerFixtures.PathOf("docs")] = ["lower"],
            [PickerFixtures.PathOf("Docs")] = ["upper"],
        });

        Assert.Equal(["lower"], PathCompleter.Candidates("~/docs/", context).Select(candidate => candidate.Name));
    }

    [Fact]
    public void GivenCancelledToken_WhenListingCandidates_ThenListingStops()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => PathCompleter.ListCandidates("~/code/", Context(), cancellation.Token));
    }

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
    public void GivenUppercasePrefix_WhenListingCandidates_ThenLowercaseDirectoriesStillMatch()
    {
        var candidates = PathCompleter.Candidates("~/code/ALP", Context());

        Assert.Equal(["alpha", "alpine"], candidates.Select(candidate => candidate.Name));
    }

    [Fact]
    public void GivenWrongCaseTyped_WhenCompleting_ThenTheRealCaseOfTheDirectoryIsUsed()
    {
        var candidates = PathCompleter.Candidates("~/code/ze", Context());

        Assert.Equal("~/code/Zed/", PathCompleter.Complete("~/code/ze", candidates, 0));
    }

    [Fact]
    public void GivenNamesDifferingInCaseOnly_WhenCompleting_ThenNoMixedCaseNameIsInvented()
    {
        var context = PickerFixtures.Context(directories: new Dictionary<string, string[]> { [PickerFixtures.PathOf("code")] = ["Docs", "docs2"] });
        var candidates = PathCompleter.Candidates("~/code/d", context);

        Assert.Equal("~/code/Docs/", PathCompleter.Complete("~/code/d", candidates, 0));
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

    private static string[] ListIgnoringCase(string directory, string existing) =>
        string.Equals(Path.TrimEndingDirectorySeparator(directory), existing, StringComparison.OrdinalIgnoreCase)
            ? [Path.Combine(directory, "alpha")]
            : [];
}
