using System.Collections.Immutable;
using Gitunia.Core.Editing;
using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Shell;
using Gitunia.Core.Status;

namespace Gitunia.Core.Prompts;

/// <summary>
/// The one prompt model as pure transitions <c>(ShellState, PromptCommand) -> ShellUpdate</c>: a prompt opens, takes
/// typed keys (text editing, picker moves and filtering), is accepted (its result command is reduced right after, in
/// <see cref="ShellUpdate.Next"/>) or cancelled. What a prompt means is the feature's result command; nothing here knows
/// a feature.
/// </summary>
public static class PromptReducer
{
    /// <summary>Applies a command to the prompt in the shell state.</summary>
    public static ShellUpdate Reduce(ShellState state, PromptCommand command) => command switch
    {
        PromptAcceptedCommand => Accept(state),
        PromptCancelledCommand => Cancel(state),
        PromptKeyPressedCommand pressed => KeyPressed(state, pressed.Key),
        PromptRequestedCommand requested => Open(state, requested.Request),
        _ => throw new UnhandledCommandException(command),
    };

    private static ShellUpdate Open(ShellState state, PromptRequest request)
    {
        if (state.Prompt is not null)
        {
            return new ShellUpdate(state, []);
        }

        PromptPicker? picker = null;
        if (request.Choices is { } choices)
        {
            picker = PromptPicker.Of(choices, request.NoMatch, request.InitialText);
        }

        var prompt = new OpenPrompt(
            request.Title,
            request.Context,
            request.AcceptLabel,
            request.InitialText,
            picker,
            request.OnAccepted,
            request.Owner,
            state.ReturnMode,
            request.ListOnly,
            request.KeyChoices);
        var opened = (state with { Prompt = prompt, Confirm = null, Output = null }).WithMode(KeyMode.Insert);

        return new ShellUpdate(opened, []);
    }

    private static ShellUpdate KeyPressed(ShellState state, KeyEvent key)
    {
        if (state.Prompt is not { } prompt)
        {
            return new ShellUpdate(state, []);
        }

        var normalized = key.Normalize();
        if (prompt.KeyChoices is { } keys)
        {
            return KeyChosen(state, prompt, keys, normalized);
        }

        if (prompt.Picker is { } picker && (PickerKeys.IsMove(normalized, out var step)
            || prompt is { ListOnly: true } && IsLetterMove(normalized, out step)))
        {
            return Replace(state, prompt with { Picker = picker.Moved(step) });
        }

        if (prompt.ListOnly)
        {
            return new ShellUpdate(state, []);
        }

        if (PromptTextEditor.Apply(prompt.Text, prompt.Caret, key) is not { } edited)
        {
            return new ShellUpdate(state, []);
        }

        var filtered = edited.Text == prompt.Text ? prompt.Picker : prompt.Picker?.Filtered(edited.Text);

        return Replace(state, prompt with { Text = edited.Text, Caret = edited.Caret, Picker = filtered });
    }

    // A one-key prompt closes on a listed key and ignores every other, like Enter.
    private static ShellUpdate KeyChosen(
        ShellState state,
        OpenPrompt prompt,
        ImmutableArray<PromptKeyChoice> keys,
        KeyEvent key)
    {
        var listed = keys.FirstOrDefault(choice => choice.Key == key.Name);
        if (key.Modifiers != KeyModifiers.None || listed is not { } chosen)
        {
            return new ShellUpdate(state, []);
        }

        var answer = new PromptAnswer(string.Empty, chosen.Value);

        return new ShellUpdate(Close(state, prompt), [], prompt.OnAccepted.Answered(answer));
    }

    private static bool IsLetterMove(KeyEvent key, out int step)
    {
        step = 0;
        if (key.Modifiers != KeyModifiers.None)
        {
            return false;
        }

        step = key.Name switch
        {
            "j" or "l" => 1,
            "k" or "h" => -1,
            _ => 0,
        };

        return step != 0;
    }

    private static ShellUpdate Accept(ShellState state)
    {
        if (state.Prompt is not { } prompt || prompt.KeyChoices is not null)
        {
            return new ShellUpdate(state, []);
        }

        string? choice = null;
        if (prompt.Picker is { } picker)
        {
            if (!picker.TryGetSelected(out var selected))
            {
                return new ShellUpdate(state, [new PostStatusEffect(picker.NoMatch, StatusSeverity.Warning)]);
            }

            choice = selected.Value;
        }

        var answer = new PromptAnswer(prompt.Text, choice);

        return new ShellUpdate(Close(state, prompt), [], prompt.OnAccepted.Answered(answer));
    }

    private static ShellUpdate Cancel(ShellState state)
    {
        if (state.Prompt is not { } prompt)
        {
            return new ShellUpdate(state, []);
        }

        return new ShellUpdate(Close(state, prompt), []);
    }

    // A dialog or box open over the prompt keeps the mode it set; it returns to where the prompt was opened from.
    private static ShellState Close(ShellState state, OpenPrompt prompt)
    {
        var closed = state with { Prompt = null };
        if (state.Confirm is { } confirm)
        {
            return closed with { Confirm = confirm with { ReturnMode = prompt.ReturnMode } };
        }

        if (state.Output is { } output)
        {
            return closed with { Output = output with { ReturnMode = prompt.ReturnMode } };
        }

        return closed.WithMode(prompt.ReturnMode);
    }

    private static ShellUpdate Replace(ShellState state, OpenPrompt prompt) =>
        new(state with { Prompt = prompt }, []);
}
