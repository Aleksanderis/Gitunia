namespace Gitunia.Core.Details;

/// <summary>Where a tab key sends the bottom detail pane.</summary>
public enum DetailTabMove
{
    /// <summary>The tab after the current one, wrapping round.</summary>
    Next,

    /// <summary>The tab before the current one, wrapping round.</summary>
    Previous,

    /// <summary>The Details tab.</summary>
    Details,

    /// <summary>The Changes tab.</summary>
    Changes,

    /// <summary>The Files tab.</summary>
    Files,
}
