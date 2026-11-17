using Gitunia.Core.Blame;
using Gitunia.Core.Config.Models;
using Gitunia.Core.Hints;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Text;
using Gitunia.Core.Visual;
using Gitunia.Core.Workspaces.Tabs;
using Xunit;

namespace Gitunia.Core.Tests.Hints;

// The hint sets that depend on the state of their screen are one declaration for the status bar and the footer: each key
// of each variant is bound to the action the hint names, in the mode and view the screen has at that moment.
public class HintSetScreensTests
{
    private static readonly TabId Tab = new(1);

    private static readonly string Home = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "home", "user");

    private static readonly SettingsEnvironment Environment = new(Home, _ => true);

    [Fact]
    public void GivenEveryRowOfTheSettings_WhenReadingTheHints_ThenEachKeyIsBoundToItsAction()
    {
        var state = Open().WithLayout(collapsed: true) with { Column = SettingsColumn.Settings };
        var seen = new HashSet<string>();

        for (var index = 0; index < state.Rows.Count; index++)
        {
            var hints = HintSet.ForSettings(state.MoveTo(index));

            AssertBound(hints, KeyMode.Normal, DefaultKeymap.SettingsView);
            seen.Add(string.Join('|', hints.Select(hint => hint.Keys)));
        }

        Assert.True(seen.Count > 1, "The rows of the settings should reach more than one hint variant.");
    }

    [Fact]
    public void GivenTheSettingsFilterIsBeingTyped_WhenReadingTheHints_ThenTheyAreTheFilterHintsAndBound()
    {
        var typing = Open() with { IsEditingFilter = true };

        var hints = HintSet.ForSettings(typing);

        Assert.Equal(HintVerb.TypeToFilter, hints[0].Description);
        AssertBound(hints, KeyMode.Insert, DefaultKeymap.SettingsView);
    }

    [Fact]
    public void GivenAFilterIsKept_WhenReadingTheHints_ThenEscClearsItAndTheKeysAreBound()
    {
        var kept = Open().WithFilterText("theme");

        var hints = HintSet.ForSettings(kept);

        Assert.Contains(hints, hint => hint.Keys == "Esc" && hint.Description == HintVerb.Clear);
        AssertBound(hints, KeyMode.Normal, DefaultKeymap.SettingsView);
    }

    [Fact]
    public void GivenAFolderRemovalIsAsked_WhenReadingTheHints_ThenYesNoAndEscAreTheConfirmKeys()
    {
        var asking = Open() with { Removal = new PendingRemoval("repositories.roots", 0, "/b") };

        var hints = HintSet.ForSettings(asking);

        Assert.Equal(["y", "n", "Esc"], hints.Select(hint => hint.Keys));
        AssertBound(hints, KeyMode.Confirm, KeyBindingTable.GlobalView);
    }

    [Fact]
    public void GivenANumberIsBeingTyped_WhenReadingTheHints_ThenEnterSavesAndEscKeeps()
    {
        var typing = Open() with { Input = new SettingsInput(SettingsInputKind.Number, "x", 0, "3") };

        var hints = HintSet.ForSettings(typing);

        Assert.Equal([HintVerb.Save, HintVerb.Keep], hints.Select(hint => hint.Description));
        AssertBound(hints, KeyMode.Insert, DefaultKeymap.SettingsView);
    }

    [Fact]
    public void GivenAChoiceIsOpen_WhenReadingTheHints_ThenEnterKeepsAndEscCancelsInTheViewsOwnMode()
    {
        var choosing = Open() with { Choice = new ChoiceEdit("x") };

        var hints = HintSet.ForSettings(choosing);

        Assert.Contains(hints, hint => hint.Keys == "Esc" && hint.Description == HintVerb.Cancel);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void GivenAHelpScreen_WhenReadingTheHints_ThenTheLabelsComeFromTheVocabularyAndTheKeysAreBound(
        bool isFiltered,
        bool isEditing,
        bool hasTabs)
    {
        var hints = HintSet.ForHelp(isFiltered, isEditing, hasTabs, hasMenu: false, scrolls: true);

        Assert.Equal(isFiltered ? HintVerb.Clear : HintVerb.Close, hints[0].Description);
        Assert.Contains(hints, hint => hint.Description == "list keys scroll" && hint.Actions.IsEmpty);
        Assert.Equal(hasTabs, hints.Any(hint => hint.Keys == "Tab"));
        AssertBound(hints.Where(hint => hint.Keys != "Enter" && hint.Keys != "Tab"), KeyMode.Normal, DefaultKeymap.HelpView);
    }

    [Fact]
    public void GivenTheReflog_WhenItsSearchOrPickerIsOpen_ThenTheHintsAreTheVariantOfThatState()
    {
        var list = new ReflogState(Tab, "HEAD", "HEAD", []);

        Assert.Equal([HintVerb.Cancel], HintSet.ForReflog(list.OpenSearch()).Select(hint => hint.Description));
        Assert.Equal([HintVerb.Clear], HintSet.ForReflog(list.OpenSearch() with { IsEditingSearch = false, SearchText = "x" })
            .Select(hint => hint.Description));
        Assert.Equal(["Enter", "Esc"], HintSet.ForReflog(list with { Picker = new ReflogRefPicker([]) })
            .Select(hint => hint.Keys));
        AssertBound(HintSet.ForReflog(list), KeyMode.Normal, DefaultKeymap.ReflogView);
    }

    [Fact]
    public void GivenTheBlameView_WhenItIsSearchingSelectingOrSteppedBack_ThenTheHintsAreTheVariantOfThatState()
    {
        var plain = new BlameState(Tab, "a.txt", "a.txt", LineCount: 3);
        var selected = plain with { Selection = new BlameSelection(VisualKind.Lines, 0) };

        Assert.Equal(HintVerb.Close, HintSet.ForBlame(plain)[^1].Description);
        Assert.Equal(["y", "Esc"], HintSet.ForBlame(selected).Select(hint => hint.Keys));
        Assert.Equal([HintVerb.Cancel], HintSet.ForBlame(plain with { SearchText = "x", IsEditingSearch = true })
            .Select(hint => hint.Description));
        Assert.Equal(HintVerb.Back, HintSet.ForBlame(plain with { Back = [new BlameStep("a.txt", "a", 0)] })[^1].Description);
        AssertBound(HintSet.ForBlame(plain), KeyMode.Normal, DefaultKeymap.BlameView);
        AssertBound(HintSet.ForBlame(selected), KeyMode.Visual, DefaultKeymap.BlameView);
    }

    [Fact]
    public void GivenTheMainlineQuestion_WhenReadingTheHints_ThenEscCancelsAndIsBound()
    {
        var hints = HintSet.ForMainline();

        Assert.Equal(HintVerb.CancelOrClickOutside, hints[0].Description);
        AssertBound(hints, KeyMode.Insert, KeyBindingTable.GlobalView);
    }

    private static SettingsState Open() =>
        SettingsState.Create(
            [.. SettingsRegistry.All],
            SettingsValueReader.Read(
                new ConfigSnapshot(true, "{}", "hash"),
                Home,
                Environment.DirectoryExists));

    private static void AssertBound(IEnumerable<KeyHint> hints, KeyMode mode, string view)
    {
        var bindings = DefaultKeymap.Create().EffectiveBindings(mode, view);
        foreach (var hint in hints.Where(hint => !hint.Actions.IsDefaultOrEmpty))
        {
            var alternatives = hint.Keys == "/" ? ["/"] : hint.Keys.Split('/');
            Assert.Equal(alternatives.Length, hint.Actions.Length);
            for (var index = 0; index < alternatives.Length; index++)
            {
                var keys = Expand(alternatives, index);
                var binding = bindings.FirstOrDefault(
                    candidate => candidate.Sequence.Equals(KeySequenceParser.Parse(keys)));

                Assert.True(binding is not null, $"'{keys}' of '{hint.Keys} {hint.Description}' is not bound in {view}/{mode}.");
                Assert.True(
                    binding.Action == hint.Actions[index],
                    $"'{keys}' of '{hint.Keys} {hint.Description}' is {binding.Action} in {view}/{mode}, not {hint.Actions[index]}.");
            }
        }
    }

    // A bare character after the first alternative reuses its prefix ("Ctrl-j/k", "<space>1/2").
    private static string Expand(string[] alternatives, int index)
    {
        var first = alternatives[0];
        foreach (var prefix in new[] { "Ctrl-", "Alt-", "<space>" })
        {
            if (index > 0 && alternatives[index].Length == 1 && first.StartsWith(prefix, StringComparison.Ordinal))
            {
                return $"{prefix}{alternatives[index]}";
            }
        }

        return alternatives[index];
    }
}
