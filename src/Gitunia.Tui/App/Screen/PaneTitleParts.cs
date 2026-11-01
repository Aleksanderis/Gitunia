using Gitunia.Tui.App.Panes;
using Gitunia.Tui.App.WorkingTree;

namespace Gitunia.Tui.App.Screen;

/// <summary>What the pane title rows depend on.</summary>
/// <param name="Repeat">The sticky repeat mode, whose pane shows a marker.</param>
/// <param name="DiffView">Titles the history diff.</param>
/// <param name="WorkingDiff">Titles the Changes view diff.</param>
public sealed record PaneTitleParts(RepeatController Repeat, DiffPaneView DiffView, WorkingDiffController WorkingDiff);
