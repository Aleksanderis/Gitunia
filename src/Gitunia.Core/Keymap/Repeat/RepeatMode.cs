using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Keymap.Repeat;

/// <summary>
/// The sticky repeat mode of a step action (context lines, pane resize): while it lasts the step keys work without
/// their prefix. A pure value, owned by one tab and one focused pane; it ends when either changes.
/// </summary>
/// <param name="Kind">What is being repeated.</param>
/// <param name="Tab">The tab it belongs to.</param>
/// <param name="Pane">The pane that had focus when it started.</param>
public sealed record RepeatMode(RepeatKind Kind, TabId Tab, PaneKind Pane)
{
    /// <summary>The marker the pane title shows while the mode is on.</summary>
    public const string TitleMarker = "[repeat]";

    /// <summary>The keymap view that holds the keys of the context mode.</summary>
    public const string ContextView = "ContextRepeat";

    /// <summary>The keymap view that holds the keys of the resize mode.</summary>
    public const string ResizeView = "ResizeRepeat";

    /// <summary>The keymap view holding this mode's keys.</summary>
    public string View => Kind == RepeatKind.Context ? ContextView : ResizeView;

    /// <summary>The mode a step <paramref name="action"/> starts in <paramref name="pane"/>; null for none.</summary>
    /// <param name="action">The action that just ran.</param>
    /// <param name="tab">The active tab.</param>
    /// <param name="pane">The focused pane.</param>
    public static RepeatMode? Start(ActionId action, TabId tab, PaneKind pane)
    {
        var isDiff = pane is PaneKind.Diff or PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff;
        if (IsContextStep(action) && isDiff)
        {
            return new RepeatMode(RepeatKind.Context, tab, pane);
        }

        if (IsResizeStep(action) && pane is PaneKind.Sidebar or PaneKind.Graph or PaneKind.Detail or PaneKind.Diff)
        {
            return new RepeatMode(RepeatKind.Resize, tab, pane);
        }

        return null;
    }

    /// <summary>The title with the marker in front while the mode is on, so a narrow box cuts the name first.</summary>
    /// <param name="title">The pane title.</param>
    /// <param name="isRepeating">The repeat mode is on for the pane.</param>
    public static string Mark(string title, bool isRepeating) => isRepeating ? $"{TitleMarker} {title}" : title;

    /// <summary>True for the actions that step the context lines of a diff.</summary>
    /// <param name="action">The action.</param>
    public static bool IsContextStep(ActionId action) =>
        action == ActionIds.DiffMoreContext || action == ActionIds.DiffLessContext;

    /// <summary>True for the actions that move a pane edge by one step (not the reset).</summary>
    /// <param name="action">The action.</param>
    public static bool IsResizeStep(ActionId action) =>
        action == ActionIds.PaneWider
        || action == ActionIds.PaneNarrower
        || action == ActionIds.PaneTaller
        || action == ActionIds.PaneShorter;

    /// <summary>True while the mode still belongs to the active tab and the focused pane.</summary>
    /// <param name="activeTab">The active tab.</param>
    /// <param name="focused">The focused pane.</param>
    public bool AppliesTo(TabId? activeTab, PaneKind focused) => activeTab == Tab && focused == Pane;
}
