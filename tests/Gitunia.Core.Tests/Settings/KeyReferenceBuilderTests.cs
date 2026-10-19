using Gitunia.Core.Config;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Settings;
using Xunit;

namespace Gitunia.Core.Tests.Settings;

public class KeyReferenceBuilderTests
{
    [Fact]
    public void GivenTheDefaultKeymap_WhenBuilding_ThenEveryNormalAndVisualBindingIsListedWithItsActionAndTitle()
    {
        var table = DefaultKeymap.Create();

        var entries = KeyReferenceBuilder.Build(table);

        foreach (var binding in table.Bindings.Where(candidate => candidate.Mode is KeyMode.Normal or KeyMode.Visual))
        {
            var section = binding.Mode == KeyMode.Normal ? binding.View : $"{binding.View} ({binding.Mode})";
            var keys = KeyFormatter.Format(binding.Sequence);
            var found = entries.Any(entry => entry.Section == section
                && entry.ActionId == binding.Action.Value
                && entry.Title == ActionCatalog.Describe(binding.Action)
                && entry.Keys.Split(" / ").Contains(keys));
            Assert.True(found, $"{section} {keys} {binding.Action} is missing from the key reference");
        }
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenBuilding_ThenNoListedKeyIsOutsideTheKeymap()
    {
        var table = DefaultKeymap.Create();
        var bound = table.Bindings
            .Where(binding => binding.Mode is KeyMode.Normal or KeyMode.Visual)
            .Select(binding => $"{Heading(binding)}|{KeyFormatter.Format(binding.Sequence)}")
            .Order()
            .ToList();

        var entries = KeyReferenceBuilder.Build(table);

        var listed = entries
            .SelectMany(entry => entry.Keys.Split(" / ").Select(keys => $"{entry.Section}|{keys}"))
            .Order()
            .ToList();
        Assert.Equal(bound, listed);
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenParsingEveryListedKey_ThenItIsTheBindingsOwnSequence()
    {
        var table = DefaultKeymap.Create();

        foreach (var binding in table.Bindings.Where(candidate => candidate.Mode is KeyMode.Normal or KeyMode.Visual))
        {
            var text = KeyFormatter.Format(binding.Sequence);
            Assert.True(KeySequenceParser.TryParse(text, out var parsed), $"{text} does not parse");
            Assert.Equal(binding.Sequence, parsed);
        }
    }

    private static string Heading(KeyBinding binding) =>
        binding.Mode == KeyMode.Normal ? binding.View : $"{binding.View} ({binding.Mode})";

    [Fact]
    public void GivenAUserRebind_WhenBuilding_ThenTheReferenceShowsTheNewKeyAndNotTheRemovedOne()
    {
        var result = KeymapConfigurator.Apply(
            DefaultKeymap.Create(),
            [
                new ConfigKeyEntry { View = "Graph", Key = "J", Action = "list.down" },
                new ConfigKeyEntry { View = "Lists", Key = "gg", Action = "none" },
            ]);

        var entries = KeyReferenceBuilder.Build(result.Table);

        Assert.Contains(entries, entry => entry.Section == "Graph" && entry.ActionId == "list.down");
        Assert.DoesNotContain(
            entries,
            entry => entry.Section == "Lists" && entry.Keys.Split(" / ").Contains("gg"));
    }

    [Fact]
    public void GivenAGraphBinding_WhenBuilding_ThenTheSnippetIsAKeysEntryThatNamesViewKeyAndAction()
    {
        var table = new KeyBindingTable(
            [
                new KeyBinding(KeyMode.Normal, "Graph", KeySequenceParser.Parse("J"), new ActionId("list.down")),
                new KeyBinding(KeyMode.Visual, "Lists", KeySequenceParser.Parse("o"), new ActionId("list.up")),
                new KeyBinding(KeyMode.Normal, "Global", KeySequenceParser.Parse("<space>,"), new ActionId("a.b")),
            ]);

        var entries = KeyReferenceBuilder.Build(table);

        Assert.Equal("Global", entries[0].Section);
        Assert.Equal("{ \"key\": \"<space>,\", \"action\": \"a.b\" }", entries[0].Snippet);
        Assert.Equal("{ \"view\": \"Graph\", \"key\": \"J\", \"action\": \"list.down\" }", entries[1].Snippet);
        Assert.Equal("Lists (Visual)", entries[2].Section);
        Assert.Equal(
            "{ \"mode\": \"Visual\", \"view\": \"Lists\", \"key\": \"o\", \"action\": \"list.up\" }",
            entries[2].Snippet);
    }

    [Fact]
    public void GivenSeveralKeysForOneAction_WhenBuilding_ThenTheyShareOneLine()
    {
        var table = DefaultKeymap.Create();

        var entries = KeyReferenceBuilder.Build(table);

        Assert.Contains(entries, entry => entry.Section == "Lists" && entry.Keys == "j / Down");
    }
}
