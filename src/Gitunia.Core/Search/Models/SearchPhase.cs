namespace Gitunia.Core.Search.Models;

/// <summary>Where a tab is in the search flow.</summary>
public enum SearchPhase
{
    /// <summary>No search; nothing highlighted.</summary>
    Idle,

    /// <summary>The search box is open and the query is being typed.</summary>
    Editing,

    /// <summary>The query was accepted; matches stay highlighted and <c>n</c>/<c>N</c> step through them.</summary>
    Active,
}
