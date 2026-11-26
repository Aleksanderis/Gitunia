using Gitunia.Core.Ignore;
using Xunit;

namespace Gitunia.Core.Tests.Ignore;

// The patterns offered for a path: pure, so every line that reaches .gitignore is checked here.
[Trait("Os", "Independent")]
public class IgnorePatternsTests
{
    [Fact]
    public void GivenAFileInAFolder_WhenListingOptions_ThenThePathTheExtensionAndTheFolderAreOffered()
    {
        var options = IgnorePatterns.Options("src/app/debug.log", false);

        Assert.Equal(
            [
                (IgnorePatterns.PathKey, "/src/app/debug.log"),
                (IgnorePatterns.ExtensionKey, "*.log"),
                (IgnorePatterns.FolderKey, "/src/app/"),
            ],
            options.Select(option => (option.Key, option.Value)));
    }

    [Fact]
    public void GivenAFileAtTheRoot_WhenListingOptions_ThenThereIsNoFolderOption()
    {
        var options = IgnorePatterns.Options("debug.log", false);

        Assert.Equal([IgnorePatterns.PathKey, IgnorePatterns.ExtensionKey], options.Select(option => option.Key));
    }

    [Theory]
    [InlineData(".env")]
    [InlineData("Makefile")]
    [InlineData("name.")]
    public void GivenANameWithoutARealExtension_WhenListingOptions_ThenNoExtensionPatternIsOffered(string name)
    {
        var options = IgnorePatterns.Options(name, false);

        Assert.DoesNotContain(options, option => option.Key == IgnorePatterns.ExtensionKey);
    }

    [Fact]
    public void GivenAnUntrackedDirectory_WhenListingOptions_ThenThePatternIsAnchoredAndEndsWithASlash()
    {
        var options = IgnorePatterns.Options("build/obj/", true);

        Assert.Equal(["/build/obj/", "/build/"], options.Select(option => option.Value));
    }

    [Fact]
    public void GivenAFolderRowWithoutATrailingSlash_WhenListingOptions_ThenItIsWrittenAsAFolder()
    {
        var options = IgnorePatterns.Options("build", true);

        Assert.Equal("/build/", Assert.Single(options).Value);
    }

    [Fact]
    public void GivenGlobCharactersAndATrailingSpace_WhenListingOptions_ThenTheyAreEscapedToMatchLiterally()
    {
        var options = IgnorePatterns.Options("a[1]*?\\b.t*t ", false);

        Assert.Equal("/a\\[1]\\*\\?\\\\b.t\\*t\\ ", options[0].Value);
    }

    [Theory]
    [InlineData("a\nb", true)]
    [InlineData("tab\there", true)]
    [InlineData("plain name.txt", false)]
    public void GivenAPath_WhenAskingIfItCanBeWritten_ThenControlCharactersAreRefused(string path, bool unwritable)
    {
        Assert.Equal(unwritable, IgnorePatterns.IsUnwritable(path));
    }
}
