using Gitunia.Core.Text;
using Xunit;

namespace Gitunia.Core.Tests.Text;

[Trait("Os", "Independent")]
public class UiTextsTests
{
    [Fact]
    public void GivenASubject_WhenBuildingTheTexts_ThenEachIsSentenceCaseWithoutAFullStop()
    {
        Assert.Equal("No matching branch", UiTexts.NoMatch("branch"));
        Assert.Equal("No match for fix", UiTexts.NoMatchFor("fix"));
        Assert.Equal("Nothing to copy", UiTexts.Nothing("copy"));
        Assert.Equal("nothing to copy", UiTexts.NothingTo("copy"));
        Assert.Equal("nothing was deleted", UiTexts.NothingWas("deleted"));
        Assert.Equal("The name unchanged: nothing was renamed", UiTexts.Unchanged("The name", "renamed"));
        Assert.Equal("The name unchanged", UiTexts.Unchanged("The name"));
        Assert.Equal("No name", UiTexts.Empty("name"));
        Assert.Equal("Loading...", UiTexts.Loading);
        Assert.Equal("No search yet: press / to start one", UiTexts.NoSearch);
    }

    [Fact]
    public void GivenCounts_WhenPluralising_ThenOnlyOneIsSingular()
    {
        Assert.Equal("1 file", CountText.Plural(1, "file"));
        Assert.Equal("0 files", CountText.Plural(0, "file"));
        Assert.Equal("3 directories", CountText.Plural(3, "directory", "directories"));
        Assert.Equal("1 directory", CountText.Plural(1, "directory", "directories"));
    }
}
