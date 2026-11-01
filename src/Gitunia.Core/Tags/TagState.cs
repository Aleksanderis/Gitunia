namespace Gitunia.Core.Tags;

/// <summary>
/// The tag slice of one tab: the open prompt and whether a tag operation runs (one at a time per tab; tab eviction
/// waits for it). The reducer sets <paramref name="Running"/> when it returns a git effect and clears it on the result.
/// </summary>
/// <param name="Prompt">The open prompt, null when none.</param>
/// <param name="Running">A tag operation is in flight.</param>
public sealed record TagState(TagPrompt? Prompt, bool Running)
{
    /// <summary>Nothing open, nothing running.</summary>
    public static TagState Empty { get; } = new(null, false);
}
