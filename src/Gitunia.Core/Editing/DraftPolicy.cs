namespace Gitunia.Core.Editing;

/// <summary>What happens to typed text when the user leaves the editor with Esc (docs/internal/ui-consistency.md, decision 2).</summary>
public enum DraftPolicy
{
    /// <summary>The text is kept and comes back when the editor opens again; throwing it away is an explicit action that asks.</summary>
    Keep,

    /// <summary>The text is thrown away.</summary>
    Discard,
}
