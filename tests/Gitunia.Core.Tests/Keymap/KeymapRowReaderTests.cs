using System.Text.Unicode;
using System.Text.Json;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Xunit;

namespace Gitunia.Core.Tests.Keymap;

[Trait("Os", "Independent")]
public class KeymapRowReaderTests
{
    [Fact]
    public void GivenARow_WhenRead_ThenActionViewModesAndKeysAreKept()
    {
        var rows = Read("""[{ "action": "list.down", "view": "Lists", "modes": ["Normal", "Visual"], "keys": ["j", "Down"] }]""");

        var row = Assert.Single(rows);
        Assert.Equal(new ActionId("list.down"), row.Action);
        Assert.Equal("Lists", row.View);
        Assert.Equal([KeyMode.Normal, KeyMode.Visual], row.Modes);
        Assert.Equal(["j", "Down"], row.Keys);
    }

    [Fact]
    public void GivenUnknownProperties_WhenRead_ThenTheyAreSkipped()
    {
        var rows = Read(
            """[{ "level": "Advanced", "label": { "a": [1, 2] }, "action": "x", "view": "Global", "modes": ["Normal"], "keys": ["q"] }]""");

        Assert.Equal(["q"], Assert.Single(rows).Keys);
    }

    [Theory]
    [InlineData("""{ "action": "x" }""")]
    [InlineData("""[{ "action": "x", "view": "Global", "modes": ["Normal"] }]""")]
    [InlineData("""[{ "action": "x", "view": "Global", "modes": ["Nope"], "keys": ["q"] }]""")]
    [InlineData("""[1]""")]
    public void GivenAMalformedFile_WhenRead_ThenItThrows(string json)
    {
        Assert.ThrowsAny<JsonException>(() => Read(json));
    }

    [Theory]
    [InlineData("""[{ "action": "x", "view": "Global", "modes": ["Normal"], "keys": [1, "a"] }]""")]
    [InlineData("""[{ "action": "x", "view": "Global", "modes": ["1"], "keys": ["a"] }]""")]
    public void GivenAWrongTypedOrNumericValue_WhenRead_ThenItThrows(string json)
    {
        Assert.ThrowsAny<JsonException>(() => Read(json));
    }

    private static IReadOnlyList<KeymapRow> Read(string json)
    {
        var buffer = new byte[json.Length * 3];
        Utf8.FromUtf16(json, buffer, out _, out var written);

        return KeymapRowReader.Read(buffer.AsSpan(0, written));
    }
}
