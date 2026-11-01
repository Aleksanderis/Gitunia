using Gitunia.Tui.App.Details;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;
using Gitunia.Tui.Views;

namespace Gitunia.Tui.App.Shell;

/// <summary>
/// Follows the graph tab binder: when another tab is shown its stores are hooked to the views and the old tab's are let
/// go, and when its graph changed the prompts that depend on the rows are told.
/// </summary>
public sealed class TabSwitchCoordinator
{
    private readonly TabSwitchTargets _targets;
    private readonly DetailPresenter _detail;
    private readonly PaneViews _panes;
    private readonly PromptDisplays _prompts;

    /// <param name="binder">Reports the tab switches and graph changes.</param>
    /// <param name="targets">The views that follow the shown tab.</param>
    /// <param name="detail">Rebuilds the detail pane.</param>
    /// <param name="panes">The detail list, whose rows are the new tab's.</param>
    /// <param name="prompts">The prompts that depend on the graph rows.</param>
    public TabSwitchCoordinator(
        GraphTabBinder binder,
        TabSwitchTargets targets,
        DetailPresenter detail,
        PaneViews panes,
        PromptDisplays prompts)
    {
        _targets = targets;
        _detail = detail;
        _panes = panes;
        _prompts = prompts;
        binder.Switched += OnTabSwitched;
        binder.GraphChanged += OnGraphChanged;
    }

    private void OnGraphChanged()
    {
        foreach (var observer in _prompts.GraphObservers)
        {
            observer.OnGraphChanged();
        }
    }

    private void OnTabSwitched(TerminalTabSession? previous, TerminalTabSession? current)
    {
        if (previous is not null)
        {
            Unhook(previous);
        }

        _targets.Visual.End();
        _panes.Detail.Source = current?.DetailSource;
        _targets.DiffView.OnTabSwitched(previous, current);
        _targets.DiffPane.FollowSelection();
        _targets.WorkingDiff.OnTabSwitched(previous, current);
        if (current is not null)
        {
            Hook(current);
        }

        _detail.RefreshDetail();
    }

    private void Unhook(TerminalTabSession session)
    {
        session.Details.Changed -= _detail.RefreshDetail;
        session.Details.Changed -= _targets.Compare.Follow;
        session.IssueLinksChanged -= _detail.RefreshDetail;
        if (session.Tree is { } tree)
        {
            tree.Store.Changed -= _detail.RefreshDetail;
        }
    }

    private void Hook(TerminalTabSession session)
    {
        session.Details.Changed += _detail.RefreshDetail;
        session.Details.Changed += _targets.Compare.Follow;
        session.IssueLinksChanged += _detail.RefreshDetail;
        if (session.Tree is { } tree)
        {
            tree.Store.Changed += _detail.RefreshDetail;
        }
    }
}
