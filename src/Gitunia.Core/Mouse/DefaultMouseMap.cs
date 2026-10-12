using Gitunia.Core.Keymap;

namespace Gitunia.Core.Mouse;

/// <summary>The default mouse map, written as data. It mirrors the mouse table in <c>docs/ux.md</c> (a test checks that).</summary>
public static class DefaultMouseMap
{
    /// <summary>The view of the tab row.</summary>
    public const string TabsView = "Tabs";

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
    ];
}
