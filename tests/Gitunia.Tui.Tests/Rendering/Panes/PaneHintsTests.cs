using Gitunia.Core.Keymap;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Xunit;

namespace Gitunia.Tui.Tests.Rendering.Panes;

public class PaneHintsTests
{
    public static TheoryData<PaneKind> Panes() => new(Enum.GetValues<PaneKind>());

    [Theory]
    [InlineData(PaneKind.Graph, "gg/G")]
    [InlineData(PaneKind.Sidebar, "/")]
    [InlineData(PaneKind.Detail, "l")]
    [InlineData(PaneKind.Diff, "w")]
    public void GivenAPane_WhenAskingForHints_ThenTheyMentionThatPanesKeys(PaneKind pane, string expectedKeys)
    {
        Assert.Contains(PaneHints.For(pane), hint => hint.Keys == expectedKeys);
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPane_WhenAskingForHints_ThenEveryHintHasKeysAndADescription(PaneKind pane)
    {
        Assert.NotEmpty(PaneHints.For(pane));
        Assert.All(PaneHints.For(pane), hint =>
        {
            Assert.False(string.IsNullOrWhiteSpace(hint.Keys), "A hint has no keys.");
            Assert.False(string.IsNullOrWhiteSpace(hint.Description), $"The hint {hint.Keys} has no description.");
        });
    }

    [Fact]
    public void GivenThePickerHints_WhenListing_ThenEveryHintHasADescription()
    {
        Assert.All(PaneHints.ForPicker(), hint => Assert.False(string.IsNullOrWhiteSpace(hint.Description), $"The hint {hint.Keys} has no description."));
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPane_WhenAskingForHints_ThenNoKeyUsesTheCaretNotation(PaneKind pane)
    {
        Assert.All(PaneHints.For(pane), hint => Assert.DoesNotContain('^', hint.Keys));
    }

    [Fact]
    public void GivenAFocusedDiffPane_WhenBuildingTheStatusBar_ThenItShowsKeysNextToTheirDescriptions()
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty, Hints: PaneHints.For(PaneKind.Diff));

        var text = StatusText(model);

        Assert.Contains("]c/[c hunk", text, StringComparison.Ordinal);
        Assert.Contains("w wrap", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoHints_WhenBuildingTheStatusBar_ThenItShowsTheGraphHintsWithTheSpelledOutNotation()
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, string.Empty);

        var text = StatusText(model);

        Assert.Contains("Ctrl-d/u page", text, StringComparison.Ordinal);
        Assert.DoesNotContain('^', text);
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPaneAt80Columns_WhenBuildingTheStatusBar_ThenHelpAndCommandsStayVisible(PaneKind pane)
    {
        var model = new ScreenModel("repo", null, 1, 1, "NORMAL", string.Empty, 1, 1, false, "Could not read the stashes: boom", Hints: PaneHints.For(pane));

        var text = string.Concat(ChromeBuilder.Status(model, 80, GraphGlyphSet.Unicode).Segments.Select(segment => segment.Text));

        Assert.Contains("? help  : commands", text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Panes))]
    public void GivenAPane_WhenCheckingItsHints_ThenEveryKeyIsBoundInTheDefaultKeymapForThatPane(PaneKind pane)
    {
        var view = ViewFor(pane);

        AssertKeysAreBound(PaneHints.For(pane), KeyMode.Normal, view);
    }

    [Fact]
    public void GivenThePickerHints_WhenCheckingThem_ThenEveryKeyIsBoundInThePickerView()
    {
        AssertKeysAreBound(PaneHints.ForPicker(), KeyMode.Insert, DefaultKeymap.PickerView);
    }

    private static string ViewFor(PaneKind pane) => pane switch
    {
        PaneKind.Sidebar => DefaultKeymap.SidebarView,
        PaneKind.Detail => DefaultKeymap.DetailView,
        PaneKind.Diff => DefaultKeymap.DiffView,
        _ => DefaultKeymap.GraphView,
    };

    private static void AssertKeysAreBound(IReadOnlyList<KeyHint> hints, KeyMode mode, string view)
    {
        var bound = DefaultKeymap.Create().EffectiveBindings(mode, view).Select(binding => binding.Sequence).ToHashSet();
        foreach (var hint in hints)
        {
            foreach (var keys in ExpandAlternatives(hint.Keys))
            {
                Assert.True(bound.Contains(KeySequenceParser.Parse(keys)), $"The hint '{hint.Keys} {hint.Description}' shows '{keys}', which is not bound in the {view} view ({mode} mode).");
            }
        }
    }

    // A hint such as "Ctrl-d/u" or "gg/G" lists alternatives; a bare character after "Ctrl-" reuses it (other modifiers are not supported yet). The test checks the key is bound, not that it triggers the action the description names.
    private static IEnumerable<string> ExpandAlternatives(string keys)
    {
        const string ctrlPrefix = "Ctrl-";
        const string slashKey = "/";
        var alternatives = keys == slashKey ? [keys] : keys.Split(slashKey[0]);
        for (var index = 0; index < alternatives.Length; index++)
        {
            var alternative = alternatives[index];
            if (index > 0 && alternative.Length == 1 && alternatives[0].StartsWith(ctrlPrefix, StringComparison.Ordinal))
            {
                yield return $"{ctrlPrefix}{alternative}";
            }
            else
            {
                yield return alternative;
            }
        }
    }

    private static string StatusText(ScreenModel model) =>
        string.Concat(ChromeBuilder.Status(model, 160, GraphGlyphSet.Unicode).Segments.Select(segment => segment.Text));
}
