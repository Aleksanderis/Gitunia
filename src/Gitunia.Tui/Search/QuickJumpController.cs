using System.Globalization;
using Gitunia.Core.Jump;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.App.Tabs;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.Search;

/// <summary>
/// The <c>&lt;space&gt;j</c> prompt (flash/easymotion style): type text, the rows in view that contain it get a label key, and
/// pressing a label moves the cursor to that row. <c>Enter</c> jumps to the nearest match, <c>Esc</c> cancels. It works on
/// whichever list has focus through that list's <see cref="IJumpTarget"/>; the matching and the labels are the pure
/// <see cref="QuickJumpLabeler"/>. This class only wires keys to them.
/// </summary>
public sealed class QuickJumpController(QuickJumpHost host) : IPromptHandler
{
    private const string Prefix = "jump ";

    private TerminalTabSession? _session;
    private IJumpTarget? _target;
    private string _text = string.Empty;

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine
    {
        get
        {
            if (_target is null || IsStale())
            {
                return string.Empty;
            }

            if (_text.Length == 0)
            {
                return $"{Prefix}_  type text, then press a label";
            }

            var count = _target.Labels(_text).Count;
            var matches = count.ToString(CultureInfo.InvariantCulture);
            return count == 0 ? $"{Prefix}{_text}_  no match in view" : $"{Prefix}{_text}_  {matches} in view";
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (IsStale())
        {
            // The tab or the pane changed under the prompt: drop it; its own accept and cancel keys are then done.
            Close();
            host.Redraw();
            if (action == ActionIds.InputAccept || action == ActionIds.Cancel)
            {
                return true;
            }
        }

        if (action == ActionIds.QuickJump)
        {
            Open();
        }
        else if (action == ActionIds.InputAccept && _target is not null)
        {
            var nearest = _target.Labels(_text).Select(label => (int?)label.Row).FirstOrDefault();
            var target = _target;
            Close();
            if (nearest is { } row)
            {
                target.JumpTo(row);
            }
            else
            {
                Notice = "No match in view";
            }
        }
        else if (action == ActionIds.Cancel && _target is not null)
        {
            Close();
        }
        else
        {
            return false;
        }

        host.Redraw();
        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (_target is null)
        {
            return false;
        }

        if (IsStale())
        {
            // The tab or the pane changed under the prompt: drop it, the key is not ours.
            Close();
            return false;
        }

        var update = QuickJumpLabeler.Apply(_text, key, _target.Labels(_text));
        if (update.JumpRow is { } row)
        {
            var target = _target;
            Close();
            target.JumpTo(row);
        }
        else
        {
            _text = update.Text;
            _target.ShowLabels(_text);
        }

        host.Redraw();
        return true;
    }

    public void OnGraphChanged()
    {
        if (_target is not null)
        {
            host.Redraw();
        }
    }

    /// <summary>Closes the prompt when focus moved to another pane or tab without a key (a click).</summary>
    public void CloseIfStale()
    {
        if (IsStale())
        {
            Close();
            host.Redraw();
        }
    }

    private void Open()
    {
        if (host.OpenTarget() is not { } target || host.ActiveSession() is not { } session)
        {
            Notice = "Quick jump is not available here";
        }
        else if (target.UnavailableReason is { } reason)
        {
            Notice = reason;
        }
        else
        {
            _session = session;
            _target = target;
            _text = string.Empty;
            target.ShowLabels(_text);
            host.SetMode(KeyMode.Insert);
        }
    }

    private bool IsStale() =>
        _target is not null && (host.ActiveSession() != _session || host.FocusedPane() != _target.Pane);

    private void Close()
    {
        _target?.ShowLabels(null);
        _target = null;
        _session = null;
        _text = string.Empty;
        host.SetMode(KeyMode.Normal);
    }
}
