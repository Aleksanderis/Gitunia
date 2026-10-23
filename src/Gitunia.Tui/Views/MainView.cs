using Gitunia.Core.Editing;
using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.Rendering.Details;
using Gitunia.Tui.Rendering.Files;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Graph;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Sidebar;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Views.Lists;
using Gitunia.Tui.Views.Theme;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Window = Terminal.Gui.Views.Window;

namespace Gitunia.Tui.Views;

/// <summary>The first screen: tab row, the framed panes and the status bar, laid out like <see cref="ScreenLayout"/>.</summary>
public sealed class MainView : IDisposable
{
    private readonly TabBarView _tabs;
    private readonly PickerView _picker;
    private readonly LineView _status;
    private readonly LineView _bottomEdge;
    private readonly LineView _sidebarFilter;
    private readonly FrameStripView _divider;
    private readonly FrameStripView[] _edges;
    private readonly LineView _detailTitle;
    private readonly LineView _diffTitle;
    private readonly FrameStripView _bottomDividerView;
    private readonly FrameStripView _workingDivider;
    private readonly FrameStripView _workingLeftEdge;
    private readonly FrameStripView _workingRightEdge;
    private readonly LineView _messageOptions;
    private readonly LineView _workingDiffTitle;
    private readonly LineView _stageAllButton;
    private readonly LineView _unstageAllButton;
    private readonly LineView _unstagedPathStyleButton;
    private readonly LineView _stagedPathStyleButton;
    private bool _isWorkingMode;
    private WorkingGridKey? _workingGridKey;
    private FrameGrid? _workingGrid;

    /// <summary>The view name reported for the wheel over an overlay; the application maps it to the open overlay's own view.</summary>
    public const string OverlayGestureView = "Overlay";

    private static readonly PaneRect Empty = new(0, 0, 0, 0);

    private readonly Func<ScreenModel> _model;
    private readonly GraphGlyphSet _glyphs;
    private readonly Func<PaneArrangement> _arrangement;
    private Func<bool> _suppressed = () => false;
    private GridKey? _gridKey;
    private FrameGrid? _grid;

