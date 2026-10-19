namespace Gitunia.Core.Config.Models;

/// <summary>The outcome of reading the config file; <see cref="Document"/> is empty when there is no usable file.</summary>
/// <param name="Document">The parsed document.</param>
/// <param name="Problems">What was wrong with the file as a whole (unreadable, not valid JSON).</param>
public sealed record ConfigLoadResult(ConfigDocument Document, IReadOnlyList<ConfigProblem> Problems);
