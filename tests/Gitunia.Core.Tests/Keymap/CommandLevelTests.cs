using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Xunit;

namespace Gitunia.Core.Tests.Keymap;

[Trait("Os", "Independent")]
public class CommandLevelTests
{
    private static HashSet<ActionId> KnownActions()
    {
        var actions = DefaultKeymap.Create().Bindings.Select(binding => binding.Action).ToHashSet();
        actions.UnionWith(ActionCatalog.NoDefaultKey);
        actions.UnionWith(ActionCatalog.PaletteOnly);

        return actions;
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenAskingForLevels_ThenEveryBoundAndKeylessActionHasOne()
    {
        var missing = KnownActions().Where(action => ActionCatalog.LevelOf(action) is null).Select(action => action.Value);

        Assert.Empty(missing);
    }

    [Fact]
    public void GivenTheApprovedList_WhenCountingTheBoundActions_ThenThereAre201EverydayAnd45AdvancedAnd10AdvancedPlus()
    {
        // The Level column of docs/internal/ux-keymap.md (192 rows plus the nine tab.go-to.N ids, 45, 10).
        var counts = KnownActions()
            .GroupBy(action => ActionCatalog.LevelOf(action)!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Equal(201, counts[CommandLevel.Everyday]);
        Assert.Equal(45, counts[CommandLevel.Advanced]);
        Assert.Equal(10, counts[CommandLevel.AdvancedPlus]);
    }

    [Theory]
    [InlineData("working-tree.commit", CommandLevel.Everyday)]
    [InlineData("view.cycle-command-level", CommandLevel.Everyday)]
    [InlineData("tab.go-to.3", CommandLevel.Everyday)]
    [InlineData("conflict-editor.take", CommandLevel.Everyday)]
    [InlineData("conflict-editor.scroll-left", CommandLevel.Advanced)]
    [InlineData("conflict-editor.scroll-right", CommandLevel.Advanced)]
    [InlineData("branch.fast-forward", CommandLevel.Advanced)]
    [InlineData("branch.set-upstream", CommandLevel.AdvancedPlus)]
    [InlineData("reflog.open", CommandLevel.AdvancedPlus)]
    public void GivenAnAction_WhenAskingForItsLevel_ThenItFollowsTheApprovedList(string action, CommandLevel expected)
    {
        Assert.Equal(expected, ActionCatalog.LevelOf(new ActionId(action)));
    }

    [Fact]
    public void GivenAnUnknownAction_WhenAskingForItsLevel_ThenThereIsNoneAndItStaysListed()
    {
        var unknown = new ActionId("no.such-action");

        Assert.Null(ActionCatalog.LevelOf(unknown));
        Assert.True(ActionCatalog.IsListedAt(unknown, CommandLevel.Everyday));
    }

    [Theory]
    [InlineData(CommandLevel.Everyday, CommandLevel.Everyday, true)]
    [InlineData(CommandLevel.Everyday, CommandLevel.Advanced, true)]
    [InlineData(CommandLevel.Advanced, CommandLevel.Everyday, false)]
    [InlineData(CommandLevel.Advanced, CommandLevel.Advanced, true)]
    [InlineData(CommandLevel.AdvancedPlus, CommandLevel.Advanced, false)]
    [InlineData(CommandLevel.AdvancedPlus, CommandLevel.AdvancedPlus, true)]
    public void GivenAnActionAndALevel_WhenAskingIfItIsListed_ThenEachLevelIncludesTheOnesBelow(
        CommandLevel actionLevel,
        CommandLevel shown,
        bool expected)
    {
        var action = KnownActions().First(candidate => ActionCatalog.LevelOf(candidate) == actionLevel);

        Assert.Equal(expected, ActionCatalog.IsListedAt(action, shown));
    }

    [Fact]
    public void GivenTheLevels_WhenCycling_ThenTheyGoEverydayAdvancedAdvancedPlusAndBackToEveryday()
    {
        Assert.Equal(CommandLevel.Advanced, CommandLevels.Next(CommandLevel.Everyday));
        Assert.Equal(CommandLevel.AdvancedPlus, CommandLevels.Next(CommandLevel.Advanced));
        Assert.Equal(CommandLevel.Everyday, CommandLevels.Next(CommandLevel.AdvancedPlus));
    }

    [Theory]
    [InlineData("everyday", CommandLevel.Everyday)]
    [InlineData("Advanced", CommandLevel.Advanced)]
    [InlineData("ADVANCED+", CommandLevel.AdvancedPlus)]
    public void GivenAConfigName_WhenParsing_ThenTheLevelIsFoundInAnyCaseAndRoundTrips(string text, CommandLevel expected)
    {
        Assert.True(CommandLevels.TryParse(text, out var level));
        Assert.Equal(expected, level);
        Assert.Equal(expected, ParseOrFail(CommandLevels.NameOf(level)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("expert")]
    [InlineData(null)]
    public void GivenAnUnknownName_WhenParsing_ThenItIsRefused(string? text)
    {
        Assert.False(CommandLevels.TryParse(text, out _));
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenLookingUpTheToggle_ThenItIsBoundToSpaceStarInTheGlobalView()
    {
        var binding = DefaultKeymap.Create().Bindings.Single(candidate => candidate.Action == ActionIds.ViewCycleCommandLevel);

        Assert.Equal("<space>*", KeyFormatter.Display(binding.Sequence));
        Assert.Equal(KeyBindingTable.GlobalView, binding.View);
    }

    private static CommandLevel ParseOrFail(string text)
    {
        Assert.True(CommandLevels.TryParse(text, out var level));

        return level;
    }
}
