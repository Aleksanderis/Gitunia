using System.Text.Json.Serialization;

namespace Gitunia.App.Updates;

/// <summary>What the update hint remembers between runs (<c>update-check.json</c> in the state directory).</summary>
/// <param name="CheckedAt">When the last request went out, successful or not.</param>
/// <param name="Latest">The newest release version it saw, null when no request ever worked.</param>
public sealed record UpdateCheckRecord(
    [property: JsonPropertyName("checkedAt")] DateTimeOffset CheckedAt,
    [property: JsonPropertyName("latest")] string? Latest);