    public MainView(
        Func<ScreenModel> model,
        Func<int, StyledLine> detailTitleLine,
        GraphGlyphSet glyphs,
        SidebarListSource sidebarSource,
        Func<SidebarChrome> sidebarChrome,
        Func<PaneArrangement> arrangement,
        Func<int, StyledLine> diffTitleLine,
        DiffListSource diffSource)
    {
        _arrangement = arrangement;
        _model = model;
        _glyphs = glyphs;
        _tabs = new TabBarView(model, glyphs)
        {
            X = 0,
            Y = ScreenLayout.TabRow,
            Width = Dim.Fill(),
            Height = 1,
        };
        Sidebar = new PaneListView
        {
            OpensRowMenu = true,
            X = 1,
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => Rects().Sidebar.Width),
            Height = Dim.Func(_ => Rects().Sidebar.Height),
            CanFocus = true,
            Source = sidebarSource,
        };
        _sidebarFilter = new LineView(width =>
        {
            var chrome = sidebarChrome();
            return PanePadding.Wrap(SidebarRowRenderer.FilterLine(chrome.FilterText, chrome.IsEditingFilter, PanePadding.TextWidth(width), glyphs));
        })
        {
            X = 1,
            Y = Pos.Func(_ => Rects().SidebarFilter.Y),
            Width = Dim.Func(_ => Rects().SidebarFilter.Width),
            Height = Dim.Func(_ => Rects().SidebarFilter.Height),
        };
        _edges =
        [
            new FrameStripView(CurrentGrid) { X = 0, Y = ScreenLayout.FrameTop, Width = Dim.Fill(), Height = 1 },
            new FrameStripView(CurrentGrid) { X = 0, Y = ScreenLayout.GraphTop, Width = 1, Height = Dim.Func(_ => ScreenLayout.GraphHeight(ScreenHeight())) },
            new FrameStripView(CurrentGrid) { X = Pos.Func(_ => Math.Max(0, ScreenWidth - 1)), Y = ScreenLayout.GraphTop, Width = 1, Height = Dim.Func(_ => ScreenLayout.GraphHeight(ScreenHeight())) },
        ];
        _bottomEdge = new LineView(BottomEdgeLine)
        {
            X = 0,
            Y = Pos.Func(_ => ScreenLayout.FrameBottom(ScreenHeight())),
            Width = Dim.Fill(),
            Height = 1,
        };
        _divider = new FrameStripView(CurrentGrid)
        {
            X = Pos.Func(_ => Rects().SidebarDivider.X),
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => Rects().SidebarDivider.Width),
            Height = Dim.Func(_ => Rects().SidebarDivider.Height),
        };
        Graph = new PaneListView
        {
            X = Pos.Func(_ => Rects().Graph.X),
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => Rects().Graph.Width),
            Height = Dim.Func(_ => Rects().Graph.Height),
            CanFocus = true,
            IgnoresBlankDoubleClick = true,
        };
        _detailTitle = new LineView(width => CurrentGrid().ResolveRules(detailTitleLine(width), Rects().DetailTitle.Y, Rects().DetailTitle.X))
        {
            X = Pos.Func(_ => Rects().DetailTitle.X),
            Y = Pos.Func(_ => Rects().DetailTitle.Y),
            Width = Dim.Func(_ => Rects().DetailTitle.Width),
            Height = Dim.Func(_ => Rects().DetailTitle.Height),
        };
        Detail = new PaneListView
        {
            X = Pos.Func(_ => Rects().Detail.X),
            Y = Pos.Func(_ => Rects().Detail.Y),
            Width = Dim.Func(_ => Rects().Detail.Width),
            Height = Dim.Func(_ => Rects().Detail.Height),
            CanFocus = true,
        };
        Detail.SetScheme(Palette.BaseScheme());
        _bottomDividerView = new FrameStripView(CurrentGrid)
        {
            X = Pos.Func(_ => Rects().BottomDivider.X),
            Y = Pos.Func(_ => Rects().BottomDivider.Y),
            Width = Dim.Func(_ => Rects().BottomDivider.Width),
            Height = Dim.Func(_ => Rects().BottomDivider.Height),
        };
        _diffTitle = new LineView(width => CurrentGrid().ResolveRules(diffTitleLine(width), Rects().DiffTitle.Y, Rects().DiffTitle.X))
        {
            X = Pos.Func(_ => Rects().DiffTitle.X),
            Y = Pos.Func(_ => Rects().DiffTitle.Y),
            Width = Dim.Func(_ => Rects().DiffTitle.Width),
            Height = Dim.Func(_ => Rects().DiffTitle.Height),
        };
        Diff = new PaneListView
        {
            X = Pos.Func(_ => Rects().Diff.X),
            Y = Pos.Func(_ => Rects().Diff.Y),
            Width = Dim.Func(_ => Rects().Diff.Width),
            Height = Dim.Func(_ => Rects().Diff.Height),
            CanFocus = true,
            Source = diffSource,
        };
        UnstagedSource = new WorkingTreeListSource(WorkingTreeSection.Unstaged, glyphs);
        StagedSource = new WorkingTreeListSource(WorkingTreeSection.Staged, glyphs);
        WorkingUnstaged = CreateWorkingList(UnstagedSource, () => WorkingRects().Unstaged);
        WorkingStaged = CreateWorkingList(StagedSource, () => WorkingRects().Staged);
        _workingDivider = new FrameStripView(CurrentGrid)
        {
            X = Pos.Func(_ => WorkingDividerRect().X),
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => WorkingDividerRect().Width),
            Height = Dim.Func(_ => WorkingDividerRect().Height),
            Visible = false,
        };
        _workingLeftEdge = CreateWorkingEdge(
            () => WorkingEdgeRect(WorkingRects().Unstaged, WorkingRects().Staged, isLeft: true));
        _workingRightEdge = CreateWorkingEdge(
            () => WorkingEdgeRect(WorkingRects().Diff, WorkingRects().Message, isLeft: false));
        _workingDiffTitle = new LineView(WorkingDiffTitleLine)
        {
            X = Pos.Func(_ => WorkingRects().Diff.X),
            Y = Pos.Func(_ => WorkingRects().Diff.Y - 1),
            Width = Dim.Func(_ => WorkingRects().Diff.Width),
            Height = Dim.Func(_ => WorkingRects().Diff.IsEmpty ? 0 : 1),
            Visible = false,
        };
        WorkingDiff = new PaneListView
        {
            X = Pos.Func(_ => WorkingRects().Diff.X),
            Y = Pos.Func(_ => WorkingRects().Diff.Y),
            Width = Dim.Func(_ => WorkingRects().Diff.Width),
            Height = Dim.Func(_ => WorkingRects().Diff.Height),
            CanFocus = true,
            Source = new DiffListSource(glyphs) { GroupKeys = () => _model().Keys },
            Visible = false,
        };
        WorkingMessage = new PaneListView
        {
            X = Pos.Func(_ => WorkingRects().MessageEditor.X),
            Y = Pos.Func(_ => WorkingRects().MessageEditor.Y),
            Width = Dim.Func(_ => WorkingRects().MessageEditor.Width),
            Height = Dim.Func(_ => WorkingRects().MessageEditor.Height),
            CanFocus = true,
            Source = new MessageBoxListSource(() => ActiveDraft(), () => IsEditingMessage(), glyphs),
            Visible = false,
        };
        _messageOptions = new LineView(OptionsLine)
        {
            X = Pos.Func(_ => WorkingRects().MessageOptions.X),
            Y = Pos.Func(_ => WorkingRects().MessageOptions.Y),
            Width = Dim.Func(_ => WorkingRects().MessageOptions.Width),
            Height = Dim.Func(_ => WorkingRects().MessageOptions.Height),
            Visible = false,
        };
        _messageOptions.Clicked += OnMessageOptionsClicked;
        _stageAllButton = CreateListButton(WorkingTreeSection.Unstaged, WorkingUnstaged, isPathStyle: false);
        _unstageAllButton = CreateListButton(WorkingTreeSection.Staged, WorkingStaged, isPathStyle: false);
        _unstagedPathStyleButton = CreateListButton(WorkingTreeSection.Unstaged, WorkingUnstaged, isPathStyle: true);
        _stagedPathStyleButton = CreateListButton(WorkingTreeSection.Staged, WorkingStaged, isPathStyle: true);
        _picker = new PickerView(model, glyphs)
        {
            X = 1,
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => ScreenLayout.InnerWidth(ScreenWidth)),
            Height = Dim.Func(_ => ScreenLayout.GraphHeight(ScreenHeight())),
            Visible = false,
        };
        _status = new LineView(width => ChromeBuilder.Status(
            BannerRenderer.ForStatusBar(model(), ScreenHeight()),
            width,
            glyphs))
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            Height = 1,
        };
        Window = new Window { Title = string.Empty, BorderStyle = LineStyle.None, Width = Dim.Fill(), Height = Dim.Fill() };
        Overlay = new OverlayView();
        _tabs.TabClicked += number => Gestured?.Invoke(DefaultMouseMap.TabsView, MouseGesture.Click, number);
        _tabs.DirtyMarkerClicked += number => Gestured?.Invoke(DefaultMouseMap.TabDirtyMarker, MouseGesture.Click, number);
        _tabs.RemoteChipClicked += view => Gestured?.Invoke(view, MouseGesture.Click, null);
        _tabs.NewTabClicked += () => Gestured?.Invoke(DefaultMouseMap.NewTabButton, MouseGesture.Click, null);
        _tabs.CommandsClicked += () => Gestured?.Invoke(DefaultMouseMap.CommandsButton, MouseGesture.Click, null);
        _picker.GestureRaised += gesture => Gestured?.Invoke(DefaultKeymap.PickerView, gesture, null);
        _picker.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        Graph.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.GraphView, gesture, target);
        Sidebar.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.SidebarView, gesture, target);
        Sidebar.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        Detail.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.DetailView, gesture, target);
        Diff.GestureRaised += (gesture, target) => Gestured?.Invoke(DefaultKeymap.DiffView, gesture, target);
        WorkingUnstaged.GestureRaised +=
            (gesture, target) => Gestured?.Invoke(DefaultKeymap.WorkingUnstagedView, gesture, target);
        WorkingStaged.GestureRaised +=
            (gesture, target) => Gestured?.Invoke(DefaultKeymap.WorkingStagedView, gesture, target);
        WorkingDiff.GestureRaised +=
            (gesture, target) => Gestured?.Invoke(WorkingDiffView(), gesture, target);
        WorkingDiff.ButtonAt = (column, row) =>
            (WorkingDiff.Source as DiffListSource)?.ButtonAt(WorkingDiff, column, row);
        WorkingDiff.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        WorkingMessage.GestureRaised +=
            (gesture, target) => Gestured?.Invoke(DefaultKeymap.WorkingMessageView, gesture, target);
        WorkingMessage.ClickPositioned += OnMessageClicked;
        _divider.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Sidebar, column, row);
        _bottomDividerView.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Detail, column, row);
        _detailTitle.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Bottom, column, row);
        _diffTitle.Dragged += (column, row) => EdgeDragged?.Invoke(PaneEdge.Bottom, column, row);
        _bottomEdge.Clicked += OnBottomEdgeClicked;
        _detailTitle.Clicked += OnDetailTitleClicked;
        _diffTitle.Clicked += OnDiffTitleClicked;
        _workingDiffTitle.Clicked += OnWorkingDiffTitleClicked;
        Overlay.LineClicked += line => OverlayLineClicked?.Invoke(line);
        Overlay.GestureRaised += gesture => Gestured?.Invoke(OverlayGestureView, gesture, null);
        Window.Add(
            _tabs,
            _edges[0],
            _bottomEdge,
            _edges[1],
            _edges[2],
            Sidebar,
            _sidebarFilter,
            _divider,
            Graph,
            _detailTitle,
            Detail,
            _bottomDividerView,
            _diffTitle,
            Diff,
            _workingDivider,
            _workingLeftEdge,
            _workingRightEdge,
            _workingDiffTitle,
            WorkingUnstaged,
            WorkingStaged,
            WorkingDiff,
            WorkingMessage,
            _messageOptions,
            _stageAllButton,
            _unstageAllButton,
            _unstagedPathStyleButton,
            _stagedPathStyleButton,
            _picker,
            _status,
            Overlay);
        ApplyVisibility();
    }

    /// <summary>
    /// Raised for a mouse gesture the mouse map decides on: the keymap view name of the pane (or <c>Tabs</c>, <c>Picker</c>),
    /// the gesture and, for the tab row, the one-based number of the clicked tab.
    /// </summary>
    public event Action<string, MouseGesture, int?>? Gestured;

    /// <summary>
    /// Raised before the click gesture on the message box, with the text column and row of the pointer (the box's padding
    /// removed) and the text width of the box.
    /// </summary>
    public event Action<int, int, int>? MessageClicked;

    /// <summary>Raised while a divider or a pane title row is dragged: which edge and the pointer's screen column and row.</summary>
    public event Action<PaneEdge, int, int>? EdgeDragged;

    /// <summary>Raised with the frame line when the overlay box is clicked.</summary>
    public event Action<int>? OverlayLineClicked;

    /// <summary>Raised with the row index when a picker row is clicked (the highlight follows the pointer).</summary>
    public event Action<int>? PickerRowClicked
    {
        add => _picker.RowClicked += value;
        remove => _picker.RowClicked -= value;
    }

    /// <summary>True while panes are shown or hidden: the toolkit moves the focus on its own then, which is not the user's choice.</summary>
    public bool IsChangingVisibility { get; private set; }

    /// <summary>The pane that has focus in the working-tree view, from the tab's state; the layout of a narrow screen follows it.</summary>
    public Func<PaneKind> WorkingFocus { get; set; } = () => PaneKind.WorkingUnstaged;

    /// <summary>The keymap view of the diff box (the side of the selected file), for the mouse map.</summary>
    public Func<string> WorkingDiffView { get; set; } = () => DefaultKeymap.WorkingUnstagedDiffView;

    /// <summary>The staging buttons of the diff box's title row: those of the selected file's side.</summary>
    public Func<WorkingTreeButtons> WorkingDiffButtons { get; set; } = () => WorkingTreeButtons.None;

    /// <summary>True while the diff box fills the whole working-tree view (<c>&lt;space&gt;z</c>).</summary>
    public Func<bool> IsWorkingDiffZoomed { get; set; } = () => false;

    /// <summary>The title of the diff box of the working-tree view: side, hunk and file.</summary>
    public Func<string> WorkingDiffTitle { get; set; } = () => WorkingTreePaneRenderer.DiffTitleName;

    /// <summary>The rows of the Unstaged list of the working-tree view.</summary>
    public WorkingTreeListSource UnstagedSource { get; }

    /// <summary>The rows of the Staged list of the working-tree view.</summary>
    public WorkingTreeListSource StagedSource { get; }

    /// <summary>The Unstaged file list of the working-tree view.</summary>
    public PaneListView WorkingUnstaged { get; }

    /// <summary>The Staged file list of the working-tree view.</summary>
    public PaneListView WorkingStaged { get; }

    /// <summary>The diff box of the working-tree view.</summary>
    public PaneListView WorkingDiff { get; }

    /// <summary>The editor rows of the commit message box of the working-tree view (the option row below them is a separate row).</summary>
    public PaneListView WorkingMessage { get; }

    /// <summary>The commit draft of the active tab, null when it has none; the message box and its option row show it.</summary>
    public Func<CommitDraft?> ActiveDraft { get; set; } = () => null;

    /// <summary>True while the message box is in Insert mode, so it shows the text cursor.</summary>
    public Func<bool> IsEditingMessage { get; set; } = () => false;

    public Window Window { get; }

    public PaneListView Graph { get; }

    public PaneListView Sidebar { get; }

    /// <summary>The commit detail pane below the graph.</summary>
    public PaneListView Detail { get; }

    /// <summary>The diff pane, beside or in place of the detail pane.</summary>
    public PaneListView Diff { get; }

    /// <summary>The floating box for the help, the palette and the which-key popup; added last so it draws on top.</summary>
    public OverlayView Overlay { get; }

    /// <summary>The terminal size in columns and rows.</summary>
    public ScreenSize ScreenSize => new(Window.Viewport.Width, Window.Viewport.Height);

    /// <summary>Makes every pane and the picker swallow mouse input while <paramref name="isSuppressed"/> returns true (an overlay is open).</summary>
    public void SuppressMouseWhen(Func<bool> isSuppressed)
    {
        _suppressed = isSuppressed;
        Graph.IsSuppressed = isSuppressed;
        Sidebar.IsSuppressed = isSuppressed;
        Detail.IsSuppressed = isSuppressed;
        Diff.IsSuppressed = isSuppressed;
        WorkingUnstaged.IsSuppressed = isSuppressed;
        WorkingStaged.IsSuppressed = isSuppressed;
        WorkingDiff.IsSuppressed = isSuppressed;
        WorkingMessage.IsSuppressed = isSuppressed;
        _picker.IsSuppressed = isSuppressed;
    }

    /// <summary>Shows the repository picker in place of the graph, or the graph again.</summary>
    public void ShowPicker(bool isOpen)
    {
        if (_picker.Visible == isOpen)
        {
            return;
        }

        _picker.Visible = isOpen;
        ApplyVisibility();
        if (!isOpen)
        {
            (_isWorkingMode ? FocusedWorkingList() : Graph).SetFocus();
        }

        _picker.SetNeedsDraw();
        Graph.SetNeedsDraw();
    }

    /// <summary>Shows the working-tree view in place of the sidebar, graph and detail panes, or those again.</summary>
    public void ShowWorkingTree(bool isShown)
    {
        if (_isWorkingMode == isShown)
        {
            return;
        }

        _isWorkingMode = isShown;
        ApplyVisibility();
        _workingGrid = null;
        _grid = null;
        Window.SetNeedsLayout();
        Window.SetNeedsDraw();
    }

    private int ScreenWidth => Window.Viewport.Width;

    /// <summary>Raised after the chrome was redrawn: the model a popup is built from may have changed.</summary>
    public event Action? ChromeRefreshed;

    /// <summary>Redraws the tab row, frame and status bar after the model changed.</summary>
    public void RefreshChrome()
    {
        ChromeRefreshed?.Invoke();
        _tabs.SetNeedsDraw();
        _status.SetNeedsDraw();
        _picker.SetNeedsDraw();
        _sidebarFilter.SetNeedsDraw();
        _divider.SetNeedsDraw();
        _bottomEdge.SetNeedsDraw();
        foreach (var edge in _edges)
        {
            edge.SetNeedsDraw();
        }

        Sidebar.SetNeedsDraw();
        _detailTitle.SetNeedsDraw();
        _diffTitle.SetNeedsDraw();
        _bottomDividerView.SetNeedsDraw();
        if (_isWorkingMode)
        {
            _workingDivider.SetNeedsDraw();
            _workingLeftEdge.SetNeedsDraw();
            _workingRightEdge.SetNeedsDraw();
            WorkingUnstaged.SetNeedsDraw();
            WorkingStaged.SetNeedsDraw();
            _workingDiffTitle.SetNeedsDraw();
            WorkingDiff.SetNeedsDraw();
            WorkingMessage.SetNeedsDraw();
            _messageOptions.SetNeedsDraw();
            _stageAllButton.SetNeedsDraw();
            _unstageAllButton.SetNeedsDraw();
            _unstagedPathStyleButton.SetNeedsDraw();
            _stagedPathStyleButton.SetNeedsDraw();
        }
    }

    /// <summary>Repaints every view after the palette changed (the settings screen previews a theme).</summary>
    public void RefreshPalette()
    {
        Detail.SetScheme(Palette.BaseScheme());
        RefreshChrome();
        Window.SetNeedsDraw();
        Graph.SetNeedsDraw();
        Detail.SetNeedsDraw();
        Diff.SetNeedsDraw();
        WorkingUnstaged.SetNeedsDraw();
        WorkingStaged.SetNeedsDraw();
        WorkingDiff.SetNeedsDraw();
        WorkingMessage.SetNeedsDraw();
    }

    public void Dispose() => Window.Dispose();

    private PaneListView FocusedWorkingList() => WorkingFocus() switch
    {
        PaneKind.WorkingStaged => WorkingStaged,
        PaneKind.WorkingUnstagedDiff or PaneKind.WorkingStagedDiff => WorkingDiff,
        PaneKind.WorkingMessage => WorkingMessage,
        _ => WorkingUnstaged,
    };

    // The history panes and the working-tree panes never show together; the picker covers either one. The panes that appear are
    // shown before the others are hidden, so the toolkit never hands the focus of a hiding pane to a pane that is about to hide too.
    private void ApplyVisibility()
    {
        IsChangingVisibility = true;
        try
        {
            SetVisibility();
        }
        finally
        {
            IsChangingVisibility = false;
        }
    }

    private void SetVisibility()
    {
        var isPicker = _picker.Visible;
        var history = !_isWorkingMode;
        var working = _isWorkingMode;
        _workingDivider.Visible = working;
        _workingLeftEdge.Visible = working;
        _workingRightEdge.Visible = working;
        WorkingUnstaged.Visible = working && !isPicker;
        WorkingStaged.Visible = working && !isPicker;
        WorkingDiff.Visible = working && !isPicker;
        _workingDiffTitle.Visible = working && !isPicker;
        WorkingMessage.Visible = working && !isPicker;
        _messageOptions.Visible = working && !isPicker;
        _stageAllButton.Visible = working && !isPicker;
        _unstageAllButton.Visible = working && !isPicker;
        _unstagedPathStyleButton.Visible = working && !isPicker;
        _stagedPathStyleButton.Visible = working && !isPicker;
        Sidebar.Visible = history;
        _sidebarFilter.Visible = history;
        _divider.Visible = history;
        _detailTitle.Visible = history;
        Detail.Visible = history;
        _bottomDividerView.Visible = history;
        _diffTitle.Visible = history;
        Diff.Visible = history;
        Graph.Visible = history && !isPicker;
    }

    private static PaneListView CreateWorkingList(WorkingTreeListSource source, Func<PaneRect> rect) => new()
    {
        X = Pos.Func(_ => rect().X),
        Y = Pos.Func(_ => rect().Y),
        Width = Dim.Func(_ => rect().Width),
        Height = Dim.Func(_ => rect().Height),
        CanFocus = true,
        Source = source,
        Visible = false,
    };

    // The title row of a file list is the top edge of its box, one row above the list; the button sits at its right end and is the
    // mouse path of stage all / unstage all. It has no width when the title leaves no room for it.
    private LineView CreateListButton(WorkingTreeSection section, PaneListView list, bool isPathStyle)
    {
        var view = new LineView(_ => ListButton(section, isPathStyle) is { } button
            ? PathStyleChip.Line(button, _glyphs)
            : new StyledLine([]))
        {
            X = Pos.Func(_ => ListButtonRect(section, isPathStyle).X),
            Y = Pos.Func(_ => ListButtonRect(section, isPathStyle).Y),
            Width = Dim.Func(_ => ListButtonRect(section, isPathStyle).Width),
            Height = Dim.Func(_ => ListButtonRect(section, isPathStyle).Height),
            Visible = false,
        };
        view.Clicked += _ =>
        {
            if (_suppressed() || ListButton(section, isPathStyle) is not { } button)
            {
                return;
            }

            FocusFromTitle(list);
            Gestured?.Invoke(button.View, MouseGesture.Click, null);
        };
        return view;
    }

    private TitleButton? ListButton(WorkingTreeSection section, bool isPathStyle)
    {
        var rect = ListRect(section);
        var tree = section == WorkingTreeSection.Staged ? StagedSource.Tree : UnstagedSource.Tree;
        if (rect.IsEmpty)
        {
            return null;
        }

        var title = WorkingTreePaneRenderer.ListTitle(tree, section);

        return isPathStyle
            ? WorkingListTitleButtons.PathStyleButton(section, title, rect.Width, _model().Keys)
            : WorkingListTitleButtons.Layout(section, title, rect.Width, _model().Keys);
    }

    private PaneRect ListRect(WorkingTreeSection section) =>
        section == WorkingTreeSection.Staged ? WorkingRects().Staged : WorkingRects().Unstaged;

    private PaneRect ListButtonRect(WorkingTreeSection section, bool isPathStyle)
    {
        var rect = ListRect(section);
        return ListButton(section, isPathStyle) is { } button
            ? new PaneRect(rect.X + button.Start, rect.Y - 1, button.Width, 1)
            : Empty;
    }

    private FrameStripView CreateWorkingEdge(Func<PaneRect> rect) => new(CurrentGrid)
    {
        X = Pos.Func(_ => rect().X),
        Y = Pos.Func(_ => rect().Y),
        Width = Dim.Func(_ => rect().Width),
        Height = Dim.Func(_ => rect().Height),
        Visible = false,
    };

    private void OnMessageClicked(int column, int row)
    {
        var padding = PanePadding.ColumnsFor(ScreenWidth);
        var textWidth = PanePadding.TextWidth(WorkingRects().MessageEditor.Width, padding);
        MessageClicked?.Invoke(column - padding, row, textWidth);
    }

    private WorkingTreeRects WorkingRects() => WorkingTreeLayout.Compute(ScreenWidth, ScreenHeight(), WorkingFocus(), IsWorkingDiffZoomed());

    // The line between the lists and the diff, on the screen's rows of content.
    private PaneRect WorkingDividerRect()
    {
        var diff = WorkingRects().Diff;
        return diff.IsEmpty ? Empty : new PaneRect(diff.X - 1, ScreenLayout.GraphTop, 1, ScreenLayout.GraphHeight(ScreenHeight()));
    }

    // The row where two stacked boxes meet (the lower box's top edge, which carries its title): from the screen's left edge or the divider.
    private static PaneRect WorkingEdgeRect(PaneRect upper, PaneRect lower, bool isLeft) =>
        upper.IsEmpty || lower.IsEmpty ? Empty : new PaneRect(isLeft ? 1 : lower.X, lower.Y - 1, lower.Width, 1);

    private int ScreenHeight() => Window.Viewport.Height;

    // A click on a pane's title row focuses the pane, like a click in its list.
    private void FocusFromTitle(PaneListView pane)
    {
        if (!_suppressed() && !pane.HasFocus)
        {
            pane.SetFocus();
        }
    }

    private void OnDetailTitleClicked(int column)
    {
        if (_suppressed())
        {
            return;
        }

        FocusFromTitle(Detail);
        var selectedTab = (Detail.Source as DetailListSource)?.Content.Tab ?? BottomTab.Changes;
        if (TitleButton.ViewAt(DetailTabStrip.Layout(_detailTitle.Viewport.Width, selectedTab), column) is { } tabView)
        {
            Gestured?.Invoke(tabView, MouseGesture.Click, null);
        }
        else if ((Detail.Source as DetailListSource)?.Content is { } content
            && DetailPaneRenderer.PathStyleButton(
                content,
                _detailTitle.Viewport.Width,
                _glyphs,
                _model().Keys) is { } chip
            && column >= chip.Start
            && column < chip.Start + chip.Width)
        {
            Gestured?.Invoke(chip.View, MouseGesture.Click, null);
        }
    }

    private void OnDiffTitleClicked(int column)
    {
        if (_suppressed())
        {
            return;
        }

        FocusFromTitle(Diff);
        var buttons = DiffTitleButtons.Layout(
            _diffTitle.Viewport.Width,
            _model().Keys,
            Diff.Source is DiffListSource { Document.IsFileContent: true });
        if (TitleButton.ViewAt(buttons, column) is { } buttonView)
        {
            Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        }
    }

    private void OnWorkingDiffTitleClicked(int column)
    {
        if (_suppressed())
        {
            return;
        }

        FocusFromTitle(WorkingDiff);
        var buttons = DiffTitleButtons.Layout(_workingDiffTitle.Viewport.Width, WorkingDiffButtons(), _model().Keys);
        if (TitleButton.ViewAt(buttons, column) is { } buttonView)
        {
            Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        }
    }

    private StyledLine WorkingDiffTitleLine(int width)
    {
        var rect = WorkingRects().Diff;
        var line = DiffPaneRenderer.RenderWorkingTitle(
            WorkingDiffTitle(),
            WorkingPanes.IsDiff(WorkingFocus()),
            WorkingDiffButtons(),
            width,
            _glyphs,
            _model().Keys);

        return CurrentGrid().ResolveRules(line, rect.Y - 1, rect.X);
    }

    // The frame's bottom edge, or the in-progress operation banner over it (ux.md, workflow 4).
    private StyledLine BottomEdgeLine(int width)
    {
        var edge = CurrentGrid().Row(ScreenLayout.FrameBottom(ScreenHeight()), 0, width);
        return BannerRenderer.EdgeLine(_model(), edge, width, ScreenHeight(), _glyphs);
    }

    private void OnBottomEdgeClicked(int column)
    {
        var model = _model();
        if (_suppressed() || model.Banner is not { } banner || ScreenHeight() < ScreenLayout.MinimumHeightWithBanner)
        {
            return;
        }

        if (BannerRenderer.Build(banner, ScreenWidth, _glyphs).ViewAt(column) is { } view)
        {
            Gestured?.Invoke(view, MouseGesture.Click, null);
        }
    }

    private FrameGrid CurrentGrid() => _isWorkingMode && !_picker.Visible ? CurrentWorkingGrid() : CurrentHistoryGrid();

    private FrameGrid CurrentWorkingGrid()
    {
        var focus = WorkingFocus();
        var rects = WorkingRects();
        var unstagedTitle = WorkingTreePaneRenderer.ListTitle(UnstagedSource.Tree, WorkingTreeSection.Unstaged);
        var stagedTitle = WorkingTreePaneRenderer.ListTitle(StagedSource.Tree, WorkingTreeSection.Staged);
        var messageTitle = ActiveDraft()?.Title ?? WorkingTreePaneRenderer.MessageTitle;
        var key = new WorkingGridKey(
            ScreenWidth,
            ScreenHeight(),
            rects,
            focus,
            unstagedTitle,
            stagedTitle,
            messageTitle,
            _glyphs.IsAscii);
        if (_workingGrid is null || _workingGridKey != key)
        {
            _workingGrid = FrameGrid.Build(
                ScreenWidth,
                ScreenHeight(),
                [
                    new FrameBox(rects.Unstaged, focus == PaneKind.WorkingUnstaged, unstagedTitle),
                    new FrameBox(rects.Staged, focus == PaneKind.WorkingStaged, stagedTitle),
                    new FrameBox(rects.Diff, WorkingPanes.IsDiff(focus), string.Empty),
                    new FrameBox(rects.Message, focus == PaneKind.WorkingMessage, messageTitle),
                ],
                _glyphs);
            _workingGridKey = key;
        }

        return _workingGrid;
    }

    private FrameGrid CurrentHistoryGrid()
    {
        var isPicker = _picker.Visible;
        var rects = isPicker
            ? new PaneRects(Empty, Empty, Empty, new PaneRect(1, ScreenLayout.GraphTop, ScreenLayout.InnerWidth(ScreenWidth), ScreenLayout.GraphHeight(ScreenHeight())), Empty, Empty, Empty, Empty, Empty)
            : Rects();
        PaneKind? focused = isPicker ? PaneKind.Graph : Sidebar.HasFocus ? PaneKind.Sidebar : Detail.HasFocus ? PaneKind.Detail : Diff.HasFocus ? PaneKind.Diff : PaneKind.Graph;
        var title = _model().RepositoryName.Length > 0 ? _model().RepositoryName : "Repository";
        var key = new GridKey(
            ScreenWidth,
            ScreenHeight(),
            rects,
            focused,
            title,
            isPicker,
            _glyphs.IsAscii);
        if (_grid is null || _gridKey != key)
        {
            _grid = FrameGrid.Build(ScreenWidth, ScreenHeight(), rects, focused, _glyphs, isPicker ? string.Empty : title, isPicker ? string.Empty : "History");
            _gridKey = key;
        }

        return _grid;
    }

    private readonly record struct WorkingGridKey(
        int Width,
        int Height,
        WorkingTreeRects Rects,
        PaneKind Focus,
        string UnstagedTitle,
        string StagedTitle,
        string MessageTitle,
        bool IsAscii);

    private StyledLine OptionsLine(int width) =>
        MessageBoxRenderer.OptionsLine(ActiveDraft(), width, PanePadding.ColumnsFor(ScreenWidth), _glyphs);

    // The checkboxes and the Commit button of the option row are the mouse path of the Alt- chords and the commit key.
    private void OnMessageOptionsClicked(int column)
    {
        if (_suppressed())
        {
            return;
        }

        var padding = PanePadding.ColumnsFor(ScreenWidth);
        var buttons = MessageBoxRenderer.Buttons(PanePadding.TextWidth(_messageOptions.Viewport.Width, padding), padding);
        if (TitleButton.ViewAt(buttons, column) is { } buttonView)
        {
            Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        }
    }

    private PaneRects Rects()
    {
        var current = _arrangement();
        return PaneLayout.Compute(
            ScreenWidth,
            ScreenHeight(),
            current.IsDiffOpen,
            current.Zoomed,
            current.Sizes,
            current.ZoomBoth);
    }
}
