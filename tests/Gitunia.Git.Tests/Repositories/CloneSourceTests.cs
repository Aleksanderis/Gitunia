using Gitunia.Git.Repositories;
using Xunit;

namespace Gitunia.Git.Tests.Repositories;

public class CloneSourceTests
{
    private static readonly string Base = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "base"));
    private static readonly string Home = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "home"));

    [Theory]
    [InlineData("https://github.com/o/r.git")]
    [InlineData("ssh://git@host/o/r.git")]
    [InlineData("file:///srv/repo.git")]
    [InlineData("git@github.com:o/r.git")]
    [InlineData("host:path/r.git")]
    public void GivenAUrl_WhenResolving_ThenItIsKeptAsTyped(string url)
    {
        Assert.Equal(url, CloneSource.Resolve($"  {url} ", Base, Home));
    }

    [Fact]
    public void GivenARelativePath_WhenResolving_ThenItIsTakenFromTheBaseNotTheWorkingDirectory()
    {
        Assert.Equal(
            Path.Combine(Home, "x.git"),
            CloneSource.Resolve(Path.Combine("..", "home", "x.git"), Base, Home));
        Assert.Equal(
            Path.Combine(Base, "sub", "x.git"),
            CloneSource.Resolve(Path.Combine(".", "sub", "x.git"), Base, Home));
    }

    [Fact]
    public void GivenATildePath_WhenResolving_ThenItStartsAtHome()
    {
        Assert.Equal(Path.Combine(Home, "src", "x.git"), CloneSource.Resolve("~/src/x.git", Base, Home));
        Assert.Equal(Home, CloneSource.Resolve("~", Base, Home));
    }

    [Theory]
    [InlineData("https://github.com/o/app.git", "app")]
    [InlineData("https://github.com/o/app", "app")]
    [InlineData("https://github.com/o/app.git/", "app")]
    [InlineData("git@github.com:o/app.git", "app")]
    [InlineData("git@github.com:app.git", "app")]
    [InlineData("/srv/git/app.git", "app")]
    [InlineData("https://github.com/", "github.com")]
    [InlineData("", "repository")]
    [InlineData("..", "repository")]
    public void GivenASource_WhenNamingTheClone_ThenTheLastSegmentWithoutDotGitIsUsed(string source, string expected)
    {
        Assert.Equal(expected, CloneSource.RepositoryName(source));
    }
}
