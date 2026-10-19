using System.Text.Json;
using Gitunia.Core.Config;
using Xunit;

namespace Gitunia.Core.Tests.Config;

public class ConfigTextEditorTests
{
    private const string Quoted = "\"square\"";

    [Fact]
    public void GivenAnExistingKey_WhenSetting_ThenOnlyTheValueChangesAndCommentsStay()
    {
        const string text = "{\n  // Pick one.\n  \"frameStyle\": \"rounded\", // trailing\n  \"scanRoots\": [],\n}\n";

        var result = ConfigTextEditor.Set(text, ["frameStyle"], Quoted);

        Assert.True(result.Edited);
        Assert.Equal(text.Replace("\"rounded\"", Quoted, StringComparison.Ordinal), result.Text);
    }

    [Fact]
    public void GivenKeyInOtherCase_WhenSetting_ThenTheExistingMemberIsChangedNotDuplicated()
    {
        var result = ConfigTextEditor.Set("{ \"FrameStyle\": \"rounded\" }", ["frameStyle"], Quoted);

        Assert.Equal("{ \"FrameStyle\": \"square\" }", result.Text);
    }

    [Fact]
    public void GivenDuplicateKeys_WhenSetting_ThenTheLastIsChangedAndShadowingIsReported()
    {
        const string text = "{ \"theme\": \"a\", \"theme\": \"b\" }";

        var result = ConfigTextEditor.Set(text, ["theme"], "\"c\"");

        Assert.Equal("{ \"theme\": \"a\", \"theme\": \"c\" }", result.Text);
        Assert.True(result.Shadowed);
    }

    [Fact]
    public void GivenAMissingKeyInAMultiLineObject_WhenSetting_ThenItIsAppendedLikeTheOthers()
    {
        const string text = "{\n    \"theme\": \"auto\" // keep\n}\n";

        var result = ConfigTextEditor.Set(text, ["frameStyle"], Quoted);

        Assert.Equal("{\n    \"theme\": \"auto\", // keep\n    \"frameStyle\": \"square\"\n}\n", result.Text);
    }

    [Fact]
    public void GivenATrailingComma_WhenInserting_ThenNoSecondCommaIsAdded()
    {
        const string text = "{\n  \"theme\": \"auto\",\n}";

        var result = ConfigTextEditor.Set(text, ["frameStyle"], Quoted);

        Assert.Equal("{\n  \"theme\": \"auto\",\n  \"frameStyle\": \"square\"\n}", result.Text);
    }

    [Fact]
    public void GivenACrLfFile_WhenInserting_ThenNewLinesUseCrLf()
    {
        const string text = "{\r\n  \"theme\": \"auto\"\r\n}\r\n";

        var result = ConfigTextEditor.Set(text, ["frameStyle"], Quoted);

        Assert.Equal("{\r\n  \"theme\": \"auto\",\r\n  \"frameStyle\": \"square\"\r\n}\r\n", result.Text);
    }

    [Fact]
    public void GivenTabIndent_WhenInsertingIntoAnEmptyNestedObject_ThenTabsAreUsed()
    {
        const string text = "{\n\t\"tabs\": {}\n}";

        var result = ConfigTextEditor.Set(text, ["tabs", "showBranch"], "true");

        Assert.Equal("{\n\t\"tabs\": {\n\t\t\"showBranch\": true\n\t}\n}", result.Text);
    }

    [Fact]
    public void GivenASingleLineObject_WhenInserting_ThenTheMemberStaysOnTheLine()
    {
        var result = ConfigTextEditor.Set("{ \"theme\": \"auto\" }", ["frameStyle"], Quoted);

        Assert.Equal("{ \"theme\": \"auto\", \"frameStyle\": \"square\" }", result.Text);
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

    [Fact]
    public void GivenACommentOnlyFile_WhenSetting_ThenTheCommentIsKeptAndTheObjectFollows()
    {
        const string text = "// my config\n";

        var result = ConfigTextEditor.Set(text, ["theme"], "\"dark\"");

        Assert.Equal("// my config\n{\n  \"theme\": \"dark\"\n}\n", result.Text);
    }

    [Fact]
    public void GivenAMissingNestedKey_WhenSetting_ThenTheParentIsCreatedOnOneLine()
    {
        const string text = "{\n  \"theme\": \"auto\"\n}";

        var result = ConfigTextEditor.Set(text, ["tabs", "showBranch"], "true");

        Assert.Equal("{\n  \"theme\": \"auto\",\n  \"tabs\": { \"showBranch\": true }\n}", result.Text);
    }

    [Fact]
    public void GivenAnExistingParent_WhenInsertingANestedKey_ThenItGoesIntoThatParent()
    {
        const string text = "{\n  \"tabs\": {\n    \"other\": 1\n  }\n}";

        var result = ConfigTextEditor.Set(text, ["tabs", "showBranch"], "true");

        Assert.Equal("{\n  \"tabs\": {\n    \"other\": 1,\n    \"showBranch\": true\n  }\n}", result.Text);
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

    [Fact]
    public void GivenABlockCommentAfterTheLastMember_WhenInserting_ThenTheMemberIsNeverHiddenInTheComment()
    {
        const string text = "{\n  \"a\": 1 /* x\n y */ }";

        var result = ConfigTextEditor.Set(text, ["b"], "true");

        Assert.False(result.Edited);
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void GivenATopLevelArray_WhenSetting_ThenItIsRefused()
    {
        Assert.False(ConfigTextEditor.Set("[]", ["theme"], "1").Edited);
    }

    [Fact]
    public void GivenAValueThatIsNotOneJsonValue_WhenSetting_ThenItIsRefused()
    {
        Assert.False(ConfigTextEditor.Set("{}", ["theme"], "1, \"x\": 2").Edited);
    }

    [Fact]
    public void GivenAStringWithQuotesAndNonAscii_WhenQuoting_ThenItRoundTrips()
    {
        var quoted = ConfigTextEditor.StringValue("a\"b é");

        Assert.Equal("a\"b é", JsonDocument.Parse(quoted).RootElement.GetString());
    }
}
