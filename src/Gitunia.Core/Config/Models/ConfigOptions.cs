using System.Collections.Immutable;

namespace Gitunia.Core.Config.Models;

/// <summary>The plain options of the config file, after validation.</summary>
/// <param name="ShowBranchInTab">Whether the tab shows the checked-out branch.</param>
/// <param name="ScanRoots">The folders the repository picker scans, null for the conventional ones.</param>
/// <param name="WatchFiles">Live file watching instead of polling; applied at once.</param>
/// <param name="LabelCaps">The ends of the branch label chips in the graph; applied at once.</param>
public sealed record ConfigOptions(bool ShowBranchInTab, ImmutableArray<string>? ScanRoots, bool WatchFiles = false,
    LabelCapStyle LabelCaps = LabelCapStyle.Rounded)
{
    /// <summary>What applies when the file sets nothing.</summary>
    public static ConfigOptions Default { get; } = new(false, null);
}
