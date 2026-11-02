using System.Text.Json;
using Gitunia.Core.Config;
using Xunit;

namespace Gitunia.Core.Tests.Config;

public class ConfigTextEditorTests
{
    private const string Quoted = "\"square\"";

    [Fact]
    public void GivenStrings_WhenBuildingAListValue_ThenItIsOneLineOfEscapedJson()
    {
        Assert.Equal("[]", ConfigTextEditor.StringListValue([]));
        Assert.Equal("[\"/a\", \"C:\\\\b\"]", ConfigTextEditor.StringListValue(["/a", "C:\\b"]));
    }

    [Fact]
    public void GivenDuplicateKeys_WhenSetting_ThenTheLastIsChangedAndShadowingIsReported()
    {
        const string text = "{ \"theme\": \"a\", \"theme\": \"b\" }";

        var result = ConfigTextEditor.Set(text, ["theme"], "\"c\"");

        Assert.Equal("{ \"theme\": \"a\", \"theme\": \"c\" }", result.Text);
        Assert.True(result.Shadowed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n")]
    [InlineData("{}")]
    [InlineData("{\n}")]
    public void GivenAnEmptyFileOrObject_WhenSetting_ThenTheObjectIsFilled(string text)
    {
        var result = ConfigTextEditor.Set(text, ["theme"], "\"dark\"");

        Assert.True(result.Edited);
        Assert.Contains("\"theme\": \"dark\"", result.Text, StringComparison.Ordinal);
        Assert.Equal("dark", JsonDocument.Parse(result.Text).RootElement.GetProperty("theme").GetString());
    }

    [Theory]
    [InlineData("{ \"tabs\": 5 }")]
    [InlineData("{ \"tabs\": null }")]
    [InlineData("{ \"tabs\": [] }")]
    public void GivenAParentThatIsNotAnObject_WhenSetting_ThenItIsRefusedAndNotReplaced(string text)
    {
        var result = ConfigTextEditor.Set(text, ["tabs", "showBranch"], "true");

        Assert.False(result.Edited);
        Assert.Equal(text, result.Text);
        Assert.Contains("tabs", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAnUnparsableFile_WhenSetting_ThenItIsRefusedWithTheLine()
    {
        const string text = "{\n  \"theme\": \"auto\"\n  \"oops\": 1\n}";

        var result = ConfigTextEditor.Set(text, ["theme"], "\"dark\"");

        Assert.False(result.Edited);
        Assert.Equal(text, result.Text);
        Assert.Contains("line 3", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\n  \"a\": 1 /* x\n y */ }", "b", "true")] // A member would be hidden in the block comment.
    [InlineData("[]", "theme", "1")] // The top level is an array.
    [InlineData("{}", "theme", "1, \"x\": 2")] // The value is not one JSON value.
    public void GivenATextOrValueItCannotEdit_WhenSetting_ThenItIsRefusedAndTheTextStays(
        string text,
        string key,
        string value)
    {
        var result = ConfigTextEditor.Set(text, [key], value);

        Assert.False(result.Edited);
        Assert.Equal(text, result.Text);
    }

    // Text, path ('/' separated), value, expected text.
    public static TheoryData<string, string, string, string> Edits() => new()
    {
        { // Only the multi-line array is replaced.
            "{\n  // roots\n  \"scanRoots\": [\n    \"/a\",\n  ],\n  \"theme\": \"dark\"\n}\n",
            "scanRoots",
            "[\"/a\", \"/b\"]",
            "{\n  // roots\n  \"scanRoots\": [\"/a\", \"/b\"],\n  \"theme\": \"dark\"\n}\n"
        },
        { // Only the value changes, comments stay.
            "{\n  // Pick one.\n  \"frameStyle\": \"rounded\", // trailing\n  \"scanRoots\": [],\n}\n",
            "frameStyle",
            Quoted,
            "{\n  // Pick one.\n  \"frameStyle\": \"square\", // trailing\n  \"scanRoots\": [],\n}\n"
        },
        { // Other case: changed, not duplicated.
            "{ \"FrameStyle\": \"rounded\" }",
            "frameStyle",
            Quoted,
            "{ \"FrameStyle\": \"square\" }"
        },
        { // Appended like the others.
            "{\n    \"theme\": \"auto\" // keep\n}\n",
            "frameStyle",
            Quoted,
            "{\n    \"theme\": \"auto\", // keep\n    \"frameStyle\": \"square\"\n}\n"
        },
        { // A trailing comma gets no second one.
            "{\n  \"theme\": \"auto\",\n}",
            "frameStyle",
            Quoted,
            "{\n  \"theme\": \"auto\",\n  \"frameStyle\": \"square\"\n}"
        },
        { // CRLF files keep CRLF.
            "{\r\n  \"theme\": \"auto\"\r\n}\r\n",
            "frameStyle",
            Quoted,
            "{\r\n  \"theme\": \"auto\",\r\n  \"frameStyle\": \"square\"\r\n}\r\n"
        },
        { // Tab indent in an empty nested object.
            "{\n\t\"tabs\": {}\n}",
            "tabs/showBranch",
            "true",
            "{\n\t\"tabs\": {\n\t\t\"showBranch\": true\n\t}\n}"
        },
        { // A single-line object stays on the line.
            "{ \"theme\": \"auto\" }",
            "frameStyle",
            Quoted,
            "{ \"theme\": \"auto\", \"frameStyle\": \"square\" }"
        },
        { // A comment-only file keeps the comment.
            "// my config\n",
            "theme",
            "\"dark\"",
            "// my config\n{\n  \"theme\": \"dark\"\n}\n"
        },
        { // A missing parent is created on one line.
            "{\n  \"theme\": \"auto\"\n}",
            "tabs/showBranch",
            "true",
            "{\n  \"theme\": \"auto\",\n  \"tabs\": { \"showBranch\": true }\n}"
        },
        { // An existing parent receives the key.
            "{\n  \"tabs\": {\n    \"other\": 1\n  }\n}",
            "tabs/showBranch",
            "true",
            "{\n  \"tabs\": {\n    \"other\": 1,\n    \"showBranch\": true\n  }\n}"
        },
    };

    [Theory]
    [MemberData(nameof(Edits))]
    public void GivenAText_WhenSettingAKey_ThenOnlyThatPartChangesAndTheStyleIsKept(
        string text,
        string path,
        string value,
        string expected)
    {
        var result = ConfigTextEditor.Set(text, path.Split('/'), value);

        Assert.True(result.Edited);
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void GivenAStringWithQuotesAndNonAscii_WhenQuoting_ThenItRoundTrips()
    {
        var quoted = ConfigTextEditor.StringValue("a\"b é");

        Assert.Equal("a\"b é", JsonDocument.Parse(quoted).RootElement.GetString());
    }
}
