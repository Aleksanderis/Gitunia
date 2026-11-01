namespace Gitunia.App.Tabs;

/// <summary>Builds the session of a tab; the terminal layer supplies one that adds its list sources and views.</summary>
/// <typeparam name="TSession">The session type the runner hands out.</typeparam>
public interface ITabSessionFactory<out TSession>
    where TSession : TabSession
{
    TSession Create(TabSessionParts parts);
}
