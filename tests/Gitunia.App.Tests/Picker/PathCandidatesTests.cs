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

    // The expected names are joined with '|'.
    [Theory]
    [InlineData("~/code/alp", "alpha|alpine")] // Only the matching directories, sorted.
    [InlineData("~/code/", "alpha|alpine|beta|Zed")] // All visible directories, no hidden ones.
    [InlineData("~/code/.", ".hidden")] // A dot prefix lists the hidden ones.
    [InlineData("~/code/z", "Zed")] // A lowercase prefix ignores case.
    [InlineData("~/code/ALP", "alpha|alpine")] // An uppercase prefix still matches lowercase directories.
    public void GivenATypedPath_WhenListingCandidates_ThenTheMatchingDirectoriesAreReturnedSorted(
        string typed,
        string expectedNames)
    {
        var candidates = Candidates(typed, Context());

        Assert.Equal(expectedNames, string.Join('|', candidates.Select(candidate => candidate.Name)));
    }

    [Theory]
    [InlineData("~/code/ze", 0, "~/code/Zed/")] // The real case of the directory is used.
    [InlineData("~/code/b", 0, "~/code/beta/")] // A single candidate gets its name and a separator.
    [InlineData("~/code/a", 0, "~/code/alp")] // Several candidates extend to the common prefix.
    [InlineData("~/code/alp", 1, "~/code/alpine/")] // Nothing left to extend: the highlighted candidate is taken.
    [InlineData("~/code/q", 0, "~/code/q")] // No candidate: the text is unchanged.
    public void GivenATypedPath_WhenCompleting_ThenTheTextIsExtendedOrTheHighlightedCandidateTaken(
        string typed,
        int highlighted,
        string expected)
    {
        var candidates = Candidates(typed, Context());

        Assert.Equal(expected, PathCompleter.Complete(typed, candidates, highlighted));
    }

    [Fact]
    public void GivenRepositoryDirectory_WhenListingCandidates_ThenItIsTaggedAsRepository()
    {
        var candidate = Candidates("~/code/alpha", Context()).Single();

        Assert.True(candidate.Tags.HasFlag(PickerTags.Repository));
        Assert.True(candidate.Tags.HasFlag(PickerTags.Directory));
    }

    [Fact]
    public void GivenNamesDifferingInCaseOnly_WhenCompleting_ThenNoMixedCaseNameIsInvented()
    {
        var context = new FakeDirectories(new Dictionary<string, string[]> { [PickerFiles.PathOf("code")] = ["Docs", "docs2"] });
        var candidates = Candidates("~/code/d", context);

        Assert.Equal("~/code/Docs/", PathCompleter.Complete("~/code/d", candidates, 0));
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
