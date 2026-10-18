namespace Gitunia.Git.Remotes.Models;

/// <summary>A configured remote: its name and fetch URL (empty when it has none).</summary>
/// <param name="Name">The remote's name.</param>
/// <param name="Url">The fetch URL.</param>
public sealed record RemoteInfo(string Name, string Url);
