namespace Gitunia.Core.Config.Models;

/// <summary>How the lines of the commit graph are drawn (the <c>laneStyle</c> option).</summary>
public enum LaneStyle
{
    /// <summary>Light lines with rounded corners (default).</summary>
    Light,

    /// <summary>Heavy lines; box drawing has no heavy rounded corners, so the corners are square.</summary>
    Heavy,
}
