using Gitunia.App.Loading.Diff;
using Gitunia.Core.Diff;
using Xunit;

namespace Gitunia.App.Tests.Loading.Diff;

[Trait("Os", "Independent")]
public class DiffCacheTests
{
    [Fact]
    public void GivenFullCache_WhenAddingOne_ThenTheLeastRecentlyUsedIsDropped()
    {
        var cache = new DiffCache(capacity: 2);
        cache.Add(CommitFactory.Id(1), Document(1));
        cache.Add(CommitFactory.Id(2), Document(1));

        cache.Add(CommitFactory.Id(3), Document(1));

        Assert.Equal(2, cache.Count);
        Assert.False(cache.Contains(CommitFactory.Id(1)));
        Assert.True(cache.Contains(CommitFactory.Id(3)));
    }

    [Fact]
    public void GivenEntryRead_WhenAddingBeyondCapacity_ThenTheReadEntrySurvives()
    {
        var cache = new DiffCache(capacity: 2);
        cache.Add(CommitFactory.Id(1), Document(1));
        cache.Add(CommitFactory.Id(2), Document(1));
        Assert.True(cache.TryGet(CommitFactory.Id(1), out _));

        cache.Add(CommitFactory.Id(3), Document(1));

        Assert.True(cache.Contains(CommitFactory.Id(1)));
        Assert.False(cache.Contains(CommitFactory.Id(2)));
    }

    [Fact]
    public void GivenRowBudget_WhenEntriesExceedIt_ThenOldEntriesAreEvicted()
    {
        var cache = new DiffCache(rowBudget: 10);
        cache.Add(CommitFactory.Id(1), Document(6));
        cache.Add(CommitFactory.Id(2), Document(6));

        Assert.False(cache.Contains(CommitFactory.Id(1)));
        Assert.Equal(6, cache.RowCount);
    }

    [Fact]
    public void GivenADiffOverTheRowBudgetAlone_WhenAdding_ThenItIsNotKept()
    {
        var cache = new DiffCache(rowBudget: 10);

        cache.Add(CommitFactory.Id(1), Document(11));

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.RowCount);
    }

    [Fact]
    public void GivenAnEntry_WhenAddingItAgain_ThenRowsAreNotCountedTwice()
    {
        var cache = new DiffCache();
        cache.Add(CommitFactory.Id(1), Document(5));

        cache.Add(CommitFactory.Id(1), Document(7));

        Assert.Equal(1, cache.Count);
        Assert.Equal(7, cache.RowCount);
    }

    private static DiffDocument Document(int rows) =>
        new(Enumerable.Range(0, rows).Select(index => new DiffRow(DiffRowKind.Note, $"row {index}", null, null, -1)).ToArray(), [], [], [], 0);
}
