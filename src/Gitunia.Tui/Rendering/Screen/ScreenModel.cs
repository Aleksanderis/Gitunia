using Gitunia.Core.Operations;
using Gitunia.Core.Picker;
using Gitunia.Core.Status;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Tabs;

namespace Gitunia.Tui.Rendering.Screen;

/// <summary>Everything the chrome around the graph shows. Plain data, so a frame can be rendered without a terminal.</summary>
/// <param name="RepositoryName">Name of the active tab's repository.</param>
/// <param name="BranchName">Checked-out branch, null when detached or not loaded yet.</param>
/// <param name="TabNumber">One-based number of the active tab.</param>
/// <param name="TabCount">Open tabs.</param>
/// <param name="ModeName">Input mode shown in the status bar, such as NORMAL.</param>
/// <param name="PendingKeys">The unfinished chord or count (<c>5g</c>), empty when none.</param>
/// <param name="SelectedPosition">One-based position of the selected row, 0 when the graph is empty.</param>
/// <param name="LoadedCount">Rows loaded so far.</param>
/// <param name="IsLoading">The log is still streaming in.</param>
/// <param name="Message">A short notice such as "HEAD is not loaded yet", empty when none.</param>
/// <param name="Tabs">One label per open tab for the tab row; empty falls back to a single label built from the fields above.</param>
/// <param name="Picker">The open repository picker, which replaces the graph area; null when closed.</param>
/// <param name="HomeDirectory">The home directory, shown as <c>~</c> in picker paths.</param>
/// <param name="MessageSeverity">How serious <paramref name="Message"/> is; errors are drawn in the error colour.</param>
/// <param name="SearchLine">The open search or prompt (<c>/foo_  3/17</c>), shown instead of the position; empty when none.</param>
/// <param name="Banner">The in-progress operation of the tab (ux.md, workflow 4), null when none. Drawn on the
/// frame's bottom edge; a terminal under <see cref="ScreenLayout.MinimumHeightWithBanner"/> rows shows its text in
/// the status bar instead.</param>
/// <param name="Hints">The key hints of the focused pane for the status bar; null uses the graph's.</param>
public sealed record ScreenModel(
    string RepositoryName,
    string? BranchName,
    int TabNumber,
    int TabCount,
    string ModeName,
    string PendingKeys,
    int SelectedPosition,
    int LoadedCount,
    bool IsLoading,
    string Message,
    string SearchLine = "",
    IReadOnlyList<TabLabel>? Tabs = null,
    PickerState? Picker = null,
    string HomeDirectory = "",
    StatusSeverity MessageSeverity = StatusSeverity.Info,
    IReadOnlyList<KeyHint>? Hints = null,
    OperationBanner? Banner = null);
