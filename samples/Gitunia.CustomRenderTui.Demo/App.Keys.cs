using Gitunia.CustomRenderTui.Demo.Views;

namespace Gitunia.CustomRenderTui.Demo;

public sealed partial class App
{
    private ListCursor Cursor => _focus switch
    {
        Pane.Sidebar => _sidebarCursor,
        Pane.Details => _detailCursor,
        _ => _graphCursor,
    };

    private int Count => _focus switch
    {
        Pane.Sidebar => _sidebar.Count,
        Pane.Details => _details.Count,
        _ => _graph.Count,
    };

    private int PageRows => Math.Max(1, _buffer.Height - 5);

    private bool OnKey(KeyEvent key)
    {
        if (_searching)
        {
            return OnSearchKey(key);
        }

        var wasPendingG = _pendingG;
        _pendingG = false;
        if (key.Ctrl)
        {
            switch (key.Character)
            {
                case 'd': Cursor.Move(PageRows / 2, Count); break;
                case 'u': Cursor.Move(-(PageRows / 2), Count); break;
                case 'c': _quit = true; break;
            }

            return true;
        }

        switch (key.Kind)
        {
            case KeyKind.Tab: _focus = (Pane)(((int)_focus + 1) % 3); break;
            case KeyKind.Down or KeyKind.WheelDown: Cursor.Move(key.Kind == KeyKind.WheelDown ? 3 : 1, Count); break;
            case KeyKind.Up or KeyKind.WheelUp: Cursor.Move(key.Kind == KeyKind.WheelUp ? -3 : -1, Count); break;
            case KeyKind.PageDown: Cursor.Move(PageRows, Count); break;
            case KeyKind.PageUp: Cursor.Move(-PageRows, Count); break;
            case KeyKind.Home: Cursor.ToStart(); break;
            case KeyKind.End: GoEnd(); break;
            case KeyKind.Text:
                switch (key.Character)
                {
                    case 'j': Cursor.Move(1, Count); break;
                    case 'k': Cursor.Move(-1, Count); break;
                    case 'G': GoEnd(); break;
                    case 'g':
                        if (wasPendingG)
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

    private bool OnSearchKey(KeyEvent key)
    {
        switch (key.Kind)
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
            case KeyKind.Text when !key.Ctrl: _input += key.Character; break;
        }

        return true;
    }

    private void Search(int direction)
    {
        var total = _graph.Count;
        if (_query.Length == 0 || total == 0)
        {
            return;
        }

        for (var step = 1; step <= total; step++)
        {
            var index = (((_graphCursor.Selection + (direction * step)) % total) + total) % total;
            if (_graph.Store.Subject(index).Contains(_query, StringComparison.OrdinalIgnoreCase))
            {
                _graphCursor.Set(index, total);
                return;
            }
        }
    }
}
