using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Tests.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Overlays;

public class HelpBuilderTests
{
    private static readonly KeyBindingTable Table = DefaultKeymap.Create();

    private static readonly ActionGroup[] Groups =
    [
        ActionGroups.Branch,
        ActionGroups.Merge,
        ActionGroups.Tag,
        ActionGroups.Remote,
        ActionGroups.Worktree,
        ActionGroups.Stash,
        ActionGroups.WorkingTree,
        ActionGroups.Copy,
        ActionGroups.Diff,
        ActionGroups.Details,
        ActionGroups.Tools,
        ActionGroups.Navigate,
        ActionGroups.View,
        ActionGroups.Tabs,
        ActionGroups.General,
    ];

    [Fact]
    public void GivenGraphView_WhenBuilding_ThenSectionsFollowTheViewChain()
    {
        var sections = HelpBuilder.Build(Table, DefaultKeymap.GraphView);

        Assert.Equal(["Graph", "Lists", "Global", "Markers"], sections.Select(section => section.Title));
    }

    [Theory]
    [InlineData(DefaultKeymap.GraphView)]
    [InlineData(DefaultKeymap.SidebarView)]
    public void GivenGraphOrSidebar_WhenBuilding_ThenTheMarkersSectionExplainsTheUpstreamGoneMark(string view)
    {
        var markers = Assert.Single(HelpBuilder.Build(Table, view), section => section.Title == "Markers");

        Assert.Contains(
            markers.Rows,
            row => row.Keys == "✗ / (x)" && row.Description.StartsWith("Upstream gone", StringComparison.Ordinal));
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
        var diff = HelpBuilder.Build(Table, DefaultKeymap.DiffView).SelectMany(section => section.Rows).Select(row => row.Description).ToList();

        Assert.Contains("Jump to HEAD", graph);
        Assert.DoesNotContain("Jump to HEAD", diff);
        Assert.Contains("Next hunk", diff);
    }

