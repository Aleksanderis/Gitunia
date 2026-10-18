using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Xunit;

namespace Gitunia.Core.Tests.Overlays;

public class PaletteEntryBuilderTests
{
    private static readonly KeyBindingTable Table = DefaultKeymap.Create();

    [Fact]
    public void GivenTheDefaultKeymap_WhenBuildingEntries_ThenEveryNonModalActionAppearsWithItsBinding()
    {
        var entries = PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView);

        foreach (var action in Table.Bindings.Select(binding => binding.Action).Distinct().Where(action => !ActionCatalog.IsModalOnly(action)))
        {
            var entry = Assert.Single(entries, candidate => candidate.Action == action);
            Assert.NotEqual(string.Empty, entry.Keys);
            Assert.NotEqual(string.Empty, entry.Title);
        }
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenCheckingTitles_ThenEveryActionHasAHumanReadableOne()
    {
        var missing = Table.Bindings.Select(binding => binding.Action).Distinct().Where(action => !ActionCatalog.IsDescribed(action)).ToList();

        Assert.True(missing.Count == 0, $"Actions without a title in ActionCatalog: {string.Join(", ", missing)}");
    }

    [Fact]
    public void GivenAnActionWithSeveralKeys_WhenBuildingEntries_ThenKeysAreListedNormalModeFirst()
    {
        var entries = PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView);

        Assert.Equal("j / Down", entries.Single(entry => entry.Action == ActionIds.ListDown).Keys);
        Assert.Equal(": / Ctrl-p", entries.Single(entry => entry.Action == ActionIds.PaletteOpen).Keys);
        Assert.Equal("Ctrl-w c", entries.Single(entry => entry.Action == ActionIds.TabClose).Keys);
        Assert.Equal("Ctrl-t / <space>o", entries.Single(entry => entry.Action == ActionIds.PickerOpen).Keys);
    }

    [Fact]
    public void GivenAView_WhenBuildingEntries_ThenItsOwnActionsComeBeforeActionsOfOtherViewsInTheSameGroup()
    {
        var entries = PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView).ToList();
        var jumpToHead = entries.FindIndex(entry => entry.Action == ActionIds.GraphJumpToHead);
        var expand = entries.FindIndex(entry => entry.Action == ActionIds.SidebarExpand);

        Assert.Equal(entries[jumpToHead].Group, entries[expand].Group);
        Assert.True(jumpToHead < expand);
    }

    [Fact]
    public void GivenModalOnlyActions_WhenBuildingEntries_ThenTheyAreHidden()
    {
        var entries = PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView);

        Assert.DoesNotContain(entries, entry => entry.Action == ActionIds.ConfirmYes);
        Assert.DoesNotContain(entries, entry => entry.Action == ActionIds.ConfirmNo);
        Assert.DoesNotContain(entries, entry => entry.Action == ActionIds.PickerNext);
        Assert.DoesNotContain(entries, entry => entry.Action == ActionIds.PickerAccept);
        Assert.DoesNotContain(entries, entry => entry.Action == ActionIds.InputAccept);
        Assert.Contains(entries, entry => entry.Action == ActionIds.PickerOpen);
    }

    [Fact]
    public void GivenAnUnimplementedAction_WhenBuildingEntries_ThenItIsListedButMarked()
    {
        var unimplemented = new HashSet<ActionId> { ActionIds.GraphJumpToHead };

        var entries = PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView, unimplemented);

        Assert.False(entries.Single(entry => entry.Action == ActionIds.GraphJumpToHead).IsImplemented);
        Assert.True(entries.Single(entry => entry.Action == ActionIds.ListDown).IsImplemented);
    }

    [Fact]
    public void GivenTabGoToActions_WhenDescribing_ThenTitleNamesTheTab()
    {
        Assert.Equal("Go to tab 3 (same as 3gt)", ActionCatalog.Describe(ActionIds.TabGoTo(3)));
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenLookingUpGroups_ThenEveryBoundActionHasOne()
    {
        var missing = Table.Bindings
            .Select(binding => binding.Action)
            .Distinct()
            .Where(action => ActionCatalog.GroupOf(action) is null)
            .ToList();

        Assert.True(missing.Count == 0, $"Actions without a group in ActionCatalog: {string.Join(", ", missing)}");
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenBuildingEntries_ThenGroupsAreContiguousAndNamedByTheCatalog()
    {
        var entries = PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView);

        var runs = entries.Select(entry => entry.Group).Distinct().ToList();
        Assert.Equal(entries.Select(entry => entry.Group).Distinct().Count(), runs.Count);
        Assert.All(entries, entry => Assert.Equal(ActionCatalog.GroupOf(entry.Action)!.Header, entry.Group));
        var starts = entries.Where((entry, index) => index == 0 || entries[index - 1].Group != entry.Group);
        Assert.Equal(runs.Count, starts.Count());
    }

    [Fact]
    public void GivenTheWhichKeyGroups_WhenComparingVocabulary_ThenEachNamesAnActionGroup()
    {
        Assert.Equal("branch operations", ActionGroups.Branch.WhichKeyName);
        Assert.Equal("tag operations", ActionGroups.Tag.WhichKeyName);
        Assert.Equal("worktree operations", ActionGroups.Worktree.WhichKeyName);
        Assert.Equal("stash operations", ActionGroups.Stash.WhichKeyName);
        Assert.Equal(7, WhichKeyBuilder.GroupPrefixes.Count);
    }
}
