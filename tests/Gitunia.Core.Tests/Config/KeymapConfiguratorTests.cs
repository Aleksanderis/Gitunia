using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Tests.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Config;

[Trait("Os", "Independent")]
public class KeymapConfiguratorTests
{
    private static readonly KeyBindingTable Defaults = DefaultKeymap.Create();

    [Fact]
    public void GivenNoEntries_WhenApplying_ThenTheDefaultTableIsReturned()
    {
        var result = KeymapConfigurator.Apply(Defaults, null);

        Assert.Same(Defaults, result.Table);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void GivenAnEntryBindingAFreeKey_WhenPressingIt_ThenTheActionFires()
    {
        var result = Apply(Entry("Normal", "Graph", "J", "list.down"));

        var feeder = new KeyFeeder(new KeymapEngine(result.Table));

        Assert.Empty(result.Problems);
        Assert.Equal(ActionIds.ListDown, feeder.Feed("J").Action);
    }

    [Fact]
    public void GivenAnEntryOnADefaultKey_WhenPressingIt_ThenTheNewActionReplacesTheOldOne()
    {
        var result = Apply(Entry("Normal", "Global", "q", "palette.open"));

        var feeder = new KeyFeeder(new KeymapEngine(result.Table));

        Assert.Empty(result.Problems);
        Assert.Equal(ActionIds.PaletteOpen, feeder.Feed("q").Action);
    }

    [Fact]
    public void GivenAnUnbindEntry_WhenPressingTheKey_ThenItIsUnbound()
    {
        var result = Apply(Entry("Normal", "Global", "q", "none"));

        var feeder = new KeyFeeder(new KeymapEngine(result.Table));

        Assert.Empty(result.Problems);
        Assert.Equal(KeyOutcome.Unbound, feeder.Feed("q").Outcome);
    }

    [Fact]
    public void GivenAnUnbindOfAKeyThatIsNotBound_WhenApplying_ThenItIsReportedAndNothingChanges()
    {
        var result = Apply(Entry("Normal", "Global", "Z", "none"));

        Assert.Single(result.Problems);
        Assert.Equal(Defaults.Bindings.Count, result.Table.Bindings.Count);
    }

    [Fact]
    public void GivenAnEntryWithoutModeAndView_WhenApplying_ThenItIsBoundInNormalModeGlobally()
    {
        var result = Apply(new ConfigKeyEntry { Key = "<space>Q", Action = "app.quit" });

        Assert.Empty(result.Problems);
        Assert.Contains(
            result.Table.Bindings,
            binding => binding is { Mode: KeyMode.Normal, View: KeyBindingTable.GlobalView, Action.Value: "app.quit" }
                && binding.Sequence.Equals(KeySequenceParser.Parse("<space>Q")));
    }

    [Fact]
    public void GivenAKeyThatIsAPrefixOfADefaultChord_WhenApplying_ThenItIsRefusedAndTheChordStillWorks()
    {
        var result = Apply(Entry("Normal", "Graph", "g", "list.down"));

        var feeder = new KeyFeeder(new KeymapEngine(result.Table));

        var problem = Assert.Single(result.Problems);
        Assert.Contains("start of a chord", problem.Message, StringComparison.Ordinal);
        Assert.Equal(ActionIds.ListTop, feeder.Feed("gg").Action);
    }

    [Fact]
    public void GivenAChordThatExtendsADefaultCompleteKey_WhenApplying_ThenItIsRefusedAndTheKeyStillWorks()
    {
        var result = Apply(Entry("Normal", "Global", "qq", "list.down"));

        var feeder = new KeyFeeder(new KeymapEngine(result.Table));

        Assert.Single(result.Problems);
        Assert.Equal(ActionIds.AppQuit, feeder.Feed("q").Action);
    }

    [Fact]
    public void GivenTwoEntriesThatConflictWithEachOther_WhenApplying_ThenTheFirstWinsAndTheSecondIsReported()
    {
        var result = Apply(
            Entry("Normal", "Graph", "ZZ", "list.down"),
            Entry("Normal", "Graph", "Z", "list.up"));

        var feeder = new KeyFeeder(new KeymapEngine(result.Table));

        var problem = Assert.Single(result.Problems);
        Assert.Equal("keys[1]", problem.Path);
        Assert.Equal(ActionIds.ListDown, feeder.Feed("ZZ").Action);
    }

    [Fact]
    public void GivenTheSameKeyTwice_WhenApplying_ThenTheSecondEntryIsReported()
    {
        var result = Apply(
            Entry("Normal", "Graph", "J", "list.down"),
            Entry("Normal", "Graph", "J", "list.up"));

        var feeder = new KeyFeeder(new KeymapEngine(result.Table));

        Assert.Equal("keys[1]", Assert.Single(result.Problems).Path);
        Assert.Equal(ActionIds.ListDown, feeder.Feed("J").Action);
    }

    [Fact]
    public void GivenUnbindingAChordAndBindingALongerOne_WhenApplying_ThenBothApplyWithoutConflict()
    {
        var result = Apply(
            Entry("Normal", "Lists", "gg", "none"),
            Entry("Normal", "Lists", "ggg", "list.top"));

        var feeder = new KeyFeeder(new KeymapEngine(result.Table));

        Assert.Empty(result.Problems);
        Assert.Equal(ActionIds.ListTop, feeder.Feed("ggg").Action);
    }

    [Theory]
    [InlineData("Bogus", "Graph", "J", "list.down", "unknown mode")]
    [InlineData("7", "Graph", "J", "list.down", "unknown mode")] // Parses as a number but is no defined mode
    [InlineData("Normal", "Nowhere", "J", "list.down", "unknown view")]
    [InlineData("Normal", "Graph", "<nope>", "list.down", "not a key sequence")]
    [InlineData("Normal", "Graph", "", "list.down", "not a key sequence")]
    [InlineData("Normal", "Graph", "J", "no.such.action", "unknown action")]
    [InlineData("Normal", "Graph", "J", "", "no action")]
    public void GivenAnInvalidEntry_WhenApplying_ThenItIsReportedAndTheDefaultsRemain(
        string mode,
        string view,
        string key,
        string action,
        string expected)
    {
        var result = Apply(Entry(mode, view, key, action));

        var problem = Assert.Single(result.Problems);
        Assert.Contains(expected, problem.Message, StringComparison.Ordinal);
        Assert.Equal(Defaults.Bindings.Count, result.Table.Bindings.Count);
    }

    [Fact]
    public void GivenANullEntryAmongValidOnes_WhenApplying_ThenOnlyTheNullOneIsReported()
    {
        var result = KeymapConfigurator.Apply(
            Defaults,
            [null, Entry("Normal", "Graph", "J", "list.down")]);

        var feeder = new KeyFeeder(new KeymapEngine(result.Table));

        Assert.Equal("keys[0]", Assert.Single(result.Problems).Path);
        Assert.Equal(ActionIds.ListDown, feeder.Feed("J").Action);
    }

    [Fact]
    public void GivenModeAndViewInOtherCase_WhenApplying_ThenTheyAreAccepted()
    {
        var result = Apply(Entry("normal", "graph", "J", "list.down"));

        Assert.Empty(result.Problems);
    }

    [Fact]
    public void GivenAcceptedEntries_WhenFindingConflicts_ThenThereAreNone()
    {
        var result = Apply(
            Entry("Normal", "Graph", "J", "list.down"),
            Entry("Normal", "Global", "q", "none"),
            Entry("Normal", "Graph", "g", "list.down"));

        Assert.Single(result.Problems);
        Assert.Empty(result.Table.FindConflicts());
    }

    [Fact]
    public void GivenFiftyValidEntries_WhenApplying_ThenAllApplyWithoutConflicts()
    {
        var entries = Enumerable.Range(0, 50)
            .Select(index => Entry("Normal", "Graph", $"<space>Z{(char)('a' + (index % 26))}{index:D2}", "list.down"));

        var result = Apply([.. entries]);

        Assert.Empty(result.Problems);
        Assert.Empty(result.Table.FindConflicts());
    }

    [Fact]
    public void GivenARefusedEntryThenAValidOneOnTheSameKey_WhenApplying_ThenTheSecondIsNotCalledADuplicate()
    {
        var result = Apply(
            Entry("Normal", "Graph", "g", "list.down"),
            Entry("Normal", "Graph", "g", "list.up"),
            Entry("Normal", "Graph", "J", "list.down"));

        Assert.Equal(["keys[0]", "keys[1]"], result.Problems.Select(problem => problem.Path));
        Assert.DoesNotContain("earlier entry", result.Problems[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAParentViewKeyThatExtendsAChildViewKey_WhenApplying_ThenItIsRefused()
    {
        var result = Apply(Entry("Normal", "Graph", "Z", "list.up"), Entry("Normal", "Lists", "ZZ", "list.down"));

        Assert.Equal("keys[1]", Assert.Single(result.Problems).Path);
    }

    [Fact]
    public void GivenAParentViewKeyThatIsAPrefixOfAChildViewChord_WhenApplying_ThenItIsRefused()
    {
        var result = Apply(Entry("Normal", "Graph", "ZZ", "list.down"), Entry("Normal", "Lists", "Z", "list.up"));

        Assert.Equal("keys[1]", Assert.Single(result.Problems).Path);
    }

    private static KeymapBuildResult Apply(params ConfigKeyEntry[] entries)
    {
        return KeymapConfigurator.Apply(Defaults, entries);
    }

    private static ConfigKeyEntry Entry(string mode, string view, string key, string action)
    {
        return new ConfigKeyEntry { Mode = mode, View = view, Key = key, Action = action };
    }
}
