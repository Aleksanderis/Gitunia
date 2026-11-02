namespace Gitunia.App.Loading.Graph;

/// <summary>A fixed lane colour count.</summary>
public sealed class FixedLaneColors(int count) : ILaneColors
{
    public int Count => count;
}
