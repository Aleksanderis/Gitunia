using Gitunia.Core.Picker;
using Xunit;

namespace Gitunia.Core.Tests.Picker;

public class PathQueryTests
{
    [Fact]
    public void GivenPathPrefix_WhenChecking_ThenItIsAPath()
    {
        var failures = new List<string>();
        string[] rows =
        [
            "/usr/src",
            "~",
            "~/code",
            "./app",
            "../app",
            "..",
            "C:\\work",
            "c:/work",
        ];

        foreach (var text in rows)
        {
            try
            {
                CheckItIsAPath(text);
            }
            catch (Exception exception)
            {
                failures.Add($"{text}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckItIsAPath(string text)
    {
        Assert.True(PathQuery.IsPath(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("gitunia")]
    [InlineData("a/b")]
    [InlineData("C:")]
    [InlineData("~foo")]
    [InlineData(".hidden")]
    public void GivenPlainText_WhenChecking_ThenItIsAFuzzyQuery(string text)
    {
        Assert.False(PathQuery.IsPath(text));
    }

    [Fact]
    public void GivenTildePath_WhenResolving_ThenHomeIsExpanded()
    {
        var home = PickerFixtures.Home;

        Assert.Equal(Path.Combine(home, "code", "app"), PathQuery.Resolve("~/code/app", home, PickerFixtures.PathOf("ignored")));
        Assert.Equal(home, PathQuery.Resolve("~", home, PickerFixtures.PathOf("ignored")));
    }

    [Fact]
    public void GivenRelativePath_WhenResolving_ThenItIsTakenFromTheBaseDirectoryNotTheWorkingDirectory()
    {
        var baseDirectory = PickerFixtures.PathOf("code", "current");

        Assert.Equal(PickerFixtures.PathOf("code", "sibling"), PathQuery.Resolve("../sibling", PickerFixtures.Home, baseDirectory));
        Assert.Equal(PickerFixtures.PathOf("code", "current", "sub"), PathQuery.Resolve("./sub", PickerFixtures.Home, baseDirectory));
    }
}
