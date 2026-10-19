using Gitunia.Core.Status;
using Gitunia.Git.Remotes.Models;
using Xunit;

namespace Gitunia.Core.Tests.Status;

public class RemoteProgressTextTests
{
    [Fact]
    public void GivenNoReportYet_WhenFormatting_ThenOnlyTheActivityShows()
    {
        Assert.Equal("Fetching...", RemoteProgressText.Format("Fetching", null));
    }

    [Fact]
    public void GivenAPercentReport_WhenFormatting_ThenStageAndPercentShow()
    {
        Assert.Equal(
            "Pushing: Writing objects 45%",
            RemoteProgressText.Format("Pushing", new RemoteProgress("Writing objects", 45)));
    }

    [Fact]
    public void GivenACountOnlyReport_WhenFormatting_ThenNoPercentShows()
    {
        Assert.Equal(
            "Pulling: Enumerating objects",
            RemoteProgressText.Format("Pulling", new RemoteProgress("Enumerating objects", null)));
    }
}
