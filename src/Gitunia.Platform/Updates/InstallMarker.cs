namespace Gitunia.Platform.Updates;

/// <summary>
/// The install-source marker: a one-line file beside the binary that the install scripts write (and <c>gitunia update</c>
/// leaves alone), so the binary knows a script put it there and it may replace itself.
/// </summary>
public static class InstallMarker
{
    public const string FileName = "gitunia.install-source";

    /// <summary>The marker text of a script install.</summary>
    public const string ScriptValue = "script";
}
