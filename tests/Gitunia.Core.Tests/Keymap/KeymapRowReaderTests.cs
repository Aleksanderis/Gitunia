using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Xunit;

namespace Gitunia.Core.Tests.Keymap;

public class KeymapRowReaderTests
{
    private const int DefaultBindingCount = 550;
    private const double ParseBudgetMilliseconds = 10;

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

    [Fact]
    public void GivenTheEmbeddedFile_WhenLoaded_ThenItYieldsTheBindingsOfTheCodeDefaults()
    {
        Assert.Equal(DefaultBindingCount, DefaultKeymap.CreateBindings().Count);
    }

    [Fact]
    public void GivenTheEmbeddedFile_WhenParsed_ThenItStaysWithinTheStartupBudget()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Gitunia.Core", "Keymap", "keymap.default.json");
        var bytes = File.ReadAllBytes(path);
        Read(bytes);

        var stopwatch = Stopwatch.StartNew();
        Read(bytes);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed.TotalMilliseconds < ParseBudgetMilliseconds, $"{stopwatch.Elapsed.TotalMilliseconds} ms");
    }

    private static IReadOnlyList<KeymapRow> Read(string json) => Read(Encoding.UTF8.GetBytes(json));

    private static IReadOnlyList<KeymapRow> Read(byte[] utf8) => KeymapRowReader.Read(utf8);
}
