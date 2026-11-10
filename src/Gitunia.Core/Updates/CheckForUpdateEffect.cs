namespace Gitunia.Core.Updates;

/// <summary>Look for a newer release in the background (at most one request a day) and post an <see cref="UpdateCheckCompleted"/>.</summary>
/// <param name="CurrentVersion">The version of the running build.</param>
public sealed record CheckForUpdateEffect(string CurrentVersion) : AppEffect;
