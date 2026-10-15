using Gitunia.Git.Diff.Models;
using Gitunia.Tui.Rendering.Diff;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Diff;

public class DiffOptionsLabelTests
{
    [Fact]
    public void GivenDefaultOptions_WhenLabelled_ThenNothingIsShown()
    {
        Assert.Equal(string.Empty, DiffOptionsLabel.Of(DiffOptions.Default));
    }

    [Fact]
    public void GivenCustomOptions_WhenLabelled_ThenEachDifferenceIsMarked()
    {
        Assert.Equal("[ctx 6] [-w]", DiffOptionsLabel.Of(new DiffOptions(6, false, true)));
        Assert.Equal("[file]", DiffOptionsLabel.Of(DiffOptions.Default with { WholeFile = true }));
    }

    [Fact]
    public void GivenContextSteps_WhenMovingBelowZeroOrPastTheMaximum_ThenTheyStayInRange()
    {
        Assert.Equal(0, DiffOptions.Default.LessContext().LessContext().ContextLines);
        Assert.Equal(DiffOptions.MaximumContextLines, new DiffOptions(DiffOptions.MaximumContextLines).MoreContext().ContextLines);
    }
}
