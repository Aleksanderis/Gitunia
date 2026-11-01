using Gitunia.Core.Prompts;

namespace Gitunia.Core.Creation;

/// <summary>A prompt of the clone and init flow. It belongs to the picker and closes with it.</summary>
public interface ICreationPromptResult : IPromptResult;

/// <summary>The answer of the clone source prompt.</summary>
public sealed record CloneSourceAnswer : ICreationPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new CloneSourceAnswered(answer.Text);
}

/// <summary>The answer of the clone target prompt.</summary>
/// <param name="Source">The resolved clone source.</param>
public sealed record CloneTargetAnswer(string Source) : ICreationPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new CloneTargetAnswered(Source, answer.Text);
}

/// <summary>The answer of the init directory prompt.</summary>
public sealed record InitDirectoryAnswer : ICreationPromptResult
{
    public AppCommand Answered(PromptAnswer answer) => new InitDirectoryAnswered(answer.Text);
}
