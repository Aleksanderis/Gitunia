using Gitunia.Core.Config;
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
}
