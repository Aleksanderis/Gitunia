using System.Collections.Immutable;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core.Config.Models;

/// <summary>The plain options of the config file, after validation.</summary>
/// <param name="ShowBranchInTab">Whether the tab shows the checked-out branch.</param>
/// <param name="ScanRoots">The folders the repository picker scans, null for the conventional ones.</param>
/// <param name="WatchFiles">Live file watching instead of polling; read at startup only.</param>
/// <param name="LabelCaps">The ends of the branch label chips in the graph; applied at once.</param>
/// <param name="WhichKeyPopup">Whether a pending chord opens the which-key popup (<c>hints.whichKey</c>); applied at once.</param>
/// <param name="StatusBarHints">How many key hints the status bar shows (<c>hints.statusBar</c>); applied at once.</param>
/// <param name="CommandLevel">
/// Which commands the help, palette, which-key, menus and hints list (<c>commandLevel</c>, #839); applied at once.
/// </param>
public sealed record ConfigOptions(bool ShowBranchInTab, ImmutableArray<string>? ScanRoots, bool WatchFiles = false,
    LabelCapStyle LabelCaps = LabelCapStyle.Rounded, bool WhichKeyPopup = true,
    StatusBarHintMode StatusBarHints = StatusBarHintMode.Full, CommandLevel CommandLevel = CommandLevels.Default)
{
    /// <summary>What applies when the file sets nothing.</summary>
    public static ConfigOptions Default { get; } = new(false, null);
}
