using System.Globalization;
using Gitunia.App.Shell;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Search;
using Gitunia.Core.Search.Models;
using Gitunia.Git;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.Search;

/// <summary>The ref jump prompt (<c>&lt;space&gt;f</c>): type to filter refs (fuzzy) or a hash prefix, <c>Up</c>/<c>Down</c> or <c>Tab</c> to choose, <c>Enter</c> to select that commit.</summary>
public sealed class RefJumpController(GraphViewHandle view, Func<IEnumerable<GitRef>> refs)
    : IActionHandler, ITextKeyHandler, IActionNotice, IPromptDisplay, IGraphObserver
{
    private RefJumpState? _prompt;
    private ObjectId? _pendingCommit;

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine
    {
        get
        {
            if (_prompt is null)
            {
                return string.Empty;
            }

            var line = $"ref {_prompt.Text}_  ";
            if (_prompt.Selected is not { } selected)
            {
                return $"{line}no matching ref or commit";
            }

            var position = (_prompt.SelectedIndex + 1).ToString(CultureInfo.InvariantCulture);
            var total = _prompt.Candidates.Length.ToString(CultureInfo.InvariantCulture);
            return $"{line}{selected.Label} ({position}/{total})";
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (action == ActionIds.GraphJumpToRef && view.ActiveSession() is { } session)
        {
            _prompt = RefJumpResolver.Resolve(string.Empty, refs(), session.Graph);
            view.Dispatcher.SetKeyMode(KeyMode.Insert);
        }
        else if (action == ActionIds.InputAccept && _prompt is not null)
        {
            var selected = _prompt.Selected;
            _prompt = null;
            view.Dispatcher.SetKeyMode(KeyMode.Normal);
            Jump(selected);
        }
        else if (action == ActionIds.Cancel && _prompt is not null)
        {
            _prompt = null;
            view.Dispatcher.SetKeyMode(KeyMode.Normal);
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
        if (_prompt is null || view.ActiveSession() is not { } session)
        {
            return false;
        }

        var normalized = key.Normalize();
        if (normalized is { Name: KeyNames.Up, Modifiers: KeyModifiers.None })
        {
            _prompt = _prompt.Move(-1);
        }
        else if (normalized is { Name: KeyNames.Down or KeyNames.Tab, Modifiers: KeyModifiers.None })
        {
            _prompt = _prompt.Move(1);
        }
        else if (PromptTextEditor.Apply(_prompt.Text, key) is { } text && text != _prompt.Text)
        {
            _prompt = RefJumpResolver.Resolve(text, refs(), session.Graph);
        }

        view.Redraw();
        return true;
    }

    public void OnGraphChanged()
    {
        if (_pendingCommit is { } commitId && view.ActiveSession() is { } session)
        {
            var index = session.Graph.IndexOf(commitId);
            if (index >= 0)
            {
                _pendingCommit = null;
                view.SelectRow(index);
            }
            else if (!session.IsLoading && (session.IsGraphComplete || session.ExpansionFailed))
            {
                _pendingCommit = null;
            }
        }
    }

    private void Jump(RefJumpCandidate? candidate)
    {
        if (candidate is null)
        {
            Notice = "No matching ref or commit";
            return;
        }

        if (view.ActiveSession() is not { } session)
        {
            return;
        }

        var index = session.Graph.IndexOf(candidate.CommitId);
        if (index >= 0)
        {
            view.SelectRow(index);
        }
        else if (session.IsLoading || session.IsTruncated)
        {
            // The log is still streaming in, or its rest has not been asked for yet (past the soft row limit); the jump
            // happens as soon as the commit arrives.
            session.RequestExpand(true);
            _pendingCommit = candidate.CommitId;
            Notice = $"Loading {candidate.Label}...";
        }
        else
        {
            Notice = $"{candidate.Label} is not in the graph";
        }
    }
}
