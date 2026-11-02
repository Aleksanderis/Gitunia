using Gitunia.Core.Graph;

namespace Gitunia.App.Loading.Graph;

/// <summary>The number of distinct lane colours the palette offers, read per load because the scheme can change.</summary>
public interface ILaneColors
{
    /// <summary>Distinct colours of the configured palette, for the layout builder.</summary>
    int Count { get; }
}

/// <summary>All the colour slots of the graph layout, for callers without a palette.</summary>
public sealed class AllLaneColors : ILaneColors
{
    public static readonly AllLaneColors Instance = new();

    public int Count => BranchColors.SlotCount;
}