    [Fact]
    public void GivenGraphView_WhenBuilding_ThenEveryRowHasKeysAndADescriptionInOneNotation()
    {
        var rows = HelpBuilder.Build(Table, DefaultKeymap.GraphView).SelectMany(section => section.Rows).ToList();

        Assert.All(rows, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Keys));
            Assert.False(string.IsNullOrWhiteSpace(row.Description), $"The keys {row.Keys} have no description.");
            Assert.DoesNotContain('^', row.Keys.Replace("Ctrl-^", string.Empty, StringComparison.Ordinal));
        });
    }

    [Fact]
    public void GivenGraphView_WhenBuilding_ThenTheHelpMentionsTheMultiplexerSafeTabJump()
    {
        var rows = HelpBuilder.Build(Table, DefaultKeymap.GraphView).SelectMany(section => section.Rows);

        Assert.Contains(rows, row => row.Keys == "Alt-3" && row.Description.Contains("3gt", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAPendingPrefix_WhenBuildingWhichKeyRows_ThenContinuationsAreDescribed()
    {
        var engine = new KeymapEngine(Table);
        var feeder = new KeyFeeder(engine);
        feeder.Feed("g");

        var rows = WhichKeyBuilder.Build(
            engine.Continuations(feeder.State, DefaultKeymap.GraphView),
            feeder.State.PendingSequence);

        Assert.Contains(new WhichKeyRow("g", "Go to the top"), rows);
        Assert.Contains(new WhichKeyRow("t", "Next tab"), rows);
    }

    [Theory]
    [InlineData(false, new[] { "m", "M", "p", "r", "i", "x", "v" }, new[] { "a", "c", "k", "s", "f", "w", "d", "I" })]
    [InlineData(true, new[] { "m", "a", "c", "k" }, new[] { "s", "f", "w", "d", "I" })]
    public void GivenTheGitPrefix_WhenBuildingWhichKeyRows_ThenOnlyTheListedKeysShow(
        bool operationInProgress,
        string[] listed,
        string[] hidden)
    {
        var engine = new KeymapEngine(Table);
        var feeder = new KeyFeeder(engine);
        feeder.Feed("<space>");
        feeder.Feed("g");

        var keys = WhichKeyBuilder.Build(
            engine.Continuations(feeder.State, DefaultKeymap.GraphView),
            feeder.State.PendingSequence,
            operationInProgress).Select(row => row.Key).ToList();

        Assert.All(listed, key => Assert.Contains(key, keys));
        Assert.All(hidden, key => Assert.DoesNotContain(key, keys));
    }

    [Theory]
    [InlineData("b", "+more (branch operations)")]
    [InlineData("g", "+more (merge, rebase, reset, pick)")]
    [InlineData("T", "+more (tag operations)")]
    [InlineData("t", "+more (external tools)")]
    public void GivenTheLeaderPrefix_WhenBuildingWhichKeyRows_ThenGroupRowsNameContents(string key, string expected)
    {
        var engine = new KeymapEngine(Table);
        var feeder = new KeyFeeder(engine);
        feeder.Feed("<space>");

        var rows = WhichKeyBuilder.Build(
            engine.Continuations(feeder.State, DefaultKeymap.GraphView),
            feeder.State.PendingSequence);

        Assert.Contains(new WhichKeyRow(key, expected), rows);
    }

    [Fact]
    public void GivenTheGroupNames_WhenLookingThemUp_ThenEveryPrefixIsABoundLeaderChord()
    {
        var sequences = Table.Bindings.Select(binding => KeyFormatter.Format(binding.Sequence)).ToList();

        Assert.All(
            WhichKeyBuilder.GroupPrefixes,
            prefix => Assert.Contains(
                sequences,
                sequence => sequence.StartsWith(prefix, StringComparison.Ordinal) && sequence.Length > prefix.Length));
    }

    [Fact]
    public void GivenTheLeaderPrefix_WhenBuildingWhichKeyRows_ThenActionsComeBeforeGroupsEachSorted()
    {
        var engine = new KeymapEngine(Table);
        var feeder = new KeyFeeder(engine);
        feeder.Feed("<space>");

        var rows = WhichKeyBuilder.Build(
            engine.Continuations(feeder.State, DefaultKeymap.GraphView),
            feeder.State.PendingSequence);

        AssertOrdered(rows);
        Assert.Contains(rows, row => row.Description.StartsWith("+more", StringComparison.Ordinal));
        Assert.StartsWith("+more", rows[^1].Description, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenANestedPrefix_WhenBuildingWhichKeyRows_ThenActionsComeBeforeGroupsEachSorted()
    {
        var engine = new KeymapEngine(Table);
        var feeder = new KeyFeeder(engine);
        feeder.Feed("<space>g");

        var rows = WhichKeyBuilder.Build(
            engine.Continuations(feeder.State, DefaultKeymap.GraphView),
            feeder.State.PendingSequence);

        Assert.True(rows.Count > 1);
        AssertOrdered(rows);
    }

    [Fact]
    public void GivenMixedCaseKeys_WhenBuildingWhichKeyRows_ThenLowercaseComesFirst()
    {
        var rows = WhichKeyBuilder.Build(
            [
                new Continuation(new KeyEvent("B"), new ActionId("x.y"), false),
                new Continuation(new KeyEvent("a"), new ActionId("x.y"), false),
                new Continuation(new KeyEvent("b"), new ActionId("x.y"), false),
                new Continuation(new KeyEvent("A"), null, true),
                new Continuation(new KeyEvent("c"), null, true),
                new Continuation(new KeyEvent("d"), new ActionId("x.y"), true),
            ],
            new KeySequence([]));

        Assert.Equal(["a", "b", "B", "d", "A", "c"], rows.Select(row => row.Key));
    }

    [Fact]
    public void GivenAGroupWithoutAName_WhenBuildingWhichKeyRows_ThenItStaysPlainMore()
    {
        var engine = new KeymapEngine(Table);
        var feeder = new KeyFeeder(engine);

        var rows = WhichKeyBuilder.Build(
            engine.Continuations(feeder.State, DefaultKeymap.GraphView),
            feeder.State.PendingSequence);

        Assert.Contains(new WhichKeyRow("Ctrl-w", "+more"), rows);
    }

    [Fact]
    public void GivenAChordPrefix_WhenBuildingWhichKeyRows_ThenTheLastKeysAreDescribed()
    {
        var engine = new KeymapEngine(Table);
        var feeder = new KeyFeeder(engine);
        feeder.Feed("Ctrl-w");

        var rows = WhichKeyBuilder.Build(
            engine.Continuations(feeder.State, DefaultKeymap.GraphView),
            feeder.State.PendingSequence);

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
    [InlineData("Ctrl-w <")]
    public void GivenDocumentedNotation_WhenFormattingTheParsedSequence_ThenItRoundTrips(string text)
    {
        Assert.Equal(text, KeyFormatter.Format(KeySequenceParser.Parse(text)));
    }

    [Fact]
    public void GivenTheLessThanKey_WhenFormatting_ThenItShowsTheCharacterNotLt()
    {
        Assert.Equal("<", KeyFormatter.Format(new KeyEvent("<")));
        Assert.Equal("Ctrl-w <", KeyFormatter.Format(KeySequenceParser.Parse("Ctrl-w <lt>")));
        Assert.DoesNotContain(Table.Bindings, binding => KeyFormatter.Format(binding.Sequence).Contains("lt>"));
    }

    private static void AssertOrdered(IReadOnlyList<WhichKeyRow> rows)
    {
        var groupStart = rows.ToList().FindIndex(row => row.Description.StartsWith("+more", StringComparison.Ordinal));
        groupStart = groupStart < 0 ? rows.Count : groupStart;
        Assert.All(rows.Skip(groupStart), row => Assert.StartsWith("+more", row.Description, StringComparison.Ordinal));
        AssertSorted([.. rows.Take(groupStart)]);
        AssertSorted([.. rows.Skip(groupStart)]);
    }

    private static void AssertSorted(List<WhichKeyRow> part)
    {
        var expected = part.OrderBy(row => row.Key, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(row => row.Key, StringComparer.Ordinal).Select(row => row.Key);
        Assert.Equal(expected, part.Select(row => row.Key));
    }

    [Theory]
    [InlineData(DefaultKeymap.GraphView)]
    [InlineData(DefaultKeymap.SidebarView)]
    [InlineData(DefaultKeymap.DiffView)]
    [InlineData(DefaultKeymap.WorkingUnstagedView)]
    public void GivenAView_WhenBuilding_ThenEveryBoundActionIsInExactlyOneTabAndNoneIsUngrouped(string view)
    {
        var tabs = HelpBuilder.Build(Table, view).Where(tab => tab.Title != "Markers").ToList();
        var titles = Table.EffectiveBindings(KeyMode.Normal, view)
            .Select(binding => ActionCatalog.Describe(binding.Action))
            .Distinct()
            .ToList();

        var rowDescriptions = tabs.SelectMany(tab => tab.Rows).Select(row => row.Description).ToList();

        Assert.All(titles, title => Assert.Single(rowDescriptions, description => description == title));
        Assert.DoesNotContain(tabs.SelectMany(tab => tab.Sections), section => section.Title == "Other");
    }

    [Fact]
    public void GivenGraphView_WhenBuilding_ThenSubheadersFollowTheCatalogGroupOrder()
    {
        var graph = HelpBuilder.Build(Table, DefaultKeymap.GraphView)[0];
        var orders = graph.Sections
            .Select(section => Groups.First(group => group.Header == section.Title).Order)
            .ToList();

        Assert.True(orders.Count > 2);
        Assert.Equal(orders.Order(), orders);
        var titles = graph.Sections.Select(section => section.Title).ToList();
        Assert.Equal(titles.Distinct(), titles);
    }
}
