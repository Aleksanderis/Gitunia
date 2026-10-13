using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Xunit;

namespace Gitunia.Core.Tests.Overlays;

public class HelpBuilderTests
{
    private static readonly KeyBindingTable Table = DefaultKeymap.Create();

    [Fact]
    public void GivenGraphView_WhenBuilding_ThenSectionsFollowTheViewChain()
    {
        var sections = HelpBuilder.Build(Table, DefaultKeymap.GraphView);

        Assert.Equal(["Graph", "Lists", "Global"], sections.Select(section => section.Title));
    }

    [Fact]
    public void GivenGraphView_WhenBuilding_ThenEveryEffectiveNormalBindingIsListed()
    {
        var rows = HelpBuilder.Build(Table, DefaultKeymap.GraphView).SelectMany(section => section.Rows).ToList();

        foreach (var action in Table.EffectiveBindings(KeyMode.Normal, DefaultKeymap.GraphView).Select(binding => binding.Action).Distinct())
        {
            Assert.Contains(rows, row => row.Description == ActionCatalog.Describe(action));
        }
    }

    [Fact]
    public void GivenAnActionWithTwoKeys_WhenBuilding_ThenOneRowListsBoth()
    {
        var rows = HelpBuilder.Build(Table, DefaultKeymap.GraphView).SelectMany(section => section.Rows);

        Assert.Contains(new HelpRow("j / Down", "Move down"), rows);
    }

    [Fact]
    public void GivenDifferentViews_WhenBuilding_ThenEachShowsItsOwnKeys()
    {
        var graph = HelpBuilder.Build(Table, DefaultKeymap.GraphView).SelectMany(section => section.Rows).Select(row => row.Description);
        var diff = HelpBuilder.Build(Table, DefaultKeymap.DiffView).SelectMany(section => section.Rows).Select(row => row.Description);

        Assert.Contains("Jump to HEAD", graph);
        Assert.DoesNotContain("Jump to HEAD", diff);
        Assert.Contains("Next hunk", diff);
    }

    [Fact]
    public void GivenAPendingPrefix_WhenBuildingWhichKeyRows_ThenContinuationsAreDescribed()
    {
        var engine = new KeymapEngine(Table);
        var feeder = new KeyFeeder(engine);
        feeder.Feed("g");

        var rows = WhichKeyBuilder.Build(engine.Continuations(feeder.State, DefaultKeymap.GraphView));

        Assert.Contains(new WhichKeyRow("g", "Go to the top"), rows);
        Assert.Contains(new WhichKeyRow("r", "Jump to a ref or hash"), rows);
        Assert.Contains(new WhichKeyRow("t", "Next tab"), rows);
    }

    [Fact]
    public void GivenAChordPrefix_WhenBuildingWhichKeyRows_ThenTheLastKeysAreDescribed()
    {
        var engine = new KeymapEngine(Table);
        var feeder = new KeyFeeder(engine);
        feeder.Feed("Ctrl-w");

        var rows = WhichKeyBuilder.Build(engine.Continuations(feeder.State, DefaultKeymap.GraphView));

        Assert.Contains(new WhichKeyRow("c", "Close the tab"), rows);
    }

    [Theory]
    [InlineData("gg")]
    [InlineData("Ctrl-w c")]
    [InlineData("<space>o")]
    [InlineData("<space><Tab>")]
    [InlineData("Ctrl-p")]
    [InlineData("Shift-Tab")]
    [InlineData("Alt-1")]
    public void GivenDocumentedNotation_WhenFormattingTheParsedSequence_ThenItRoundTrips(string text)
    {
        Assert.Equal(text, KeyFormatter.Format(KeySequenceParser.Parse(text)));
    }
}
