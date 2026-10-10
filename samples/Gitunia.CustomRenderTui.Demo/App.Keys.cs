using Gitunia.CustomRenderTui.Demo.Views;

namespace Gitunia.CustomRenderTui.Demo;

public sealed partial class App
{
    private ListCursor Cursor => _focus switch
    {
        Pane.Sidebar => _sideCur,
        Pane.Details => _detailCur,
        _ => _graphCur,
    };

    private int Count => _focus switch
    {
        Pane.Sidebar => _side.Count,
        Pane.Details => _details.Count,
        _ => _graph.Count,
    };

    private int PageRows => Math.Max(1, _buf.Height - 5);

    private bool OnKey(KeyEvent k)
    {
        if (_searching)
        {
            return OnSearchKey(k);
        }

        var wasG = _pendingG;
        _pendingG = false;
        if (k.Ctrl)
        {
            switch (k.Ch)
            {
                case 'd': Cursor.Move(PageRows / 2, Count); break;
                case 'u': Cursor.Move(-(PageRows / 2), Count); break;
                case 'c': _quit = true; break;
            }

            return true;
        }

        switch (k.Kind)
        {
            case KeyKind.Tab: _focus = (Pane)(((int)_focus + 1) % 3); break;
            case KeyKind.Down or KeyKind.WheelDown: Cursor.Move(k.Kind == KeyKind.WheelDown ? 3 : 1, Count); break;
            case KeyKind.Up or KeyKind.WheelUp: Cursor.Move(k.Kind == KeyKind.WheelUp ? -3 : -1, Count); break;
            case KeyKind.PageDown: Cursor.Move(PageRows, Count); break;
            case KeyKind.PageUp: Cursor.Move(-PageRows, Count); break;
            case KeyKind.Home: Cursor.ToStart(); break;
            case KeyKind.End: GoEnd(); break;
            case KeyKind.Text:
                switch (k.Ch)
                {
                    case 'j': Cursor.Move(1, Count); break;
                    case 'k': Cursor.Move(-1, Count); break;
                    case 'G': GoEnd(); break;
                    case 'g':
                        if (wasG)
                        {
                            Cursor.ToStart();
                            _gotoEndWhenDone = false;
                        }
                        else
                        {
                            _pendingG = true;
                        }

                        break;
                    case 'q': _quit = true; break;
                    case '/' when _focus == Pane.Graph:
                        _searching = true;
                        _input = string.Empty;
                        break;
                    case 'n': Search(1); break;
                    case 'N': Search(-1); break;
                }

                break;
        }

        return true;
    }

    private void GoEnd()
    {
        Cursor.ToEnd(Count);
        _gotoEndWhenDone = _focus == Pane.Graph && _loading; // keep following the end until the stream finishes
    }

    private bool OnSearchKey(KeyEvent k)
    {
        switch (k.Kind)
        {
            case KeyKind.Escape: _searching = false; break;
            case KeyKind.Enter:
                _searching = false;
                _query = _input;
                Search(1);
                break;
            case KeyKind.Backspace:
                if (_input.Length > 0)
                {
                    _input = _input[..^1];
                }

                break;
            case KeyKind.Text when !k.Ctrl: _input += k.Ch; break;
        }

        return true;
    }

    private void Search(int dir)
    {
        var total = _graph.Count;
        if (_query.Length == 0 || total == 0)
        {
            return;
        }

        for (var step = 1; step <= total; step++)
        {
            var i = (((_graphCur.Sel + (dir * step)) % total) + total) % total;
            if (_graph.Store.Subject(i).Contains(_query, StringComparison.OrdinalIgnoreCase))
            {
                _graphCur.Set(i, total);
                return;
            }
        }
    }
}
