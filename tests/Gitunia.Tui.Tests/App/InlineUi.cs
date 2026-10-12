namespace Gitunia.Tui.Tests.App;

/// <summary>Stands in for the UI thread in loader tests: runs posted actions immediately, one at a time.</summary>
public static class InlineUi
{
    private static readonly Lock Gate = new();

    public static void Post(Action action)
    {
        lock (Gate)
        {
            action();
        }
    }
}
