using System.Globalization;
using Gitunia.Core.Jump;
using Gitunia.Core.Jump.Models;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.Search;

/// <summary>
/// The <c>&lt;space&gt;j</c> prompt (flash/easymotion style): type text, the commits in view whose subject contains it
/// get a label key, and pressing a label selects that commit. <c>Enter</c> jumps to the nearest match, <c>Esc</c> cancels.
/// The matching and the labels are the pure <see cref="QuickJumpLabeler"/>; this class only wires keys to it.
/// </summary>
public sealed class QuickJumpController(GraphViewHandle view) : IPromptHandler
{
    private const string Prefix = "jump ";

    private TabSession? _session;
    private string _text = string.Empty;

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine
    {
        get
        {
            if (_session is null)
            {
                return string.Empty;
            }

            if (_text.Length == 0)
            {
                return $"{Prefix}_  type text, then press a label";
            }

            var count = CurrentLabels(_session).Count;
            var matches = count.ToString(CultureInfo.InvariantCulture);
            return count == 0 ? $"{Prefix}{_text}_  no match in view" : $"{Prefix}{_text}_  {matches} in view";
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (action == ActionIds.GraphQuickJump && view.ActiveSession() is { } session)
        {
            _session = session;
            _text = string.Empty;
            session.Source.JumpText = _text;
            view.SetMode(KeyMode.Insert);
        }
        else if (action == ActionIds.InputAccept && _session is not null)
        {
            var nearest = CurrentLabels(_session).Select(label => (int?)label.Row).FirstOrDefault();
            Close();
            if (nearest is { } row)
            {
                view.SelectRow(row);
            }
            else
            {
                Notice = "No match in view";
            }
        }
        else if (action == ActionIds.Cancel && _session is not null)
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
        if (_session is null)
        {
            return false;
        }

        if (view.ActiveSession() != _session)
        {
            // The tab changed under the prompt: drop it, the key is not ours.
            Close();
            return false;
        }

        var update = QuickJumpLabeler.Apply(_text, key, CurrentLabels(_session));
        if (update.JumpRow is { } row)
        {
            Close();
            view.SelectRow(row);
        }
        else
        {
            _text = update.Text;
            _session.Source.JumpText = _text;
        }

        view.Redraw();
        return true;
    }

    public void OnGraphChanged()
    {
        if (_session is not null)
        {
            view.Redraw();
        }
    }

    private IReadOnlyList<JumpLabel> CurrentLabels(TabSession session) =>
        QuickJumpLabeler.Compute(_text, session.Graph, view.FirstVisibleRow(), view.VisibleRowCount(), view.SelectedRow());

    private void Close()
    {
        _session?.Source.JumpText = null;
        _session = null;
        _text = string.Empty;
        view.SetMode(KeyMode.Normal);
    }
}
