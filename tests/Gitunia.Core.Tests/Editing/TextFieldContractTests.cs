using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Picker;
using Gitunia.Core.Prompts;
using Gitunia.Core.Shell;
using Gitunia.Core.Tests.Picker;
using Xunit;

namespace Gitunia.Core.Tests.Editing;

/// <summary>
/// The same scenario against the text fields whose state is reduced in Core and that edit through
/// <see cref="PromptTextEditor"/>: type, Left, Ctrl-w, Backspace on a grapheme, paste. A new field adds itself to
/// <see cref="Surfaces"/>.
/// </summary>
[Trait("Os", "Independent")]
public class TextFieldContractTests
{
    private const string Grapheme = "é";

    public static TheoryData<string> Surfaces => ["text field", "palette", "prompt", "help filter", "picker"];

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenAField_WhenTypingAndMovingTheCaret_ThenTextAndCaretFollow(string name)
    {
        var field = Typed(name, "foo bar");

        field.Press(new KeyEvent(KeyNames.Left));

        Assert.Equal("foo bar", field.Text);
        Assert.Equal(6, field.Caret);
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenTheCaretInsideTheLastWord_WhenCtrlWIsPressed_ThenTheWordBeforeTheCaretGoes(string name)
    {
        var field = Typed(name, "foo bar");
        field.Press(new KeyEvent(KeyNames.Left));

        field.Press(new KeyEvent("w", KeyModifiers.Ctrl));

        Assert.Equal("foo r", field.Text);
        Assert.Equal(4, field.Caret);
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenAGraphemeOfTwoChars_WhenBackspaceIsPressed_ThenTheWholeGraphemeGoes(string name)
    {
        var field = Typed(name, $"a{Grapheme}");

        field.Press(new KeyEvent(KeyNames.Backspace));

        Assert.Equal("a", field.Text);
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenTheCaretInTheMiddle_WhenTextIsPasted_ThenItGoesInAtTheCaretAndTheCaretFollows(string name)
    {
        var field = Typed(name, "ac");
        field.Press(new KeyEvent(KeyNames.Left));

        field.Press(new KeyEvent("XY"));

        Assert.Equal("aXYc", field.Text);
        Assert.Equal(3, field.Caret);
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenAField_WhenCtrlUIsPressed_ThenTheTextBeforeTheCaretGoes(string name)
    {
        var field = Typed(name, "foo bar");
        field.Press(new KeyEvent(KeyNames.Left));

        field.Press(new KeyEvent("u", KeyModifiers.Ctrl));

        Assert.Equal("r", field.Text);
        Assert.Equal(0, field.Caret);
    }

    private static Surface Typed(string name, string text)
    {
        var surface = Surface.Create(name);
        surface.Press(new KeyEvent(text));

        return surface;
    }

    // One field of the app behind the same four questions.
    private abstract class Surface
    {
        public abstract string Text { get; }

        public abstract int Caret { get; }

        public static Surface Create(string name) => name switch
        {
            "text field" => new TextFieldSurface(),
            "palette" => new PaletteSurface(),
            "prompt" => new PromptSurface(),
            "help filter" => new HelpFilterSurface(),
            "picker" => new PickerSurface(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown field"),
        };

        public abstract void Press(KeyEvent key);

        protected static int Clamp(string text, int caret) => PromptCaret.Clamp(text, caret);
    }

    private sealed class TextFieldSurface : Surface
    {
        private TextField _field = new();

        public override string Text => _field.Text;

        public override int Caret => _field.CaretColumn;

        public override void Press(KeyEvent key) => _field = _field.Apply(key) ?? _field;
    }

    private sealed class PaletteSurface : Surface
    {
        private PaletteState _palette = new([]);

        public override string Text => _palette.Query;

        public override int Caret => _palette.Field.CaretColumn;

        public override void Press(KeyEvent key) => _palette = _palette.Edit(key);
    }

    private sealed class PromptSurface : Surface
    {
        private ShellState _state = PromptReducer.Reduce(
            ShellState.Empty,
            new PromptRequestedCommand(new PromptRequest("Rename", "What for", "save", new NoResult()))).State;

        public override string Text => _state.Prompt!.Text;

        public override int Caret => Clamp(Text, _state.Prompt!.Field.Caret);

        public override void Press(KeyEvent key) =>
            _state = PromptReducer.Reduce(_state, new PromptKeyPressedCommand(key)).State;
    }

    private sealed class HelpFilterSurface : Surface
    {
        private OverlayState _state = OverlayState.OpenHelp("graph", KeyMode.Normal).OpenHelpFilter();

        public override string Text => _state.HelpFilter;

        public override int Caret => Clamp(Text, _state.HelpFilterField.Caret);

        public override void Press(KeyEvent key) => _state = _state.EditHelpFilter(key);
    }

    private sealed class PickerSurface : Surface
    {
        private PickerState _state = PickerReducer.Open(PickerFixtures.Context());

        public override string Text => _state.Query;

        public override int Caret => Clamp(Text, _state.Field.Caret);

        public override void Press(KeyEvent key) =>
            _state = PickerReducer.Reduce(_state, new EditQueryCommand(key), PickerFixtures.Context());
    }

    private sealed class NoResult : IPromptResult
    {
        public AppCommand Answered(PromptAnswer answer) => throw new NotSupportedException();
    }
}
