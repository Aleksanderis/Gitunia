using Gitunia.Git;
using Xunit;

namespace Gitunia.Git.Tests;

public class SmokeTests
{
    [Fact]
    public void GivenGitLayer_WhenReadingMinimumVersion_ThenItIs243() => Assert.Equal("2.43", GitLayer.MinimumGitVersion);
}
