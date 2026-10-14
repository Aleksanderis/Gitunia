using Gitunia.Core.Keymap;
using Xunit;

namespace Gitunia.Core.Tests.Keymap;

public class KeymapDocumentationTests
{
    [Fact]
    public void GivenTheUxKeymapTable_WhenReadingIt_ThenItHasBindings()
    {
        Assert.True(UxKeymapReader.ReadBindings().Count > 30);
    }

    [Fact]
    public void GivenTheUxKeymapTable_WhenCheckedForConflicts_ThenThereAreNone()
    {
        var table = new KeyBindingTable(UxKeymapReader.ReadBindings(), DefaultKeymap.ViewParents);

        var conflicts = table.FindConflicts();

        Assert.True(conflicts.Count == 0, string.Join(Environment.NewLine, conflicts));
    }

    [Fact]
    public void GivenTheDefaultKeymap_WhenCheckedForConflicts_ThenThereAreNone()
    {
        var conflicts = DefaultKeymap.Create().FindConflicts();

        Assert.True(conflicts.Count == 0, string.Join(Environment.NewLine, conflicts));
    }

    [Fact]
    public void GivenEveryDocumentedBinding_WhenLookingItUp_ThenItIsARegisteredDefault()
    {
        var defaults = DefaultKeymap.CreateBindings();

        var missing = UxKeymapReader.ReadBindings()
            .Where(documented => !defaults.Any(registered => registered.Mode == documented.Mode
                && registered.View == documented.View
                && registered.Sequence.Equals(documented.Sequence)))
            .Select(documented => $"{documented.View}: {documented.Sequence} ({documented.Action})")
            .ToList();

        Assert.True(missing.Count == 0, $"Documented but not registered:{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    [Fact]
    public void GivenEveryNormalModeDefault_WhenLookingItUp_ThenItIsDocumented()
    {
        var documented = UxKeymapReader.ReadBindings();

        var undocumented = DefaultKeymap.CreateBindings()
            .Where(registered => registered.Mode == KeyMode.Normal)
            .Where(registered => !documented.Any(row => row.Mode == KeyMode.Normal && row.View == registered.View && row.Sequence.Equals(registered.Sequence)))
            .Select(registered => $"{registered.View}: {registered.Sequence}")
            .ToList();

        Assert.True(undocumented.Count == 0, $"Registered Normal bindings missing from docs/ux.md:{Environment.NewLine}{string.Join(Environment.NewLine, undocumented)}");
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
}
