using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Tests.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Overlays;

[Trait("Os", "Independent")]
public class HelpAllCommandsTests
{
    private static readonly KeyBindingTable Table = DefaultKeymap.Create();

    private static readonly string[] HiddenVariantKeys = ["d", "f", "I", "w"];
    private static readonly string[] ClusterActions = ["b.one", "a.one", "c.one", "b.two", "a.two"];
    private static readonly string[] ArrangedActions = ["b.one", "b.two", "a.one", "a.two", "c.one"];
    private static readonly string?[] ArrangedClusters = ["b", "b", "a", "a", null];

    [Fact]
    public void GivenTheDefaultKeymap_WhenBuildingTheAllCommandsTabs_ThenEveryBoundActionIsInSomeTabUnlessAllowListed()
    {
        var listed = HelpBuilder.AllCommandsTabs(Table)
            .SelectMany(tab => tab.Rows)
            .Select(row => row.Action)
            .OfType<ActionId>()
            .ToHashSet();

        var missing = Table.Bindings
            .Where(binding => !HelpBuilder.IsUnlisted(binding))
            .Select(binding => binding.Action)
            .Where(action => !listed.Contains(action))
            .Distinct()
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void GivenTheAllowList_WhenReadingIt_ThenItIsOnlyTheModalPromptsAndTheirAnswers()
    {
        var unlisted = Table.Bindings.Where(HelpBuilder.IsUnlisted).ToList();

        Assert.NotEmpty(unlisted);
        Assert.All(
            unlisted,
            binding => Assert.True(
                binding.View is "Picker" or "Palette" or "ContextMenu" or "Help"
                    || binding.Action.Value is "input.accept" or "confirm.yes" or "confirm.no",
                $"{binding.View} {binding.Action.Value} is hidden from the help."));
    }

    [Fact]
    public void GivenTheViewsOfTheKeymap_WhenListingThem_ThenEachIsOnAnAllCommandsTabOrAllowListed()
    {
        var onTabs = HelpBuilder.AllCommandsTabs(Table).SelectMany(tab => tab.Rows).Select(row => row.Action).ToHashSet();
        var viewsWithoutTab = Table.Bindings
            .Where(binding => !HelpBuilder.IsUnlisted(binding))
            .GroupBy(binding => binding.View)
            .Where(group => group.All(binding => !onTabs.Contains(binding.Action)))
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(viewsWithoutTab);
    }

    [Fact]
    public void GivenTheGitPrefix_WhenBuildingWhichKeyRows_ThenTheRowsAfterTheMoreRowAreExactlyTheUnlistedActions()
    {
        var engine = new KeymapEngine(Table);
        var feeder = new KeyFeeder(engine);
        feeder.Feed("<space>");
        feeder.Feed("b");
        feeder.Feed("r");

        var rows = WhichKeyBuilder.Build(
            engine.Continuations(feeder.State, DefaultKeymap.GraphView),
            feeder.State.PendingSequence);
        var more = rows.ToList().FindIndex(row => row.Key == "+");

        Assert.Equal(rows.Count - 1, more + 4);
        Assert.DoesNotContain(rows.Take(more), row => HiddenVariantKeys.Contains(row.Key));
    }

    [Fact]
    public void GivenAnActionOfTheAllowList_WhenBoundInAListedView_ThenItIsStillNotListedByItself()
    {
        var extra = new KeyBinding(
            KeyMode.Normal,
            DefaultKeymap.GraphView,
            KeySequenceParser.Parse("<space>Q"),
            ActionIds.InputAccept);

        Assert.True(HelpBuilder.IsUnlisted(extra));
    }

    [Fact]
    public void GivenAnAllCommandsTab_WhenReadingItsRows_ThenNoActionIsListedTwiceAndNoneIsUngrouped()
    {
        foreach (var tab in HelpBuilder.AllCommandsTabs(Table))
        {
            var actions = tab.Rows.Select(row => row.Action).ToList();

            Assert.Equal(actions.Distinct(), actions);
            Assert.DoesNotContain(tab.Sections, section => section.Title == "Other");
        }
    }

    [Fact]
    public void GivenAnActionWithTwoKeys_WhenBuilding_ThenTheMainKeyComesFirstAndTheOthersFollowOnTheSameRow()
    {
        var rows = HelpBuilder.AllCommandsTabs(Table).First(tab => tab.Title == "All Global").Rows;

        var row = Assert.Single(rows, item => item.Action == ActionIds.PaneNext);

        Assert.Equal("Tab", row.Keys);
        Assert.Equal("Ctrl-w w", row.Also);
        Assert.Contains("(also Ctrl-w w)", row.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenVisualOnlyKeys_WhenBuilding_ThenTheVisualKeyIsTaggedInTheContextualAndTheAllTabs()
    {
        var contextual = HelpBuilder.Contextual(Table, DefaultKeymap.GraphView).SelectMany(tab => tab.Rows);
        var all = HelpBuilder.AllCommandsTabs(Table).SelectMany(tab => tab.Rows);

        Assert.Contains(
            contextual,
            row => row.Action == ActionIds.VisualCopy
                && row.Description.StartsWith("(Visual mode)", StringComparison.Ordinal));
        Assert.Contains(all, row => row.Action == ActionIds.VisualCopy);
    }

    [Fact]
    public void GivenSectionsWithClusters_WhenBuilding_ThenAGapOnlySeparatesDifferentClustersAndNeverStartsASection()
    {
        var sections = HelpBuilder.AllCommandsTabs(Table).SelectMany(tab => tab.Sections).ToList();

        Assert.All(sections, section => Assert.False(section.Rows[0].GapBefore));
        Assert.Contains(sections, section => section.Rows.Any(row => row.GapBefore));
        foreach (var section in sections)
        {
            var clusters = section.Rows.Select(row => ActionClusters.ClusterOf(row.Action!.Value)).ToList();
            for (var index = 1; index < section.Rows.Count; index++)
            {
                if (!section.Rows[index].GapBefore)
                {
                    // No gap: the same cluster, or two loose actions (a block of singles stays together).
                    var looseBoth = clusters.Count(name => name == clusters[index]) == 1
                        && clusters.Count(name => name == clusters[index - 1]) == 1;
                    Assert.True(clusters[index - 1] == clusters[index] || looseBoth);
                }
            }
        }
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenBuilding_ThenThereIsNoCustomTabAndTheCommandsTabIsThere()
    {
        var tabs = HelpBuilder.Build(Table, DefaultKeymap.GraphView);

        Assert.DoesNotContain(tabs, tab => tab.Title == HelpBuilder.CustomTitle);
        Assert.Contains(tabs, tab => tab.Title == HelpBuilder.CommandsTitle);
    }

    [Fact]
    public void GivenAppliedKeyEntries_WhenBuilding_ThenTheCustomTabListsAddedChangedAndUnboundKeysNotTheSkippedOnes()
    {
        var result = KeymapConfigurator.Apply(
            Table,
            [
                new ConfigKeyEntry { View = "Graph", Key = "J", Action = "list.up" },
                new ConfigKeyEntry { View = "Graph", Key = "<space>h", Action = "none" },
                new ConfigKeyEntry { View = "Graph", Key = "Q", Action = "no.such-action" },
            ]);

        var tabs = HelpBuilder.Build(result.Table, DefaultKeymap.GraphView);
        var custom = Assert.Single(tabs, tab => tab.Title == HelpBuilder.CustomTitle);
        var rows = custom.Rows.ToList();

        Assert.Single(result.Problems);
        Assert.Contains(
            rows,
            row => row.Keys == "J"
                && row.Description.StartsWith(ActionCatalog.Describe(ActionIds.ListUp), StringComparison.Ordinal));
        Assert.Contains(
            rows,
            row => row.Keys == "<space>h"
                && row.Description == $"Unbound: {ActionCatalog.Describe(ActionIds.GraphJumpToHead)}");
        Assert.DoesNotContain(rows, row => row.Keys == "Q");
        Assert.Equal(["Graph"], custom.Sections.Select(section => section.Title));
        Assert.Equal(tabs.Count - 1, tabs.ToList().IndexOf(custom));
    }

    [Fact]
    public void GivenAChangedKey_WhenBuilding_ThenTheCustomRowNamesTheActionItReplaced()
    {
        var result = KeymapConfigurator.Apply(
            Table,
            [new ConfigKeyEntry { View = "Lists", Key = "j", Action = "list.up" }]);
        var tabs = HelpBuilder.Build(result.Table, DefaultKeymap.GraphView);

        var custom = Assert.Single(tabs, tab => tab.Title == HelpBuilder.CustomTitle);
        var row = Assert.Single(custom.Rows);

        Assert.Equal(
            $"{ActionCatalog.Describe(ActionIds.ListUp)}  (was: {ActionCatalog.Describe(ActionIds.ListDown)})",
            row.Description);
    }

    [Fact]
    public void GivenTheCommandsTab_WhenBuilding_ThenItNamesThePaletteAndTheMousePaths()
    {
        var tabs = HelpBuilder.Build(Table, DefaultKeymap.GraphView);

        var commands = Assert.Single(tabs, tab => tab.Title == HelpBuilder.CommandsTitle);

        Assert.Equal(["Command palette", "Help", "Mouse"], commands.Sections.Select(section => section.Title));
    }

    [Fact]
    public void GivenAKeyTitleOrActionId_WhenFiltering_ThenMatchingRowsAppearOnceAcrossAllSections()
    {
        var tabs = HelpBuilder.Build(Table, DefaultKeymap.GraphView);

        var byKey = HelpBuilder.Filter(tabs, "<SPACE>gc");
        var byId = HelpBuilder.Filter(tabs, "graph.copy-hash");
        var none = HelpBuilder.Filter(tabs, "zzzzqqq");

        Assert.All(byKey.Rows, row => Assert.Contains("<space>gc", row.Keys + row.Text + row.Action?.Value, StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(byKey.Rows);
        Assert.Contains(byId.Rows, row => row.Action?.Value == "graph.copy-hash");
        Assert.Equal(byKey.Rows.Count(), byKey.Rows.Select(row => (row.Action?.Value, row.Keys)).Distinct().Count());
        Assert.Equal("Nothing matches the filter.", Assert.Single(none.Rows).Description);
    }

    [Fact]
    public void GivenTheGraphView_WhenBuilding_ThenTheContextualTabsComeFirstThenAllCommandsThenCommands()
    {
        var titles = HelpBuilder.Build(Table, DefaultKeymap.GraphView).Select(tab => tab.Title).ToList();

        Assert.Equal(["Graph", "Lists", "Global", "Markers", "All Global"], titles.Take(5));
        Assert.Equal(HelpBuilder.CommandsTitle, titles[^1]);
    }

    [Fact]
    public void GivenItemsOfOneGroup_WhenArranging_ThenClustersComeFirstInOrderAndLooseOnesLast()
    {
        var actions = ClusterActions.Select(text => new ActionId(text)).ToList();

        var arranged = ActionClusters.Arrange(actions, action => action);

        Assert.Equal(ArrangedActions, arranged.Select(item => item.Item.Value));
        Assert.Equal(ArrangedClusters, arranged.Select(item => item.Cluster));
    }
}
