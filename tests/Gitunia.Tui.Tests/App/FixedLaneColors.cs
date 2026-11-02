using Gitunia.App.Loading.Graph;

namespace Gitunia.Tui.Tests.App;

/// <summary>A fixed lane colour count.</summary>
public sealed class FixedLaneColors(int count) : ILaneColors
{
    public int Count => count;
}
