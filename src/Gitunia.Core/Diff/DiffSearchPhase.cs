namespace Gitunia.Core.Diff;

/// <summary>Where a diff search stands.</summary>
public enum DiffSearchPhase
{
    /// <summary>No search: nothing is highlighted.</summary>
    None,

    /// <summary>The query is being typed (Insert mode).</summary>
    Editing,

    /// <summary>The query was accepted: matches stay highlighted and <c>n</c>/<c>N</c> step through them.</summary>
    Active,
}
