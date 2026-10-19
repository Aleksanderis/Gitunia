using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Xunit;

namespace Gitunia.Core.Tests.Config;

public class ConfigKeyLocatorTests
{
    private const string Text = """
        {
          // Comment with "keys" and { braces.
          "keys": [
            { "key": "a", "action": "list.down" },
            { "key": "é", "action": "list.up", },
          ],
          "other": 12
        }
        """;

    [Fact]
    public void GivenAPropertyPath_WhenFinding_ThenTheSpanCoversTheQuotedValue()
    {
        var span = ConfigKeyLocator.Find(Text, ["keys", "1", "action"]);

        Assert.NotNull(span);
        Assert.Equal("\"list.up\"", Text.Substring(span.Value.Start, span.Value.Length));
    }

    [Fact]
    public void GivenAPropertyWrittenInOtherCase_WhenFinding_ThenItIsFoundLikeTheLoaderReadsIt()
    {
        var span = ConfigKeyLocator.Find("{ \"Keys\": [ { \"Key\": \"a\" } ] }", ["keys", "0", "key"]);

        Assert.NotNull(span);
    }

    [Fact]
    public void GivenAnArrayPath_WhenFinding_ThenTheSpanCoversTheWholeObject()
    {
        var span = ConfigKeyLocator.Find(Text, ["keys", "0"]);

        Assert.Equal(
            "{ \"key\": \"a\", \"action\": \"list.down\" }",
            Text.Substring(span!.Value.Start, span.Value.Length));
    }

    [Fact]
    public void GivenANumberValue_WhenFinding_ThenTheSpanCoversTheDigitsOnly()
    {
        var span = ConfigKeyLocator.Find(Text, ["other"]);

        Assert.Equal("12", Text.Substring(span!.Value.Start, span.Value.Length));
    }

    [Fact]
    public void GivenReplacingTheSpan_WhenSaving_ThenCommentsAndFormattingAreKept()
    {
        var span = ConfigKeyLocator.Find(Text, ["keys", "0", "action"])!.Value;

        var edited = Text.Remove(span.Start, span.Length).Insert(span.Start, "\"list.top\"");

        Assert.Contains("// Comment with \"keys\" and { braces.", edited, StringComparison.Ordinal);
        Assert.Contains("{ \"key\": \"a\", \"action\": \"list.top\" },", edited, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("keys", "2")]
    [InlineData("keys", "x")]
    [InlineData("missing")]
    [InlineData("keys", "0", "nope")]
    [InlineData("other", "deeper")]
    public void GivenAPathThatDoesNotExist_WhenFinding_ThenTheResultIsNull(params string[] path)
    {
        Assert.Null(ConfigKeyLocator.Find(Text, path));
    }

    [Fact]
    public void GivenInvalidText_WhenFinding_ThenTheResultIsNull()
    {
        Assert.Null(ConfigKeyLocator.Find("{ \"keys\": [", ["keys", "0"]));
    }

    [Fact]
    public void GivenDuplicateKeys_WhenLocating_ThenTheLastIsFoundAndShadowingIsReported()
    {
        const string text = "{ \"a\": 1, \"a\": 22 }";

        var location = ConfigKeyLocator.Locate(text, ["a"]);

        Assert.Equal(ConfigLocationKind.Found, location.Kind);
        Assert.Equal("22", text.Substring(location.Span.Start, location.Span.Length));
        Assert.True(location.Shadowed);
    }

    [Fact]
    public void GivenAMissingKey_WhenLocating_ThenItIsAbsentWithTheContainer()
    {
        const string text = "{ \"tabs\": { \"x\": 1 } }";

        var location = ConfigKeyLocator.Locate(text, ["tabs", "showBranch"]);

        Assert.Equal(ConfigLocationKind.Absent, location.Kind);
        Assert.Equal(1, location.ResolvedDepth);
        Assert.Equal(
            "{ \"x\": 1 }",
            text.Substring(location.Container!.Value.Start, location.Container.Value.Length));
    }

    [Theory]
    [InlineData("{ \"tabs\": 5 }")]
    [InlineData("{ \"tabs\": null }")]
    public void GivenAParentThatIsNotAnObject_WhenLocating_ThenTheKindSaysSo(string text)
    {
        var location = ConfigKeyLocator.Locate(text, ["tabs", "showBranch"]);

        Assert.Equal(ConfigLocationKind.ParentNotObject, location.Kind);
        Assert.Equal(1, location.ResolvedDepth);
    }

    [Fact]
    public void GivenInvalidText_WhenLocating_ThenTheLineOfTheErrorIsReported()
    {
        var location = ConfigKeyLocator.Locate("{\n  \"a\": 1,\n  \"b\" 2\n}", ["a"]);

        Assert.Equal(ConfigLocationKind.Invalid, location.Kind);
        Assert.Equal(3, location.Line);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n")]
    [InlineData("// only a comment\n/* block */")]
    public void GivenBlankOrCommentOnlyText_WhenLocating_ThenItIsEmpty(string text)
    {
        Assert.Equal(ConfigLocationKind.Empty, ConfigKeyLocator.Locate(text, ["a"]).Kind);
    }

    [Fact]
    public void GivenTextAfterTheRootValue_WhenLocating_ThenItIsInvalid()
    {
        Assert.Equal(ConfigLocationKind.Invalid, ConfigKeyLocator.Locate("{} {}", ["a"]).Kind);
    }
}
