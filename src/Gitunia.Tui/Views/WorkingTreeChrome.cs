using Gitunia.Core.Keymap;
using Gitunia.Core.Mouse;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Tui.Rendering.Diff;
using Gitunia.Tui.Rendering.Panes;
using Gitunia.Tui.Rendering.Screen;
using Gitunia.Tui.Rendering.Styling;
using Gitunia.Tui.Rendering.WorkingTree;
using Gitunia.Tui.Views.Lines;
using Gitunia.Tui.Views.Lists;
using Terminal.Gui.ViewBase;

namespace Gitunia.Tui.Views;

/// <summary>
/// The chrome of the Changes view: the divider and edges between its boxes, the title row of the diff box, the commit
/// option row and the buttons on the title rows of the file lists. It reports a click on any of them as a mouse gesture.
/// </summary>
public sealed class WorkingTreeChrome : IDisposable
{
    private readonly ChromeParts _parts;
    private readonly WorkingListButtons _buttons;
    private readonly FrameStripView _divider;
    private readonly FrameStripView _leftEdge;
    private readonly FrameStripView _rightEdge;
    private readonly LineView _diffTitle;
    private readonly LineView _messageOptions;
    private readonly LineView _stageAllButton;
    private readonly LineView _unstageAllButton;
    private readonly LineView _unstagedViewButton;
    private readonly LineView _resolveButton;
    private readonly LineView _stagedViewButton;
    private readonly LineView _unstagedDiffButton;
    private readonly LineView _stagedDiffButton;

