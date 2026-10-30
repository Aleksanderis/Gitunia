using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Tests.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Overlays;

/// <summary>Each surface that teaches keys at each of the three command levels (#839).</summary>
[Trait("Os", "Independent")]
public class CommandLevelSurfacesTests
{
    private static readonly KeyBindingTable Table = DefaultKeymap.Create();

    public static TheoryData<CommandLevel> Levels() => [.. Enum.GetValues<CommandLevel>()];

    private static KeymapState Pending(KeymapEngine engine, string keys)
    {
        var feeder = new KeyFeeder(engine);
        feeder.Feed(keys);

        return feeder.State;
    }

    private static IEnumerable<ActionId> HelpActions(IEnumerable<HelpTab> tabs) =>
        tabs.SelectMany(tab => tab.Rows).Select(row => row.Action).OfType<ActionId>();

    [Theory]
    [MemberData(nameof(Levels))]
    public void GivenALevel_WhenBuildingTheHelp_ThenNoActionAboveItIsListedInAnyTab(CommandLevel level)
    {
        var tabs = HelpBuilder.Build(Table, DefaultKeymap.GraphView, level)
            .Where(tab => tab.Title != HelpBuilder.CommandsTitle);

        var above = HelpActions(tabs).Where(action => !ActionCatalog.IsListedAt(action, level));

        Assert.Empty(above);
        Assert.Contains(HelpActions(tabs), action => action == ActionIds.ListDown);
    }

