namespace Gitunia.Core.Prompts;

/// <summary>
/// The per-prompt result: what the prompt turns into once it is accepted. A feature implements it with a small record
/// that holds what the prompt was opened for (the tab, the start point) taken from what the user saw, and returns the
/// feature's own command. It is plain data, so a Core test can compare it and no delegate is stored in state.
/// </summary>
public interface IPromptResult
{
    /// <summary>The command to reduce right after the prompt closed.</summary>
    AppCommand Answered(PromptAnswer answer);
}
