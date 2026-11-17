namespace Gitunia.Core.Filtering;

/// <summary>What a filter list has to show instead of rows.</summary>
public enum FilterStatus
{
    /// <summary>The items are there; no rows means no match.</summary>
    Ready,

    /// <summary>The items are still coming.</summary>
    Loading,

    /// <summary>The items could not be read.</summary>
    Failed,
}
