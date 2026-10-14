using Gitunia.Git.Commits;
using Gitunia.Git.Commits.Models;
using Xunit;

namespace Gitunia.Git.Tests.Commits;

public class FileChangeLettersTests
{
    [Theory]
    [InlineData('A', FileChangeKind.Added)]
    [InlineData('M', FileChangeKind.Modified)]
    [InlineData('D', FileChangeKind.Deleted)]
    [InlineData('R', FileChangeKind.Renamed)]
    [InlineData('C', FileChangeKind.Copied)]
    [InlineData('T', FileChangeKind.TypeChanged)]
    [InlineData('U', FileChangeKind.UpdatedButUnmerged)]
    public void GivenALetter_WhenMapping_ThenKindMatchesWithOrWithoutUnmodified(char letter, FileChangeKind expected)
    {
        Assert.Equal(expected, FileChangeLetters.KindOf(letter, allowUnmodified: false));
        Assert.Equal(expected, FileChangeLetters.KindOf(letter, allowUnmodified: true));
    }

    [Fact]
    public void GivenADot_WhenMapping_ThenOnlyStatusAcceptsIt()
    {
        Assert.Null(FileChangeLetters.KindOf(FileChangeLetters.Unmodified, allowUnmodified: false));
        Assert.Equal(FileChangeKind.Unmodified, FileChangeLetters.KindOf(FileChangeLetters.Unmodified, allowUnmodified: true));
    }
}
