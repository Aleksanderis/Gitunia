using System.Collections.Immutable;
using Gitunia.Core.Blame;
using Gitunia.Core.Keymap;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Settings.Models;
using Gitunia.Core.Text;

namespace Gitunia.Core.Hints;

// The hint sets that depend on the state of their screen (a search being typed, a step back, a selection): the status
// bar and the footer of the overlay both ask these, so the two cannot drift.
public static partial class HintSet
{
    private static readonly ImmutableArray<KeyHint> ReflogPicker =
    [
        new("Enter", HintVerb.Open, [ActionIds.InputAccept]),
        new("Esc", HintVerb.Back, [ActionIds.Cancel]),
    ];

    private static readonly ImmutableArray<KeyHint> SearchEditing = [new("Esc", HintVerb.Cancel, [ActionIds.Cancel])];

    private static readonly ImmutableArray<KeyHint> SearchKept = [new("Esc", HintVerb.Clear, [ActionIds.Cancel])];

    private static readonly ImmutableArray<KeyHint> BlameSelection =
    [
        new("y", "copy", [ActionIds.VisualCopy]),
        new("Esc", HintVerb.Cancel, [ActionIds.Cancel]),
    ];

    private static readonly ImmutableArray<KeyHint> Mainline =
    [
        new("Esc", HintVerb.CancelOrClickOutside, [ActionIds.Cancel]),
    ];

    private static readonly KeyHint SettingsMove = new("j/k", HintVerb.Move, [ActionIds.ListDown, ActionIds.ListUp]);

    private static readonly KeyHint SettingsBack = new("h", HintVerb.Back, [ActionIds.NavigateBack]);

    private static readonly KeyHint SettingsFilterHint = new("/", "filter", [ActionIds.SettingsFilter]);

    private static readonly KeyHint SettingsClose = new("Esc", HintVerb.Close, [ActionIds.NavigateBack]);

    private static readonly KeyHint SettingsEnterEdit = new("Enter", HintVerb.Edit, [ActionIds.SettingsActivate]);

    private static readonly KeyHint SettingsEnterOpen = new("Enter", HintVerb.Open, [ActionIds.SettingsActivate]);

    private static readonly KeyHint SettingsOpenOrEdit = new(
        "l/Enter",
        HintVerb.Edit,
        [ActionIds.SettingsActivate, ActionIds.SettingsActivate]);

    private static readonly ImmutableArray<KeyHint> SettingsRemoval =
    [
        new("y", "remove", [ActionIds.ConfirmYes]),
        new("n", "keep", [ActionIds.ConfirmNo]),
        new("Esc", HintVerb.Keep, [ActionIds.Cancel]),
    ];

    private static readonly ImmutableArray<KeyHint> SettingsTyping =
    [
        new("Enter", HintVerb.Save, [ActionIds.InputAccept]),
        new("Esc", HintVerb.Keep, [ActionIds.Cancel]),
    ];

    private static readonly ImmutableArray<KeyHint> SettingsFilterEditing =
    [
        new(string.Empty, HintVerb.TypeToFilter, []),
        new("Enter", HintVerb.Keep, [ActionIds.InputAccept]),
        new("Esc", HintVerb.Clear, [ActionIds.Cancel]),
    ];

    private static readonly ImmutableArray<KeyHint> SettingsChoice =
    [
        SettingsMove,
        new("Enter", HintVerb.Keep, [ActionIds.InputAccept]),
        new("Esc", HintVerb.Cancel, [ActionIds.Cancel]),
    ];

    private static readonly ImmutableArray<KeyHint> SettingsFiltered =
    [
        SettingsMove,
        SettingsEnterOpen,
        new("/", "new filter", [ActionIds.SettingsFilter]),
        new("Esc", HintVerb.Clear, [ActionIds.NavigateBack]),
    ];

    private static readonly ImmutableArray<KeyHint> SettingsCategories =
    [
        SettingsMove,
        SettingsOpenOrEdit with { Description = HintVerb.Open },
        SettingsFilterHint,
        SettingsClose,
    ];

    private static readonly ImmutableArray<KeyHint> SettingsItem =
    [
        SettingsEnterEdit,
        new("d", "remove", [ActionIds.SettingsRemove]),
        new("J/K", "reorder", [ActionIds.SettingsMoveDown, ActionIds.SettingsMoveUp]),
        new("a", "add", [ActionIds.SettingsAdd]),
        SettingsBack,
        SettingsClose,
    ];

    private static readonly ImmutableArray<KeyHint> SettingsPathList =
    [
        new("Enter/a", "add", [ActionIds.SettingsActivate, ActionIds.SettingsAdd]),
        SettingsMove,
        SettingsBack,
        SettingsFilterHint,
        SettingsClose,
    ];

    private static readonly ImmutableArray<KeyHint> SettingsToggle =
    [
        new("Enter/x", "flip", [ActionIds.SettingsActivate, ActionIds.SettingsToggle]),
        SettingsMove,
        SettingsBack,
        SettingsFilterHint,
        SettingsClose,
    ];

