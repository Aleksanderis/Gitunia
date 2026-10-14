using Gitunia.Core.Keymap;
using Gitunia.Core.Overlays;
using Xunit;

namespace Gitunia.Core.Tests.Overlays;

public class PaletteEntryBuilderTests
{
    private static readonly KeyBindingTable Table = DefaultKeymap.Create();

    [Fact]
    public void GivenTheDefaultKeymap_WhenBuildingEntries_ThenEveryRegisteredActionAppearsWithItsBinding()
    {
        var entries = PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView);

        foreach (var action in Table.Bindings.Select(binding => binding.Action).Distinct())
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
    public void GivenAView_WhenBuildingEntries_ThenItsOwnActionsComeBeforeActionsOfOtherViews()
    {
        var entries = PaletteEntryBuilder.Build(Table, DefaultKeymap.GraphView).ToList();
        var jumpToHead = entries.FindIndex(entry => entry.Action == ActionIds.GraphJumpToHead);
        var nextHunk = entries.FindIndex(entry => entry.Action == ActionIds.DiffNextHunk);

        Assert.True(jumpToHead < nextHunk);
    }

    [Fact]
    public void GivenTabGoToActions_WhenDescribing_ThenTitleNamesTheTab()
    {
        Assert.Equal("Go to tab 3 (same as 3gt)", ActionCatalog.Describe(ActionIds.TabGoTo(3)));
    }
}
