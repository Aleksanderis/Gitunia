using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tabs;

/// <summary>
/// One token source per tab for the work effect handlers do on its behalf. Closing a tab cancels it; the next effect of
/// that tab id would get a fresh source. UI thread only.
/// </summary>
public sealed class TabCancellation : IDisposable
{
    private readonly Dictionary<TabId, CancellationTokenSource> _sources = [];

    /// <summary>The token that ends the work of the tab.</summary>
    public CancellationToken TokenOf(TabId tabId)
    {
        if (!_sources.TryGetValue(tabId, out var source))
        {
            source = new CancellationTokenSource();
            _sources[tabId] = source;
        }

        return source.Token;
    }

    /// <summary>Cancels the tab's running work and forgets its source.</summary>
    public void Cancel(TabId tabId)
    {
        if (_sources.Remove(tabId, out var source))
        {
            source.Cancel(throwOnFirstException: false);
            source.Dispose();
        }
    }

    /// <summary>Cancels the work of every tab (a forced quit).</summary>
    public void CancelAll()
    {
        foreach (var source in _sources.Values)
        {
            source.Cancel(throwOnFirstException: false);
        }
    }

    public void Dispose()
    {
        foreach (var source in _sources.Values)
        {
            source.Dispose();
        }

        _sources.Clear();
    }
}
