using Gitunia.Git.Watching;
using Gitunia.Git.Watching.Models;
using Xunit;

namespace Gitunia.Git.Tests.Watching;

public class InotifyLimitTests
{
    [Theory]
    [InlineData(1024, 2048, 256)]
    [InlineData(128, 2048, 32)]
    [InlineData(8, 2048, 16)]
    [InlineData(65536, 2048, 2048)]
    public void GivenAnInstanceLimit_WhenTheCapIsComputed_ThenItIsAQuarterWithinTheConfiguredMaximum(
        int instances,
        int configured,
        int expected)
    {
        Assert.Equal(expected, InotifyLimit.DirectoryCap(instances, configured));
    }

    [Fact]
    public void GivenAnUnknownLimit_WhenTheCapIsComputed_ThenTheConfiguredValueStands()
    {
        Assert.Equal(2048, InotifyLimit.DirectoryCap(null, 2048));
    }

    [Fact]
    public void GivenDefaultOptions_WhenRead_ThenNoFileWatchersAreUsed()
    {
        Assert.False(RepositoryWatcherOptions.Default.UseFileWatchers);
    }
}
