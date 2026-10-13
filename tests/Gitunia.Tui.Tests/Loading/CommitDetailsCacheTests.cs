using Gitunia.Git;
using Gitunia.Tui.Loading;
using Gitunia.Tui.Tests.Rendering;
using Xunit;

namespace Gitunia.Tui.Tests.Loading;

public class CommitDetailsCacheTests
{
    [Fact]
    public void GivenFullCache_WhenAddingOne_ThenTheLeastRecentlyUsedIsDropped()
    {
        var cache = new CommitDetailsCache(capacity: 3);
        cache.Add(Details(1));
        cache.Add(Details(2));
        cache.Add(Details(3));

        cache.Add(Details(4));

        Assert.Equal(3, cache.Count);
        Assert.False(cache.Contains(CommitFactory.Id(1)));
        Assert.True(cache.Contains(CommitFactory.Id(4)));
    }

    [Fact]
    public void GivenEntryRead_WhenAddingBeyondCapacity_ThenTheReadEntrySurvives()
    {
        var cache = new CommitDetailsCache(capacity: 3);
        cache.Add(Details(1));
        cache.Add(Details(2));
        cache.Add(Details(3));
        Assert.True(cache.TryGet(CommitFactory.Id(1), out _));

        cache.Add(Details(4));

        Assert.True(cache.Contains(CommitFactory.Id(1)));
        Assert.False(cache.Contains(CommitFactory.Id(2)));
    }

    [Fact]
    public void GivenContainsCalls_WhenAddingBeyondCapacity_ThenTheyDoNotChangeTheOrder()
    {
        var cache = new CommitDetailsCache(capacity: 2);
        cache.Add(Details(1));
        cache.Add(Details(2));
        Assert.True(cache.Contains(CommitFactory.Id(1)));

        cache.Add(Details(3));

        Assert.False(cache.Contains(CommitFactory.Id(1)));
    }

    [Fact]
    public void GivenSameCommitAddedTwice_WhenCounting_ThenItIsOneEntry()
    {
        var cache = new CommitDetailsCache(capacity: 3);

        cache.Add(Details(1, fileCount: 2));
        cache.Add(Details(1, fileCount: 2));

        Assert.Equal(1, cache.Count);
        Assert.Equal(2, cache.FileCount);
    }

    [Fact]
    public void GivenFileBudget_WhenEntriesExceedIt_ThenOldEntriesAreDroppedUntilItFits()
    {
        var cache = new CommitDetailsCache(capacity: 10, fileBudget: 10);
        cache.Add(Details(1, fileCount: 4));
        cache.Add(Details(2, fileCount: 4));

        cache.Add(Details(3, fileCount: 4));

        Assert.False(cache.Contains(CommitFactory.Id(1)));
        Assert.True(cache.Contains(CommitFactory.Id(2)));
        Assert.True(cache.Contains(CommitFactory.Id(3)));
        Assert.Equal(8, cache.FileCount);
    }

    [Fact]
    public void GivenCommitOverTheWholeFileBudget_WhenAdding_ThenItIsNotKeptAndNothingElseIsLost()
    {
        var cache = new CommitDetailsCache(capacity: 10, fileBudget: 10);
        cache.Add(Details(1, fileCount: 2));

        cache.Add(Details(2, fileCount: 11));

        Assert.False(cache.Contains(CommitFactory.Id(2)));
        Assert.True(cache.Contains(CommitFactory.Id(1)));
    }

    [Fact]
    public void GivenManyAdds_WhenCounting_ThenTheBoundsAlwaysHold()
    {
        var cache = new CommitDetailsCache(capacity: 5, fileBudget: 30);

        for (var number = 1; number <= 200; number++)
        {
            cache.Add(Details(number, fileCount: number % 9));
            Assert.True(cache.Count <= 5);
            Assert.True(cache.FileCount <= 30);
        }
    }

    [Fact]
    public void GivenFilledCache_WhenClearing_ThenItIsEmpty()
    {
        var cache = new CommitDetailsCache();
        cache.Add(Details(1, fileCount: 3));

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.FileCount);
    }

    private static CommitDetails Details(int number, int fileCount = 0) =>
        new(
            CommitFactory.Id(number),
            [],
            "Author",
            "author@example.com",
            DateTimeOffset.UnixEpoch,
            "Author",
            "author@example.com",
            DateTimeOffset.UnixEpoch,
            "commit " + number,
            Enumerable.Range(0, fileCount).Select(index => new CommitFileChange("file" + index, null, FileChangeKind.Modified, null)).ToArray());
}
