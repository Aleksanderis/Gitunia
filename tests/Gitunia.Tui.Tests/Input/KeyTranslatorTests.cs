using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.Input;
using Terminal.Gui.Input;
using Xunit;

namespace Gitunia.Tui.Tests.Input;

public class KeyTranslatorTests
{
    private static KeyEvent Translate(Key key)
    {
        Assert.True(KeyTranslator.TryTranslate(key, out var keyEvent));
        return keyEvent;
    }

    [Fact]
    public void GivenLowercaseLetter_WhenTranslating_ThenTypedTextWithoutModifiers()
    {
        Assert.Equal(new KeyEvent("j"), Translate(new Key('j')));
    }

    [Fact]
    public void GivenUppercaseLetter_WhenTranslating_ThenShiftIsFoldedIntoText()
    {
        Assert.Equal(new KeyEvent("G"), Translate(new Key('G')).Normalize());
    }

    [Fact]
    public void GivenCtrlLetter_WhenTranslating_ThenCtrlModifierAndLowercaseName()
    {
        Assert.Equal(new KeyEvent("d", KeyModifiers.Ctrl), Translate(Key.D.WithCtrl).Normalize());
    }

    [Fact]
    public void GivenAltDigit_WhenTranslating_ThenAltModifier()
    {
        Assert.Equal(new KeyEvent("1", KeyModifiers.Alt), Translate(Key.D1.WithAlt));
    }

    [Fact]
    public void GivenSpaceAndSymbols_WhenTranslating_ThenTypedText()
    {
        Assert.Equal(new KeyEvent(" "), Translate(Key.Space));
        Assert.Equal(new KeyEvent("@"), Translate(new Key('@')));
        Assert.Equal(new KeyEvent("/"), Translate(new Key('/')));
    }

    [Fact]
    public void GivenNamedKeys_WhenTranslating_ThenCanonicalNames()
    {
        Assert.Equal(new KeyEvent(KeyNames.Enter), Translate(Key.Enter));
        Assert.Equal(new KeyEvent(KeyNames.Escape), Translate(Key.Esc));
        Assert.Equal(new KeyEvent(KeyNames.Tab), Translate(Key.Tab));
        Assert.Equal(new KeyEvent(KeyNames.Tab, KeyModifiers.Shift), Translate(Key.Tab.WithShift));
        Assert.Equal(new KeyEvent(KeyNames.Down), Translate(Key.CursorDown));
        Assert.Equal(new KeyEvent(KeyNames.PageUp), Translate(Key.PageUp));
    }

    [Fact]
    public void GivenFunctionKeyWithoutName_WhenTranslating_ThenItIsNotForTheKeymap()
    {
        Assert.False(KeyTranslator.TryTranslate(Key.Empty, out _));
    }

    [Theory]
    [InlineData("j", "list.down", null)]
    [InlineData("5j", "list.down", 5)]
    [InlineData("gg", "list.top", null)]
    [InlineData("G", "list.bottom", null)]
    [InlineData("@", "graph.jump-to-head", null)]
    [InlineData("q", "navigate.back", null)]
    public void GivenTerminalGuiKeys_WhenFedThroughTheKeymap_ThenGraphActionsFire(string typed, string expectedAction, int? expectedCount)
    {
        var engine = new KeymapEngine(DefaultKeymap.Create());
        var state = KeymapState.Initial;
        KeyResult? last = null;
        foreach (var character in typed)
        {
            Assert.True(KeyTranslator.TryTranslate(new Key(character), out var keyEvent));
            last = engine.Process(state, keyEvent, DefaultKeymap.GraphView);
            state = last.State;
        }

        Assert.Equal(KeyOutcome.Action, last!.Outcome);
        Assert.Equal(expectedAction, last.Action!.Value.Value);
        Assert.Equal(expectedCount, last.Count);
    }

    [Fact]
    public void GivenCtrlD_WhenFedThroughTheKeymap_ThenHalfPageDown()
    {
        var engine = new KeymapEngine(DefaultKeymap.Create());

        var result = engine.Process(KeymapState.Initial, Translate(Key.D.WithCtrl), DefaultKeymap.GraphView);

        Assert.Equal(ActionIds.ListHalfPageDown, result.Action);
    }

    [Fact]
    public void GivenCtrlSix_WhenTranslating_ThenItIsCtrlCaretTheTerminalSends()
    {
        // 0x1E arrives as Ctrl+6 (checked against Terminal.Gui 2.5.0 under tmux), so the previous-tab key needs this mapping.
        Assert.Equal(new KeyEvent("^", KeyModifiers.Ctrl), Translate(Key.D6.WithCtrl));
    }

    [Fact]
    public void GivenCtrlEnter_WhenTranslating_ThenItIsCtrlJNotEnter()
    {
        // 0x0A arrives as Ctrl+Enter, distinct from Enter (0x0D), so Ctrl-j can move the picker highlight.
        Assert.Equal(new KeyEvent("j", KeyModifiers.Ctrl), Translate(Key.Enter.WithCtrl));
        Assert.Equal(new KeyEvent(KeyNames.Enter), Translate(Key.Enter));
    }

    [Fact]
    public void GivenPickerInInsertMode_WhenPressingCtrlL_ThenItAccepts()
    {
        var engine = new KeymapEngine(DefaultKeymap.Create());
        var state = KeymapState.Initial.WithMode(KeyMode.Insert);

        var result = engine.Process(state, Translate(Key.L.WithCtrl), DefaultKeymap.PickerView);

        Assert.Equal(new KeyEvent("l", KeyModifiers.Ctrl), Translate(Key.L.WithCtrl).Normalize());
        Assert.Equal(ActionIds.PickerAccept, result.Action);
    }

    [Fact]
    public void GivenPickerInInsertMode_WhenPressingCtrlJAndCtrlCaret_ThenHighlightMovesAndPreviousTabFires()
    {
        var engine = new KeymapEngine(DefaultKeymap.Create());
        var state = KeymapState.Initial.WithMode(KeyMode.Insert);

        var down = engine.Process(state, Translate(Key.Enter.WithCtrl), DefaultKeymap.PickerView);
        var previousTab = engine.Process(state, Translate(Key.D6.WithCtrl), DefaultKeymap.PickerView);

        Assert.Equal(ActionIds.PickerNext, down.Action);
        Assert.Equal(ActionIds.TabLastActive, previousTab.Action);
    }
}
