using Gitunia.Core.Effects;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Search;
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
            state.ReturnMode);
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
        if (prompt.Picker is { } picker && PickerKeys.IsMove(normalized, out var step))
        {
            return Replace(state, prompt with { Picker = picker.Moved(step) });
        }

        if (PromptTextEditor.Apply(prompt.Text, key) is not { } edited || edited == prompt.Text)
        {
            return new ShellUpdate(state, []);
        }

        return Replace(state, prompt with { Text = edited, Picker = prompt.Picker?.Filtered(edited) });
    }

    private static ShellUpdate Accept(ShellState state)
    {
        if (state.Prompt is not { } prompt)
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
