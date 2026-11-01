using Gitunia.App.Picker;
using Gitunia.Core.Picker;
using Gitunia.App.Tests.Fakes;
using Xunit;

namespace Gitunia.App.Tests.Picker;

public class PathCandidatesTests
{
    private static FakeDirectories Context() => new(
        new Dictionary<string, string[]>
        {
            [PickerFiles.PathOf("code")] = ["alpha", "alpine", "beta", ".hidden", "Zed"],
            [PickerFiles.PathOf("code", "alpha")] = ["src"],
        },
        [PickerFiles.PathOf("code", "alpha")]);

    [Fact]
    public void GivenRealTreeWithMixedCase_WhenTypingLowercaseDirectoryPart_ThenRealDirectoriesAreListed()
    {
        var root = Path.Combine(Path.GetTempPath(), $"picker-case-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Projects", "MyRepo", "Alpha"));
            Directory.CreateDirectory(Path.Combine(root, "Projects", "MyRepo", "alpine"));
            var candidates = Candidates("~/projects/myrepo/al", DiskDirectories.Instance, root);

            Assert.Equal(["Alpha", "alpine"], candidates.Select(candidate => candidate.Name));
            // Real file system check: on macOS and Windows the directory lists as typed, the completer must correct it.
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
        var context = new FakeDirectories(new Dictionary<string, string[]>
        {
            [PickerFiles.Home] = ["Projects"],
            [PickerFiles.PathOf("Projects")] = ["Work"],
            [PickerFiles.PathOf("Projects", "Work")] = ["Xylo", "xray"],
        });

        var candidates = Candidates("~/projects/WORK/x", context);

