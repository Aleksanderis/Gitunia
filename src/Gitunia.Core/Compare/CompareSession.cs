using System.Collections.Immutable;
using Gitunia.Core.Workspaces.Tabs;

namespace Gitunia.Core.Compare;

/// <summary>The follow state of every tab that compares; a tab without an entry has <see cref="CompareFollow.None"/>.</summary>
/// <param name="Tabs">The follow state per tab.</param>
public sealed record CompareSession(ImmutableDictionary<TabId, CompareFollow> Tabs)
{
    /// <summary>No tab compares.</summary>
    public static CompareSession Empty { get; } = new([]);

    /// <summary>The follow state of a tab.</summary>
    public CompareFollow Of(TabId tab) => Tabs.GetValueOrDefault(tab, CompareFollow.None);

    /// <summary>The session with the follow state of a tab replaced; the default state is not stored.</summary>
    public CompareSession With(TabId tab, CompareFollow follow)
    {
        var tabs = follow == CompareFollow.None ? Tabs.Remove(tab) : Tabs.SetItem(tab, follow);

        return new CompareSession(tabs);
    }
}