    [Fact]
    public void GivenEachLevel_WhenBuildingTheHelp_ThenTheAdvancedActionsAppearOnlyFromTheirLevel()
    {
        var everyday = HelpActions(HelpBuilder.Build(Table, DefaultKeymap.GraphView, CommandLevel.Everyday)).ToHashSet();
        var advanced = HelpActions(HelpBuilder.Build(Table, DefaultKeymap.GraphView, CommandLevel.Advanced)).ToHashSet();
        var all = HelpActions(HelpBuilder.Build(Table, DefaultKeymap.GraphView, CommandLevel.AdvancedPlus)).ToHashSet();

        Assert.DoesNotContain(ActionIds.BranchFastForward, everyday);
        Assert.Contains(ActionIds.BranchFastForward, advanced);
        Assert.DoesNotContain(ActionIds.BranchSetUpstream, advanced);
        Assert.Contains(ActionIds.BranchSetUpstream, all);
        Assert.Contains(ActionIds.BranchCreate, everyday);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void GivenALevel_WhenBuildingTheHelp_ThenTheClosingLineCountsExactlyTheHiddenCommands(CommandLevel level)
    {
        var tabs = HelpBuilder.Build(Table, DefaultKeymap.GraphView, level);
        var allTabs = HelpBuilder.AllCommandsTabs(Table, CommandLevel.AdvancedPlus);
        var shown = HelpActions(HelpBuilder.AllCommandsTabs(Table, level)).ToHashSet();
        var hidden = HelpActions(allTabs).Distinct().Count(action => !shown.Contains(action));

        var closing = tabs.Single(tab => tab.Title == HelpBuilder.CommandsTitle).Rows
            .Where(row => row.Action == ActionIds.ViewCycleCommandLevel)
            .ToList();

        if (hidden == 0)
        {
            Assert.Empty(closing);

            return;
        }

        var row = Assert.Single(closing);
        Assert.StartsWith($"{hidden} advanced command", row.Description, StringComparison.Ordinal);
        Assert.Equal("<space>*", row.Keys);
    }

    [Fact]
    public void GivenEveryday_WhenBuildingTheHelp_ThenSomeCommandsAreHiddenAndAtAdvancedPlusNoneAre()
    {
        var everyday = HelpBuilder.Build(Table, DefaultKeymap.GraphView, CommandLevel.Everyday);
        var all = HelpBuilder.Build(Table, DefaultKeymap.GraphView, CommandLevel.AdvancedPlus);

        Assert.Contains(HelpActions(everyday), action => action == ActionIds.ViewCycleCommandLevel);
        Assert.DoesNotContain(
            all.Single(tab => tab.Title == HelpBuilder.CommandsTitle).Rows,
            row => row.Action == ActionIds.ViewCycleCommandLevel && row.Description.Contains("hidden", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenAModalScreenIsFocused_WhenBuildingTheHelpAtEveryday_ThenItsOwnKeysAreAllListed()
    {
        var tabs = HelpBuilder.Build(Table, DefaultKeymap.RebaseEditorView, CommandLevel.Everyday);

        var rebaseTodo = HelpActions(tabs.Where(tab => tab.Title == $"{HelpBuilder.AllPrefix}Rebase todo")).ToHashSet();
        var bound = Table.Bindings
            .Where(binding => binding.View == DefaultKeymap.RebaseEditorView && !HelpBuilder.IsUnlisted(binding))
            .Select(binding => binding.Action)
            .ToHashSet();

        Assert.Equal(bound, rebaseTodo);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void GivenALevel_WhenBuildingThePalette_ThenNoActionAboveItIsListed(CommandLevel level)
    {
        var entries = PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView, level);

        Assert.DoesNotContain(entries, entry => !ActionCatalog.IsListedAt(entry.Action, level));
        Assert.Contains(entries, entry => entry.Action == ActionIds.BranchCreate);
        Assert.Contains(entries, entry => entry.Action == ActionIds.ViewCycleCommandLevel);
    }

    [Fact]
    public void GivenEachLevel_WhenBuildingThePalette_ThenTheCountGrowsAndKeylessAdvancedActionsAppearFromTheirLevel()
    {
        var everyday = PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView, CommandLevel.Everyday);
        var advanced = PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView, CommandLevel.Advanced);
        var all = PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView, CommandLevel.AdvancedPlus);

        Assert.True(everyday.Count < advanced.Count);
        Assert.True(advanced.Count < all.Count);
        Assert.DoesNotContain(everyday, entry => entry.Action == ActionIds.SettingsOpenTheme);
        Assert.Contains(advanced, entry => entry.Action == ActionIds.SettingsOpenTheme);
        Assert.DoesNotContain(advanced, entry => entry.Action == ActionIds.ReflogOpen);
        Assert.Contains(all, entry => entry.Action == ActionIds.ReflogOpen);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void GivenALevel_WhenListingTheWhichKeyContinuationsOfTheLeader_ThenNoActionAboveItIsOffered(CommandLevel level)
    {
        var engine = new KeymapEngine(Table);
        var state = Pending(engine, "<space>");

        var continuations = engine.Continuations(state, DefaultKeymap.GraphView, level);

        Assert.DoesNotContain(
            continuations,
            continuation => continuation.Action is { } action && !ActionCatalog.IsListedAt(action, level));
        Assert.Contains(continuations, continuation => continuation.Action == ActionIds.WorkingTreeToggle);
    }

    [Fact]
    public void GivenEachLevel_WhenListingTheBranchChord_ThenItsAdvancedKeysAppearFromTheirLevel()
    {
        var engine = new KeymapEngine(Table);
        var state = Pending(engine, "<space>b");

        var everyday = engine.Continuations(state, DefaultKeymap.GraphView, CommandLevel.Everyday);
        var advanced = engine.Continuations(state, DefaultKeymap.GraphView, CommandLevel.Advanced);
        var all = engine.Continuations(state, DefaultKeymap.GraphView, CommandLevel.AdvancedPlus);

        Assert.DoesNotContain(everyday, continuation => continuation.Action == ActionIds.BranchFastForward);
        Assert.Contains(advanced, continuation => continuation.Action == ActionIds.BranchFastForward);
        Assert.DoesNotContain(advanced, continuation => continuation.Action == ActionIds.BranchSetUpstream);
        Assert.Contains(all, continuation => continuation.Action == ActionIds.BranchSetUpstream);
        Assert.Contains(everyday, continuation => continuation.Action == ActionIds.BranchCreate);
    }

    [Fact]
    public void GivenAGroupWithOnlyHigherLevelActions_WhenListingTheLeaderContinuations_ThenTheGroupIsHiddenWithThem()
    {
        var engine = new KeymapEngine(Table);
        var state = Pending(engine, "<space>");
        var groups = engine.Continuations(state, DefaultKeymap.GraphView, CommandLevel.AdvancedPlus)
            .Where(continuation => continuation.Action is null)
            .Select(continuation => continuation.Key)
            .ToList();

        foreach (var level in Enum.GetValues<CommandLevel>())
        {
            var shown = engine.Continuations(state, DefaultKeymap.GraphView, level).Select(item => item.Key).ToHashSet();
            var prefix = new KeySequence([new KeyEvent(" ")]);
            foreach (var group in groups)
            {
                var hasListed = Table.EffectiveBindings(KeyMode.Normal, DefaultKeymap.GraphView)
                    .Where(binding => binding.Sequence.StartsWith(new KeySequence([.. prefix.Keys, group])))
                    .Any(binding => ActionCatalog.IsListedAt(binding.Action, level));

                Assert.Equal(hasListed, shown.Contains(group));
            }
        }
    }

    [Fact]
    public void GivenNoLevel_WhenListingTheContinuations_ThenEveryBindingIsOffered()
    {
        var engine = new KeymapEngine(Table);
        var state = Pending(engine, "<space>");

        Assert.Equal(
            engine.Continuations(state, DefaultKeymap.GraphView).Count,
            engine.Continuations(state, DefaultKeymap.GraphView, CommandLevel.AdvancedPlus).Count);
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void GivenALevel_WhenBuildingTheActionsMenuOfALocalBranch_ThenOnlyListedActionsRemainInOrder(CommandLevel level)
    {
        var row = new SidebarRow(
            SidebarRowKind.LocalBranch,
            SidebarSection.Branches,
            "x",
            string.Empty,
            null,
            false,
            false,
            0,
            null,
            null);
        var actions = RowActions.OfSidebarRow(row);

        var entries = ContextMenuBuilder.Build(Table, DefaultKeymap.SidebarView, actions, level: level);

        var expected = actions.Where(action => ActionCatalog.IsListedAt(action, level)
            && Table.EffectiveBindings(KeyMode.Normal, DefaultKeymap.SidebarView).Any(binding => binding.Action == action));
        Assert.Equal(expected, entries.Select(entry => entry.Action));
        Assert.Contains(entries, entry => entry.Action == ActionIds.BranchSwitch);
    }

    [Fact]
    public void GivenEachLevel_WhenBuildingTheActionsMenuOfALocalBranch_ThenTheUpstreamActionsNeedAdvancedPlus()
    {
        var row = new SidebarRow(
            SidebarRowKind.LocalBranch,
            SidebarSection.Branches,
            "x",
            string.Empty,
            null,
            false,
            false,
            0,
            null,
            null);
        var actions = RowActions.OfSidebarRow(row);

        var advanced = ContextMenuBuilder.Build(Table, DefaultKeymap.SidebarView, actions, level: CommandLevel.Advanced);
        var all = ContextMenuBuilder.Build(Table, DefaultKeymap.SidebarView, actions, level: CommandLevel.AdvancedPlus);

        Assert.DoesNotContain(advanced, entry => entry.Action == ActionIds.BranchSetUpstream);
        Assert.Contains(all, entry => entry.Action == ActionIds.BranchSetUpstream);
    }
}
