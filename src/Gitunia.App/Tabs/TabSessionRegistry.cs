using System.Diagnostics.CodeAnalysis;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.App.Tabs;

/// <summary>
/// The runtime sessions of the open tabs, by tab. The effect runner fills it; the composition root creates it first and
/// hands it to everything that reads a session, so none of them needs the runner itself.
/// </summary>
/// <typeparam name="TSession">The session type the factory builds.</typeparam>
public sealed class TabSessionRegistry<TSession>
    where TSession : TabSession
{
    private readonly Dictionary<TabId, TSession> _sessions = [];

    /// <summary>The sessions of the open tabs.</summary>
    public IEnumerable<TSession> Values => _sessions.Values;

    /// <summary>Sets the session of a tab.</summary>
    /// <param name="tabId">The tab.</param>
    public TSession this[TabId tabId]
    {
        set => _sessions[tabId] = value;
    }

    /// <summary>The session of a tab, or null.</summary>
    /// <param name="tabId">The tab.</param>
    public TSession? GetValueOrDefault(TabId tabId) => _sessions.GetValueOrDefault(tabId);

    /// <summary>Finds the session of a tab.</summary>
    /// <param name="tabId">The tab.</param>
    /// <param name="session">The session when found.</param>
    public bool TryGetValue(TabId tabId, [MaybeNullWhen(false)] out TSession session) =>
        _sessions.TryGetValue(tabId, out session);

    /// <summary>Removes the session of a tab.</summary>
    /// <param name="tabId">The tab.</param>
    /// <param name="session">The removed session.</param>
    public bool Remove(TabId tabId, [MaybeNullWhen(false)] out TSession session) =>
        _sessions.Remove(tabId, out session);
}
