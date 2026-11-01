namespace Gitunia.Core.Tags;

/// <summary>
/// The tag slice of one tab: the open prompt. Whether a tag operation runs is not here but in the tab's
/// <see cref="Running.RunningOperations"/>, which the root reducer keeps and hands to the reducer as
/// <see cref="TagContext.IsBusy"/>.
/// </summary>
/// <param name="Prompt">The open prompt, null when none.</param>
public sealed record TagState(TagPrompt? Prompt)
{
    /// <summary>Nothing open.</summary>
    public static TagState Empty { get; } = new((TagPrompt?)null);
}
