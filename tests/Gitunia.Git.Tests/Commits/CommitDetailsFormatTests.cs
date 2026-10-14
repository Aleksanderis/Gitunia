using Gitunia.Git.Commits;
using Xunit;

namespace Gitunia.Git.Tests.Commits;

public class CommitDetailsFormatTests
{
    [Fact]
    public void GivenTheFormat_WhenCountingSeparators_ThenFieldCountMatches()
    {
        var separators = CommitDetailsFormat.Format.Split(GitSeparators.UnitSeparatorPlaceholder).Length - 1;

        Assert.Equal(CommitDetailsFormat.FieldCount, separators + 1);
    }
}