        Assert.Equal(["xray", "Xylo"], candidates.Select(candidate => candidate.Name));
        Assert.All(candidates, candidate => Assert.StartsWith(PickerFiles.PathOf("Projects", "Work"), candidate.Path, StringComparison.Ordinal));
    }

    [Fact]
    public void GivenWrongCaseDirectoryPart_WhenCompleting_ThenTextUsesRealCaseSoEnterCanOpenIt()
    {
        var context = new FakeDirectories(new Dictionary<string, string[]>
        {
            [PickerFiles.Home] = ["Projects"],
            [PickerFiles.PathOf("Projects")] = ["Work"],
            [PickerFiles.PathOf("Projects", "Work")] = ["MyRepo"],
        });
        const string typed = "~/projects/work/my";

        var completed = PathCompleter.Complete(typed, Candidates(typed, context), 0);

        Assert.Equal("~/Projects/Work/MyRepo/", completed);
    }

    [Fact]
    public void GivenCaseInsensitiveFileSystem_WhenTypingWrongCaseDirectory_ThenRealCaseIsListed()
    {
        // A case-insensitive file system answers for the typed spelling and echoes it back.
        var candidates = Candidates("~/CODE/a", new IgnoringCaseDirectories());

        Assert.Equal(PickerFiles.PathOf("code", "alpha"), candidates[0].Path);
    }

    [Fact]
    public void GivenTwoDirectoriesDifferingInCase_WhenExactSpellingTyped_ThenExactOneIsListed()
    {
        var context = new FakeDirectories(new Dictionary<string, string[]>
        {
            [PickerFiles.Home] = ["Docs", "docs"],
            [PickerFiles.PathOf("docs")] = ["lower"],
            [PickerFiles.PathOf("Docs")] = ["upper"],
        });

        Assert.Equal(["lower"], Candidates("~/docs/", context).Select(candidate => candidate.Name));
    }

    [Fact]
    public void GivenCancelledToken_WhenListingCandidates_ThenListingStops()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => PathCandidates.List(
                "~/code/",
                PickerFiles.Home,
                PickerFiles.BaseDirectory,
                Context(),
                cancellation.Token));
    }

    [Fact]
    public void GivenTypedPrefix_WhenListingCandidates_ThenOnlyMatchingDirectoriesAreReturnedSorted()
    {
        var candidates = Candidates("~/code/alp", Context());

        Assert.Equal(["alpha", "alpine"], candidates.Select(candidate => candidate.Name));
    }

    [Fact]
    public void GivenTrailingSeparator_WhenListingCandidates_ThenAllVisibleDirectoriesAreReturnedAndHiddenOnesAreNot()
    {
        var candidates = Candidates("~/code/", Context());

        Assert.Equal(["alpha", "alpine", "beta", "Zed"], candidates.Select(candidate => candidate.Name));
    }

    [Fact]
    public void GivenDotPrefix_WhenListingCandidates_ThenHiddenDirectoriesAreIncluded()
    {
        var candidates = Candidates("~/code/.", Context());

        Assert.Equal([".hidden"], candidates.Select(candidate => candidate.Name));
    }

    [Fact]
    public void GivenRepositoryDirectory_WhenListingCandidates_ThenItIsTaggedAsRepository()
    {
        var candidate = Candidates("~/code/alpha", Context()).Single();

        Assert.True(candidate.Tags.HasFlag(PickerTags.Repository));
        Assert.True(candidate.Tags.HasFlag(PickerTags.Directory));
    }

    [Fact]
    public void GivenLowercasePrefix_WhenListingCandidates_ThenCaseIsIgnored()
    {
        var candidates = Candidates("~/code/z", Context());

        Assert.Equal(["Zed"], candidates.Select(candidate => candidate.Name));
    }

    [Fact]
    public void GivenUppercasePrefix_WhenListingCandidates_ThenLowercaseDirectoriesStillMatch()
    {
        var candidates = Candidates("~/code/ALP", Context());

        Assert.Equal(["alpha", "alpine"], candidates.Select(candidate => candidate.Name));
    }

    [Fact]
    public void GivenWrongCaseTyped_WhenCompleting_ThenTheRealCaseOfTheDirectoryIsUsed()
    {
        var candidates = Candidates("~/code/ze", Context());

        Assert.Equal("~/code/Zed/", PathCompleter.Complete("~/code/ze", candidates, 0));
    }

    [Fact]
    public void GivenNamesDifferingInCaseOnly_WhenCompleting_ThenNoMixedCaseNameIsInvented()
    {
        var context = new FakeDirectories(new Dictionary<string, string[]> { [PickerFiles.PathOf("code")] = ["Docs", "docs2"] });
        var candidates = Candidates("~/code/d", context);

        Assert.Equal("~/code/Docs/", PathCompleter.Complete("~/code/d", candidates, 0));
    }

    [Fact]
    public void GivenSingleCandidate_WhenCompleting_ThenNameAndSeparatorAreAdded()
    {
        var candidates = Candidates("~/code/b", Context());

        Assert.Equal("~/code/beta/", PathCompleter.Complete("~/code/b", candidates, 0));
    }

    [Fact]
    public void GivenSharedPrefix_WhenCompleting_ThenTextIsExtendedToTheCommonPrefix()
    {
        var candidates = Candidates("~/code/a", Context());

        Assert.Equal("~/code/alp", PathCompleter.Complete("~/code/a", candidates, 0));
    }

    [Fact]
    public void GivenNothingLeftToExtend_WhenCompleting_ThenTheHighlightedCandidateIsTaken()
    {
        var candidates = Candidates("~/code/alp", Context());

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
        var context = new FakeDirectories(new Dictionary<string, string[]> { [PickerFiles.Home] = ["code"] });

        Assert.Equal(["code"], Candidates("~", context).Select(candidate => candidate.Name));
    }

    private static IReadOnlyList<PickerRow> Candidates(string text, IDirectoryLister files, string? home = null) =>
        PathCandidates.List(text, home ?? PickerFiles.Home, PickerFiles.BaseDirectory, files, CancellationToken.None);

    // Models a case-insensitive file system: the home directory holds "code", and any spelling of it lists as typed.
    private sealed class IgnoringCaseDirectories : IDirectoryLister
    {
        public IReadOnlyList<string> ListDirectories(string directory)
        {
            var trimmed = Path.TrimEndingDirectorySeparator(directory);
            if (trimmed == Path.TrimEndingDirectorySeparator(PickerFiles.Home))
            {
                return [PickerFiles.PathOf("code")];
            }

            return string.Equals(trimmed, PickerFiles.PathOf("code"), StringComparison.OrdinalIgnoreCase)
                ? [Path.Combine(trimmed, "alpha")]
                : [];
        }

        public bool IsRepository(string directory) => false;

        public bool DirectoryExists(string directory) => false;
    }
}
