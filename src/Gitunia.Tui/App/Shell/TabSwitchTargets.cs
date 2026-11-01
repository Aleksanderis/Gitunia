using Gitunia.Tui.App.Compare;
using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Shell;

/// <summary>The views and controllers that follow the shown tab.</summary>
/// <param name="Compare">Follows the comparison of the tab.</param>
/// <param name="Visual">A Visual range ends with the switch.</param>
/// <param name="DiffView">Hooks the stores of the history diff.</param>
/// <param name="DiffPane">Follows the selection of the new tab.</param>
/// <param name="WorkingDiff">Hooks the stores of the Changes view diff.</param>
public sealed record TabSwitchTargets(
    CompareInput Compare,
    VisualRangeInput Visual,
    DiffPaneView DiffView,
    DiffPaneInput DiffPane,
    WorkingDiffController WorkingDiff);
