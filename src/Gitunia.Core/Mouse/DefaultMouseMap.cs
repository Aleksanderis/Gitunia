using Gitunia.Core.Keymap;

namespace Gitunia.Core.Mouse;

/// <summary>The default mouse map, written as data. It mirrors the mouse table in <c>docs/ux.md</c> (a test checks that).</summary>
public static class DefaultMouseMap
{
    /// <summary>The view of the tab row.</summary>
    public const string TabsView = "Tabs";

    /// <summary>The view of the header bar (the first row); a click opens the command palette, the menu of every action.</summary>
    public const string HeaderView = "Header";

    /// <summary>The buttons in the diff pane's title row: each is a view of its own so the map can bind a click to its action.</summary>
    public const string DiffPreviousFileButton = "DiffButton.PreviousFile";

    public const string DiffNextFileButton = "DiffButton.NextFile";

    public const string DiffPreviousHunkButton = "DiffButton.PreviousHunk";

    public const string DiffNextHunkButton = "DiffButton.NextHunk";

    public const string DiffWrapButton = "DiffButton.Wrap";

    public const string DiffZoomButton = "DiffButton.Zoom";

    public const string DiffCloseButton = "DiffButton.Close";

    /// <summary>Picker row buttons, shown on the highlighted row.</summary>
    public const string PickerPinButton = "PickerButton.Pin";

    public const string PickerRemoveButton = "PickerButton.Remove";

    /// <summary>List rows moved per wheel notch.</summary>
    public const int WheelLines = 3;

    /// <summary>Views that inherit from <c>Lists</c>; the others stand alone.</summary>
    public static IReadOnlyDictionary<string, string> ViewParents { get; } = new Dictionary<string, string>
    {
        [DefaultKeymap.GraphView] = DefaultKeymap.ListsView,
        [DefaultKeymap.DetailView] = DefaultKeymap.ListsView,
        [DefaultKeymap.SidebarView] = DefaultKeymap.ListsView,
        [DefaultKeymap.DiffView] = DefaultKeymap.ListsView,
    };

    /// <summary>The views of the overlays: their gestures run even though an open overlay otherwise suppresses the panes behind it.</summary>
    public static IReadOnlyList<string> OverlayViews { get; } = [DefaultKeymap.HelpView, DefaultKeymap.MessagesView, DefaultKeymap.PaletteView];

    public static MouseMap Create() => new(CreateBindings(), ViewParents);

    public static IReadOnlyList<MouseBinding> CreateBindings() =>
    [
        new(DefaultKeymap.ListsView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.ListsView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(DefaultKeymap.GraphView, MouseGesture.DoubleClick, ActionIds.GraphOpenDetails),
        new(DefaultKeymap.DetailView, MouseGesture.DoubleClick, ActionIds.DetailOpenDiff),
        new(DefaultKeymap.SidebarView, MouseGesture.Click, ActionIds.SidebarActivate),
        new(TabsView, MouseGesture.Click, ActionIds.TabNext, UsesTarget: true),
        new(DefaultKeymap.PickerView, MouseGesture.DoubleClick, ActionIds.PickerAccept),
        new(DefaultKeymap.PickerView, MouseGesture.WheelUp, ActionIds.PickerPrevious),
        new(DefaultKeymap.PickerView, MouseGesture.WheelDown, ActionIds.PickerNext),
        new(DefaultKeymap.ListsView, MouseGesture.DragStart, ActionIds.VisualToggle),
        new(DefaultKeymap.ListsView, MouseGesture.DragUp, ActionIds.ListUp, UsesTarget: true),
        new(DefaultKeymap.ListsView, MouseGesture.DragDown, ActionIds.ListDown, UsesTarget: true),
        new(HeaderView, MouseGesture.Click, ActionIds.PaletteOpen),
        new(DefaultKeymap.HelpView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.HelpView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(DefaultKeymap.MessagesView, MouseGesture.WheelUp, ActionIds.ListUp, WheelLines),
        new(DefaultKeymap.MessagesView, MouseGesture.WheelDown, ActionIds.ListDown, WheelLines),
        new(DefaultKeymap.PaletteView, MouseGesture.Click, ActionIds.CommandExecute),
        new(DefaultKeymap.PaletteView, MouseGesture.WheelUp, ActionIds.PalettePrevious),
        new(DefaultKeymap.PaletteView, MouseGesture.WheelDown, ActionIds.PaletteNext),
        new(DiffPreviousFileButton, MouseGesture.Click, ActionIds.DiffPreviousFile),
        new(DiffNextFileButton, MouseGesture.Click, ActionIds.DiffNextFile),
        new(DiffPreviousHunkButton, MouseGesture.Click, ActionIds.DiffPreviousHunk),
        new(DiffNextHunkButton, MouseGesture.Click, ActionIds.DiffNextHunk),
        new(DiffWrapButton, MouseGesture.Click, ActionIds.DiffToggleWrap),
        new(DiffZoomButton, MouseGesture.Click, ActionIds.PaneZoom),
        new(DiffCloseButton, MouseGesture.Click, ActionIds.NavigateBack),
        new(PickerPinButton, MouseGesture.Click, ActionIds.PickerPin),
        new(PickerRemoveButton, MouseGesture.Click, ActionIds.PickerRemoveRecent),
    ];
}
