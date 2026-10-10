using Gitunia.Git;
using Xunit;

namespace Gitunia.Git.Tests;

public class SmokeTests
{
    [Fact]
    public void Given_git_layer_When_reading_minimum_version_Then_it_is_2_43() => Assert.Equal("2.43", GitLayer.MinimumGitVersion);
}
