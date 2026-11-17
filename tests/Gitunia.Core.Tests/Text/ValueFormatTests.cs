using Gitunia.Core.Text;
using Gitunia.Git.Commits.Models;
using Xunit;

namespace Gitunia.Core.Tests.Text;

[Trait("Os", "Independent")]
public class ValueFormatTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "now")]
    [InlineData(59, "now")]
    [InlineData(60, "1m ago")]
    [InlineData(59 * 60, "59m ago")]
    [InlineData(60 * 60, "1h ago")]
    [InlineData(23 * 3600, "23h ago")]
    [InlineData(24 * 3600, "1d ago")]
    [InlineData(6 * 86400, "6d ago")]
    [InlineData(7 * 86400, "1w ago")]
    [InlineData(29 * 86400, "4w ago")]
    [InlineData(30 * 86400, "1mo ago")]
    [InlineData(364 * 86400, "12mo ago")]
    [InlineData(365 * 86400, "1y ago")]
    [InlineData(800 * 86400, "2y ago")]
    public void GivenAnAge_WhenFormatting_ThenTheLargestFittingUnitIsUsed(int seconds, string expected)
    {
        Assert.Equal(expected, ValueFormat.Relative(Now.AddSeconds(-seconds), Now));
    }

    [Fact]
    public void GivenATimeInTheFuture_WhenFormatting_ThenItReadsAsNow()
    {
        Assert.Equal("now", ValueFormat.Relative(Now.AddDays(2), Now));
    }

    [Fact]
    public void GivenATimeWithAZone_WhenFormattingTheDateForms_ThenTheWallClockOfItsOffsetIsShown()
    {
        var time = new DateTimeOffset(2026, 11, 17, 9, 5, 0, TimeSpan.FromHours(2));

        Assert.Equal("2026-11-17", ValueFormat.Date(time));
        Assert.Equal("2026-11-17 09:05", ValueFormat.DateTime(time));
        Assert.Equal("2026-11-17 09:05 +02:00", ValueFormat.DateTimeZone(time));
    }

    [Theory]
    [InlineData(FileChangeKind.Added, "A")]
    [InlineData(FileChangeKind.Deleted, "D")]
    [InlineData(FileChangeKind.Renamed, "R")]
    [InlineData(FileChangeKind.Copied, "C")]
    [InlineData(FileChangeKind.TypeChanged, "T")]
    [InlineData(FileChangeKind.UpdatedButUnmerged, "U")]
    [InlineData(FileChangeKind.Modified, "M")]
    [InlineData(FileChangeKind.Unmodified, "M")]
    public void GivenAChangeKind_WhenAskingForItsLetter_ThenItIsGitsLetter(FileChangeKind kind, string expected)
    {
        Assert.Equal(expected, ValueFormat.StatusLetter(kind));
    }

    [Fact]
    public void GivenARename_WhenFormattingIt_ThenTheOldPathComesFirst()
    {
        Assert.Equal("a.txt -> b.txt", ValueFormat.RenameText("a.txt", "b.txt"));
    }

    [Theory]
    [InlineData(2, 1, "+2-1")]
    [InlineData(2, 0, "+2")]
    [InlineData(null, 3, "-3")]
    [InlineData(0, 0, "")]
    [InlineData(null, null, "")]
    public void GivenAheadAndBehind_WhenFormatting_ThenZeroAndUnknownAreLeftOut(int? ahead, int? behind, string expected)
    {
        Assert.Equal(expected, ValueFormat.AheadBehind(ahead, behind, "+", "-"));
    }
}