    private static readonly ImmutableArray<KeyHint> SettingsNumber =
    [
        SettingsEnterEdit,
        new("+/-", "step", [ActionIds.SettingsIncrease, ActionIds.SettingsDecrease]),
        SettingsMove,
        SettingsBack,
        SettingsClose,
    ];

    private static readonly ImmutableArray<KeyHint> SettingsOpenable =
        [SettingsEnterOpen, SettingsMove, SettingsBack, SettingsFilterHint, SettingsClose];

    private static readonly ImmutableArray<KeyHint> SettingsReadOnly =
        [SettingsMove, SettingsBack, SettingsFilterHint, SettingsClose];

    private static readonly ImmutableArray<KeyHint> SettingsDefault =
        [SettingsMove, SettingsOpenOrEdit, SettingsBack, SettingsFilterHint, SettingsClose];

    /// <summary>The hints while the reflog is open: the list, the ref picker, or the search being typed or kept.</summary>
    /// <param name="state">The reflog.</param>
    public static IReadOnlyList<KeyHint> ForReflog(ReflogState state)
    {
        if (state.Picker is not null)
        {
            return ReflogPicker;
        }

        if (state.HasSearch)
        {
            return state.IsEditingSearch ? SearchEditing : SearchKept;
        }

        return Reflog;
    }

    /// <summary>The hints while the blame view is open: a search, a selection, a step back or the plain view.</summary>
    /// <param name="state">The blame view.</param>
    public static IReadOnlyList<KeyHint> ForBlame(BlameState state)
    {
        if (state.HasSearch)
        {
            return state.IsEditingSearch ? SearchEditing : SearchKept;
        }

        if (state.Selection is not null)
        {
            return BlameSelection;
        }

        return state.Steps.IsEmpty ? Blame : BlameStepped;
    }

    /// <summary>The reflog list's own hints, whatever its search is doing: the action rows are drawn from these.</summary>
    public static IReadOnlyList<KeyHint> ForReflogList() => Reflog;

    /// <summary>The blame view's own hints, whatever it is doing: the action rows are drawn from these.</summary>
    public static IReadOnlyList<KeyHint> ForBlameList() => Blame;

    /// <summary>The hints of the help and the message history: close or clear the filter, keep it, the next tab, the scroll.</summary>
    /// <param name="isFiltered">The list is narrowed by a filter, so Esc clears it first.</param>
    /// <param name="isEditingFilter">The filter box takes typed text.</param>
    /// <param name="hasTabs">There is more than one tab or section.</param>
    /// <param name="hasMenu">The tabs are a menu (sections), not a row of tabs.</param>
    /// <param name="scrolls">The content is longer than the box.</param>
    public static IReadOnlyList<KeyHint> ForHelp(
        bool isFiltered,
        bool isEditingFilter,
        bool hasTabs,
        bool hasMenu,
        bool scrolls)
    {
        var hints = new List<KeyHint>
        {
            new("Esc", isFiltered ? HintVerb.Clear : HintVerb.Close, [ActionIds.Cancel]),
        };
        if (isEditingFilter)
        {
            hints.Add(new("Enter", HintVerb.Keep, [ActionIds.InputAccept]));
        }

        if (hasTabs)
        {
            hints.Add(new("Tab", hasMenu ? "next section" : "next tab", [ActionIds.HelpTabNext]));
        }

        if (scrolls)
        {
            hints.Add(new(string.Empty, "list keys scroll", []));
        }

        return hints;
    }

    /// <summary>The answer of the mainline question of a cherry-pick or revert of a merge commit.</summary>
    public static IReadOnlyList<KeyHint> ForMainline() => Mainline;

    /// <summary>The hints of the settings screen for what it is doing: a removal question, a field, a choice, a row.</summary>
    /// <param name="state">The settings screen.</param>
    public static IReadOnlyList<KeyHint> ForSettings(SettingsState state)
    {
        if (state.Removal is not null)
        {
            return SettingsRemoval;
        }

        if (state.Input is not null)
        {
            return SettingsTyping;
        }

        if (state.IsEditingFilter)
        {
            return SettingsFilterEditing;
        }

        if (state.Choice is not null)
        {
            return SettingsChoice;
        }

        if (state.IsFiltering)
        {
            return SettingsFiltered;
        }

        if (state.Column == SettingsColumn.Categories && !state.ListsAllCategories)
        {
            return SettingsCategories;
        }

        return state.Selected switch
        {
            { Kind: SettingRowKind.Item } => SettingsItem,
            { Definition.Control: SettingControl.PathList } => SettingsPathList,
            { Definition.Control: SettingControl.Toggle } => SettingsToggle,
            { Definition.Control: SettingControl.Number } => SettingsNumber,
            { Kind: SettingRowKind.Binding } => SettingsOpenable,
            { Definition.Control: SettingControl.KeyReference } => SettingsReadOnly,
            { Definition.Control: SettingControl.Action } => SettingsOpenable,
            _ => SettingsDefault,
        };
    }
}
