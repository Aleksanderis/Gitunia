using System.Collections.Immutable;

namespace Gitunia.Core.Tags;

/// <summary>
/// An open tag prompt. What the action acts on (the start point, the tag) is fixed when the prompt opens, taken from
/// what
/// the user saw.
/// </summary>
/// <param name="Kind">Which prompt is open.</param>
/// <param name="Text">The typed text (the name, the message or the remote filter).</param>
/// <param name="StartPoint">The commit a new tag is made at.</param>
/// <param name="Label">How the start point is named for the user (a ref name or a short hash).</param>
/// <param name="NewName">The accepted name of the tag being created.</param>
/// <param name="TagName">The existing tag a push or remote delete acts on.</param>
/// <param name="Remotes">Every remote, for the remote prompts.</param>
/// <param name="Candidates">The remotes matching <paramref name="Text"/>, best match first.</param>
/// <param name="Index">The highlighted candidate.</param>
public sealed record TagPrompt(
    TagPromptKind Kind,
    string Text,
    string StartPoint,
    string Label,
    string NewName,
    string TagName,
    ImmutableArray<string> Remotes,
    ImmutableArray<string> Candidates,
    int Index)
{
    /// <summary>True for the prompts that choose a remote (type-to-filter) instead of typing text.</summary>
    public bool IsPicker => Kind is TagPromptKind.PushRemote or TagPromptKind.DeleteRemote;

    /// <summary>The highlighted remote, or false when nothing matches.</summary>
    public bool TryGetSelected(out string remote)
    {
        if (Index >= 0 && Index < Candidates.Length)
        {
            remote = Candidates[Index];
            return true;
        }

        remote = string.Empty;
        return false;
    }
}
