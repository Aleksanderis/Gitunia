using Gitunia.App.Tabs;

namespace Gitunia.App.Tests.Fakes;

/// <summary>Builds plain runtime sessions, without a terminal side; the diff stores in the parts stay unused.</summary>
public sealed class TabSessionFactory : ITabSessionFactory<TabSession>
{
    public TabSession Create(TabSessionParts parts) => new(parts);
}
