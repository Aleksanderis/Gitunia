using Gitunia.Git.Commits;
using Xunit;

namespace Gitunia.Git.Tests.Commits;

public class CommitLogFormatTests
{
    [Fact]
    public void GivenTheFormat_WhenCountingSeparators_ThenFieldCountMatches()
    {
        var separators = CommitLogFormat.Format.Split(GitSeparators.UnitSeparatorPlaceholder).Length - 1;

        Assert.Equal(CommitLogFormat.FieldCount, separators + 1);
    }
}
