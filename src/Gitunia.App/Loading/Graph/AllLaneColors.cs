using Gitunia.Core.Graph;

namespace Gitunia.App.Loading.Graph;

/// <summary>All the colour slots of the graph layout, for callers without a palette.</summary>
public sealed class AllLaneColors : ILaneColors
{
    public static readonly AllLaneColors Instance = new();

    public int Count => BranchColors.SlotCount;
}
