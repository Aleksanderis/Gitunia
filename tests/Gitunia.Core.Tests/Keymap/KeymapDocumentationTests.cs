using System.Reflection;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.KeymapDoc;
using Xunit;

namespace Gitunia.Core.Tests.Keymap;

public class KeymapDocumentationTests
{
    private static readonly HashSet<string> UnboundActions = [.. ActionCatalog.NoDefaultKey.Select(action => action.Value)];

    [Fact]
    public void GivenTheKeymapFile_WhenRenderingTheReference_ThenTheCommittedDocIsCurrent()
    {
        var expected = KeymapReference.Render(ReadRows());

        var committed = File.ReadAllText(RepoDocs.FindUxDocument("keymap.md")).ReplaceLineEndings("\n");

        Assert.True(
            committed == expected,
            "docs/keymap.md is stale: run scripts/gen-keymap-doc.sh and commit the result.");
    }

    [Fact]
    public void GivenTheKeymapFile_WhenReadingIt_ThenItHasRows()
    {
        Assert.True(ReadRows().Count > 100);
    }

    [Fact]
    public void GivenEveryActionInTheKeymapFile_WhenLookingItUpInCode_ThenTheIdExists()
    {
        var known = KnownActionIds();

        var unknown = ReadRows()
            .Select(row => row.Action.Value)
            .Where(id => !known.Contains(id) && !id.StartsWith("tab.go-to.", StringComparison.Ordinal))
            .Distinct()
            .ToList();

        Assert.True(unknown.Count == 0, $"Ids in keymap.default.json missing from ActionIds:{Environment.NewLine}{string.Join(Environment.NewLine, unknown)}");
    }

    [Fact]
    public void GivenEveryActionIdInCode_WhenLookingItUpInTheKeymapFile_ThenItIsBoundOrMarkedUnbound()
    {
        var bound = ReadRows().Select(row => row.Action.Value).ToHashSet();

        var neither = KnownActionIds()
            .Where(id => !bound.Contains(id) && !UnboundActions.Contains(id))
            .Order()
            .ToList();

        Assert.True(neither.Count == 0, $"Actions with no key and not marked unbound:{Environment.NewLine}{string.Join(Environment.NewLine, neither)}");
    }

    [Fact]
    public void GivenTheUnboundList_WhenLookingItUpInTheKeymapFile_ThenNoMarkedActionIsBound()
    {
        var bound = ReadRows().Select(row => row.Action.Value).ToHashSet();

        Assert.False(UnboundActions.Overlaps(bound));
    }

    [Fact]
    public void GivenTheKeymapFile_WhenCheckingEachView_ThenNoKeyIsBoundTwiceInAMode()
    {
        var duplicates = ReadRows()
            .SelectMany(row => row.Modes.SelectMany(mode => row.Keys.Select(key => (row.View, Mode: mode, Key: key, row.Action))))
            .GroupBy(entry => (entry.View, entry.Mode, entry.Key))
            .Where(group => group.Select(entry => entry.Action).Distinct().Count() > 1)
            .Select(group => $"{group.Key.View} {group.Key.Mode} {group.Key.Key}: {string.Join(", ", group.Select(entry => entry.Action.Value))}")
            .ToList();

        Assert.True(duplicates.Count == 0, string.Join(Environment.NewLine, duplicates));
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenCheckedForConflicts_ThenThereAreNone()
    {
        var conflicts = DefaultKeymap.Create().FindConflicts();

        Assert.True(conflicts.Count == 0, string.Join(Environment.NewLine, conflicts));
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenLookingForAltEnter_ThenNothingIsBoundToIt()
    {
        var bound = DefaultKeymap.CreateBindings()
            .Where(binding => binding.Sequence.ToString().Contains("Alt-Enter", StringComparison.Ordinal));

        Assert.Empty(bound);
    }

    [Fact]
    public void GivenTwoBindingsOnTheSameKeyInOneView_WhenCheckingConflicts_ThenADuplicateIsReported()
    {
        var table = new KeyBindingTable(
        [
            new KeyBinding(KeyMode.Normal, "Graph", KeySequenceParser.Parse("x"), new ActionId("one")),
            new KeyBinding(KeyMode.Normal, "Graph", KeySequenceParser.Parse("x"), new ActionId("two")),
            new KeyBinding(KeyMode.Insert, "Graph", KeySequenceParser.Parse("x"), new ActionId("three")),
        ]);

        var conflict = Assert.Single(table.FindConflicts());

        Assert.Equal(BindingConflictKind.Duplicate, conflict.Kind);
    }

    private static List<KeymapRow> ReadRows() =>
        [.. KeymapRowReader.Read(File.ReadAllBytes(RepoDocs.Find("src", "Gitunia.Core", "Keymap", "keymap.default.json")))];

    private static HashSet<string> KnownActionIds() =>
        [.. typeof(ActionIds).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(ActionId))
            .Select(property => ((ActionId)property.GetValue(null)!).Value)];
}
