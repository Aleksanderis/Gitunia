using System.Collections.Immutable;
using Gitunia.App;
using Gitunia.Core.Jump;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Workspaces.Panes;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.Search;

/// <summary>
/// The terminal side of the <c>&lt;space&gt;j</c> prompt (flash/easymotion style): it turns keys into commands and
/// holds the <see cref="IJumpTarget"/> of the list that has focus, which alone knows its rows. The prompt itself (the
/// typed text, which tab and pane it belongs to, the key mode) is state, and what a key does is the
/// <see cref="QuickJumpReducer"/>.
/// </summary>
/// <param name="host">Takes the commands and holds the state.</param>
/// <param name="view">Creates the target of the focused list and redraws the status bar.</param>
public sealed class QuickJumpInput(Dispatcher host, IQuickJumpHost view) : IPromptHandler
{
    private IJumpTarget? _target;

    /// <summary>The prompt reports through status messages (effects), not through a notice.</summary>
    public string Notice => string.Empty;

    public string StatusLine
    {
        get
        {
            if (_target is null || host.State.Shell.QuickJump is not { } session || IsStale(session))
            {
                return string.Empty;
            }

            return QuickJumpReducer.StatusLine(session.Text, _target.Labels(session.Text).Count);
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        if (OpenSession() is { } session && IsStale(session))
        {
            // The tab or the pane changed under the prompt: drop it; its own accept and cancel keys are then done.
            Close();
            view.Redraw();
            if (action == ActionIds.InputAccept || action == ActionIds.Cancel)
            {
                return true;
            }
        }

        if (action == ActionIds.QuickJump)
        {
            Open();
        }
        else if (action == ActionIds.InputAccept && OpenSession() is { } accepting)
        {
            Send(new QuickJumpAccepted(LabelsOf(accepting.Text)));
        }
        else if (action == ActionIds.Cancel && _target is not null)
        {
            Close();
        }
        else
        {
            return false;
        }

        view.Redraw();

        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (OpenSession() is not { } session)
        {
            return false;
        }

        if (IsStale(session))
        {
            // The tab or the pane changed under the prompt: drop it, the key is not ours.
            Close();

            return false;
        }

        Send(new QuickJumpKeyPressed(key, LabelsOf(session.Text)));
        view.Redraw();

        return true;
    }

    public void OnGraphChanged()
    {
        if (_target is not null)
        {
            view.Redraw();
        }
    }

    /// <summary>Closes the prompt when focus moved to another pane or tab without a key (a click).</summary>
    public void CloseIfStale()
    {
        if (OpenSession() is { } session && IsStale(session))
        {
            Close();
            view.Redraw();
        }
    }

    /// <summary>Moves the cursor of the labelled list to a row; the answer to <see cref="JumpToRowEffect"/>.</summary>
    public void JumpTo(int row) => _target?.JumpTo(row);

    private void Open()
    {
        var tab = host.Workspace.ActiveTab;
        var target = view.OpenTarget();
        var pane = tab?.View.FocusedPane ?? PaneKind.Graph;
        host.Dispatch(new QuickJumpOpenRequested(tab?.Id, pane, target is not null, target?.UnavailableReason));
        if (host.State.Shell.QuickJump is not null)
        {
            _target = target;
            target?.ShowLabels(string.Empty);
        }
    }

    // Reduces the key; the target then shows the new text, or no labels when the prompt ended.
    private void Send(QuickJumpCommand command)
    {
        var target = _target;
        host.Dispatch(command);
        if (host.State.Shell.QuickJump is { } session)
        {
            target?.ShowLabels(session.Text);

            return;
        }

        _target = null;
        target?.ShowLabels(null);
    }

    private ImmutableArray<JumpLabel> LabelsOf(string text) =>
        _target is null ? [] : [.. _target.Labels(text)];

    private QuickJumpSession? OpenSession() => _target is null ? null : host.State.Shell.QuickJump;

    private bool IsStale(QuickJumpSession session) =>
        _target is not null
        && (host.Workspace.ActiveTabId != session.TabId
            || host.Workspace.ActiveTab?.View.FocusedPane != _target.Pane);

    private void Close()
    {
        var target = _target;
        _target = null;
        target?.ShowLabels(null);
        host.Dispatch(new QuickJumpClosed());
    }
}
