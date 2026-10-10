using Gitunia.Git;
using Xunit;

namespace Gitunia.Git.Tests;

public class SmokeTests
{
    [Fact]
    public void Minimum_git_version_is_declared() => Assert.Equal("2.43", GitLayer.MinimumGitVersion);
}
