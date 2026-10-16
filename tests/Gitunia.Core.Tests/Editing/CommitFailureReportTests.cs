using Gitunia.Core.Editing;
using Xunit;

namespace Gitunia.Core.Tests.Editing;

public class CommitFailureReportTests
{
    [Fact]
    public void GivenHookOutput_WhenBuildingTheLines_ThenTheOutputComesFirstThenABlankLineAndTheHint()
    {
        var lines = CommitFailureReport.Lines("lint: bad\r\n  indented line\n\n");

        Assert.Equal(["lint: bad", "  indented line", string.Empty, CommitFailureReport.Hint], lines);
    }

    [Fact]
    public void GivenNoOutput_WhenBuildingTheLines_ThenAPlaceholderStandsInForIt()
    {
        var lines = CommitFailureReport.Lines(string.Empty);

        Assert.Equal([CommitFailureReport.NoOutput, string.Empty, CommitFailureReport.Hint], lines);
    }

    [Fact]
    public void GivenSeveralLines_WhenBuildingTheNotice_ThenItIsTheFirstNonBlankLine()
    {
        Assert.Equal("first", CommitFailureReport.Notice("\n  first  \nsecond"));
        Assert.Equal(CommitFailureReport.NoOutput, CommitFailureReport.Notice(string.Empty));
    }

    [Fact]
    public void GivenAVeryLongLine_WhenBuildingTheNotice_ThenItIsCut()
    {
        var notice = CommitFailureReport.Notice(new string('x', 500));

        Assert.EndsWith("...", notice, StringComparison.Ordinal);
        Assert.True(notice.Length < 200);
    }

    [Fact]
    public void GivenTheHint_WhenReadingIt_ThenItNamesTheWayOut()
    {
        Assert.Contains("<space>te", CommitFailureReport.Hint, StringComparison.Ordinal);
        Assert.Contains("passphrase", CommitFailureReport.Hint, StringComparison.Ordinal);
    }
}