    /// <param name="parts">What the chrome is drawn from.</param>
    /// <param name="buttons">The layout of the list title buttons.</param>
    public WorkingTreeChrome(ChromeParts parts, WorkingListButtons buttons)
    {
        _parts = parts;
        _buttons = buttons;
        var geometry = parts.Geometry;
        var panes = parts.Panes;
        _divider = new FrameStripView(parts.Grids)
        {
            X = Pos.Func(_ => geometry.WorkingDividerRect().X),
            Y = ScreenLayout.GraphTop,
            Width = Dim.Func(_ => geometry.WorkingDividerRect().Width),
            Height = Dim.Func(_ => geometry.WorkingDividerRect().Height),
            Visible = false,
        };
        _leftEdge = CreateEdge(() => EdgeRect(geometry.WorkingRects().Unstaged, geometry.WorkingRects().Staged, isLeft: true));
        _rightEdge = CreateEdge(() => EdgeRect(geometry.WorkingRects().Diff, geometry.WorkingRects().Message, isLeft: false));
        _diffTitle = new LineView(new WorkingDiffTitleLine(parts))
        {
            X = Pos.Func(_ => geometry.WorkingRects().Diff.X),
            Y = Pos.Func(_ => geometry.WorkingRects().Diff.Y - 1),
            Width = Dim.Func(_ => geometry.WorkingRects().Diff.Width),
            Height = Dim.Func(_ => geometry.WorkingRects().Diff.IsEmpty ? 0 : 1),
            Visible = false,
        };
        _messageOptions = new LineView(new MessageOptionsLine(parts))
        {
            X = Pos.Func(_ => geometry.WorkingRects().MessageOptions.X),
            Y = Pos.Func(_ => geometry.WorkingRects().MessageOptions.Y),
            Width = Dim.Func(_ => geometry.WorkingRects().MessageOptions.Width),
            Height = Dim.Func(_ => geometry.WorkingRects().MessageOptions.Height),
            Visible = false,
        };
        _stageAllButton = CreateListButton(WorkingTreeSection.Unstaged, panes.WorkingUnstaged, ListButtonKind.StageAll);
        _unstageAllButton = CreateListButton(WorkingTreeSection.Staged, panes.WorkingStaged, ListButtonKind.StageAll);
        _unstagedViewButton = CreateListButton(WorkingTreeSection.Unstaged, panes.WorkingUnstaged, ListButtonKind.View);
        _stagedViewButton = CreateListButton(WorkingTreeSection.Staged, panes.WorkingStaged, ListButtonKind.View);
        _resolveButton = CreateListButton(WorkingTreeSection.Unstaged, panes.WorkingUnstaged, ListButtonKind.Resolve);
        _unstagedDiffButton = CreateListButton(WorkingTreeSection.Unstaged, panes.WorkingUnstaged, ListButtonKind.Diff);
        _stagedDiffButton = CreateListButton(WorkingTreeSection.Staged, panes.WorkingStaged, ListButtonKind.Diff);
        _messageOptions.Clicked += OnMessageOptionsClicked;
        _diffTitle.Clicked += OnDiffTitleClicked;
        WireGestures();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var view in FrameViews.Concat(RowViews))
        {
            view.Dispose();
        }
    }

    /// <summary>Raised for a click on a chrome view: the keymap view name, the gesture and the target.</summary>
    public event Action<string, MouseGesture, int?>? Gestured;

    /// <summary>
    /// Raised before the click gesture on the message box, with the text column and row of the pointer (the box's padding
    /// removed) and the text width of the box.
    /// </summary>
    public event Action<int, int, int>? MessageClicked;

    /// <summary>The frame views (divider, edges, diff title), to add to the window before the lists.</summary>
    public IEnumerable<View> FrameViews => [_divider, _leftEdge, _rightEdge, _diffTitle];

    /// <summary>The option row and the list buttons, to add to the window after the lists.</summary>
    public IEnumerable<View> RowViews =>
    [
        _messageOptions,
        _stageAllButton,
        _unstageAllButton,
        _unstagedViewButton,
        _resolveButton,
        _stagedViewButton,
        _unstagedDiffButton,
        _stagedDiffButton,
    ];

    /// <summary>Shows or hides the chrome with the Changes view panes.</summary>
    /// <param name="isWorking">The Changes view is the screen.</param>
    /// <param name="isPicker">The picker covers it.</param>
    public void SetVisibility(bool isWorking, bool isPicker)
    {
        var withPanes = isWorking && !isPicker;
        _divider.Visible = isWorking;
        _leftEdge.Visible = isWorking;
        _rightEdge.Visible = isWorking;
        _diffTitle.Visible = withPanes;
        _messageOptions.Visible = withPanes;
        _stageAllButton.Visible = withPanes;
        _unstageAllButton.Visible = withPanes;
        _unstagedViewButton.Visible = withPanes;
        _resolveButton.Visible = withPanes;
        _stagedViewButton.Visible = withPanes;
        _unstagedDiffButton.Visible = withPanes;
        _stagedDiffButton.Visible = withPanes;
    }

    /// <summary>Redraws the chrome views.</summary>
    public void Refresh()
    {
        _divider.SetNeedsDraw();
        _leftEdge.SetNeedsDraw();
        _rightEdge.SetNeedsDraw();
        _diffTitle.SetNeedsDraw();
        _messageOptions.SetNeedsDraw();
        _stageAllButton.SetNeedsDraw();
        _unstageAllButton.SetNeedsDraw();
        _unstagedViewButton.SetNeedsDraw();
        _resolveButton.SetNeedsDraw();
        _stagedViewButton.SetNeedsDraw();
        _unstagedDiffButton.SetNeedsDraw();
        _stagedDiffButton.SetNeedsDraw();
    }

    private void WireGestures()
    {
        var panes = _parts.Panes;
        panes.WorkingUnstaged.GestureRaised += (gesture, target) =>
            Gestured?.Invoke(DefaultKeymap.WorkingUnstagedView, gesture, target);
        panes.WorkingStaged.GestureRaised += (gesture, target) =>
            Gestured?.Invoke(DefaultKeymap.WorkingStagedView, gesture, target);
        panes.WorkingUnstaged.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        panes.WorkingStaged.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        panes.WorkingDiff.GestureRaised += (gesture, target) =>
            Gestured?.Invoke(_parts.Geometry.WorkingDiffView, gesture, target);
        panes.WorkingDiff.ButtonClicked += buttonView => Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        panes.WorkingMessage.GestureRaised += (gesture, target) =>
            Gestured?.Invoke(DefaultKeymap.WorkingMessageView, gesture, target);
        panes.WorkingMessage.ClickPositioned += OnMessageClicked;
    }

    // The title row of a file list is the top edge of its box, one row above the list; its buttons are the mouse
    // path of stage all / unstage all, the view dropdown and (Unstaged, on a conflicted file) resolve.
    private LineView CreateListButton(WorkingTreeSection section, PaneListView list, ListButtonKind kind)
    {
        var view = new LineView(new ListButtonLine(_buttons, section, kind))
        {
            X = Pos.Func(_ => _buttons.Rect(section, kind).X),
            Y = Pos.Func(_ => _buttons.Rect(section, kind).Y),
            Width = Dim.Func(_ => _buttons.Rect(section, kind).Width),
            Height = Dim.Func(_ => _buttons.Rect(section, kind).Height),
            Visible = false,
        };
        view.Clicked += _ =>
        {
            if (_parts.Gate.IsSuppressed || _buttons.Button(section, kind) is not { } button)
            {
                return;
            }

            FocusFromTitle(list);
            Gestured?.Invoke(button.View, MouseGesture.Click, null);
        };

        return view;
    }

    private FrameStripView CreateEdge(Func<PaneRect> rect) => new(_parts.Grids)
    {
        X = Pos.Func(_ => rect().X),
        Y = Pos.Func(_ => rect().Y),
        Width = Dim.Func(_ => rect().Width),
        Height = Dim.Func(_ => rect().Height),
        Visible = false,
    };

    // The row where two stacked boxes meet (the lower box's top edge, which carries its title): from the screen's left
    // edge or the divider.
    private static PaneRect EdgeRect(PaneRect upper, PaneRect lower, bool isLeft)
    {
        if (upper.IsEmpty || lower.IsEmpty)
        {
            return new PaneRect(0, 0, 0, 0);
        }

        return new PaneRect(isLeft ? 1 : lower.X, lower.Y - 1, lower.Width, 1);
    }

    // A click on a pane's title row focuses the pane, like a click in its list.
    private void FocusFromTitle(PaneListView pane)
    {
        if (!_parts.Gate.IsSuppressed && !pane.HasFocus)
        {
            pane.SetFocus();
        }
    }

    private void OnMessageClicked(int column, int row)
    {
        var geometry = _parts.Geometry;
        var padding = PanePadding.ColumnsFor(geometry.ScreenWidth);
        var textWidth = PanePadding.TextWidth(geometry.WorkingRects().MessageEditor.Width, padding);
        MessageClicked?.Invoke(column - padding, row, textWidth);
    }

    private void OnDiffTitleClicked(int column)
    {
        if (_parts.Gate.IsSuppressed)
        {
            return;
        }

        FocusFromTitle(_parts.Panes.WorkingDiff);
        var buttons = DiffTitleButtons.Layout(
            _diffTitle.Viewport.Width,
            _parts.Geometry.WorkingDiffButtons(),
            _parts.Panes.Content.Keys);
        if (TitleButton.ViewAt(buttons, column) is { } buttonView)
        {
            Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        }
    }

    // The checkboxes and the Commit button of the option row are the mouse path of the Alt- chords and the commit key.
    private void OnMessageOptionsClicked(int column)
    {
        if (_parts.Gate.IsSuppressed)
        {
            return;
        }

        var padding = PanePadding.ColumnsFor(_parts.Geometry.ScreenWidth);
        var textWidth = PanePadding.TextWidth(_messageOptions.Viewport.Width, padding);
        var buttons = MessageBoxRenderer.Buttons(textWidth, padding, _parts.Panes.Content.Keys);
        if (TitleButton.ViewAt(buttons, column) is { } buttonView)
        {
            Gestured?.Invoke(buttonView, MouseGesture.Click, null);
        }
    }
}
