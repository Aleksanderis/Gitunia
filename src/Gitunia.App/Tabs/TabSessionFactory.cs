namespace Gitunia.App.Tabs;

/// <summary>Builds plain runtime sessions, without a terminal side.</summary>
public sealed class TabSessionFactory : ITabSessionFactory<TabSession>
{
    public TabSession Create(TabSessionParts parts) => new(parts);
}
