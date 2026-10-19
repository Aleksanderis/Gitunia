using System.Text.Json;
using Gitunia.Core.Config.Models;

namespace Gitunia.Core.Config;

/// <summary>
/// Reads <c>config.json</c> from the config directory. Never throws except for cancellation: a missing file is
/// an empty config, an unreadable or invalid one is an empty config plus a problem to show. The file is never
/// modified or moved (it is the user's text).
/// </summary>
public sealed class ConfigLoader
{
    /// <summary>The config file name inside the config directory.</summary>
    public const string FileName = "config.json";

    private const long MaximumFileBytes = 1024 * 1024;

    /// <summary>
    /// Creates a loader for a config directory (from <c>IPlatform.ConfigDirectory</c>; tests pass a temp
    /// directory).
    /// </summary>
    /// <param name="configDirectory">The directory holding the config file.</param>
    public ConfigLoader(string configDirectory)
    {
        FilePath = Path.Combine(configDirectory, FileName);
    }

    /// <summary>The full path of the config file.</summary>
    public string FilePath { get; }

    /// <summary>Parses config text; an invalid text yields an empty document and a problem.</summary>
    /// <param name="text">The file text.</param>
    /// <param name="problems">Receives the problem when the text does not parse.</param>
    public static ConfigDocument Parse(string text, ICollection<ConfigProblem> problems)
    {
        try
        {
            var document = JsonSerializer.Deserialize(text, ConfigJsonContext.Default.ConfigDocument);
            if (document is not null)
            {
                return document;
            }

            problems.Add(new ConfigProblem(string.Empty, $"{FileName} is empty, using the defaults"));
        }
        catch (JsonException exception)
        {
            var line = exception.LineNumber is { } number ? $" (line {number + 1})" : string.Empty;
            problems.Add(new ConfigProblem(string.Empty, $"{FileName} is not valid{line}, using the defaults: {exception.Message}"));
        }

        return new ConfigDocument();
    }

    /// <summary>Reads and parses the file.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<ConfigLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        var problems = new List<ConfigProblem>();
        var text = await ReadTextAsync(problems, cancellationToken).ConfigureAwait(false);
        var document = text is null ? new ConfigDocument() : Parse(text, problems);

        return new ConfigLoadResult(document, problems);
    }

    /// <summary>
    /// Reads the file text; null when there is no file or it cannot be used (then a problem is added when
    /// it exists).
    /// </summary>
    /// <param name="problems">Receives the problem when the file exists but cannot be used.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<string?> ReadTextAsync(ICollection<ConfigProblem> problems, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(FilePath);
            if (!info.Exists)
            {
                return null;
            }

            if (info.Length > MaximumFileBytes)
            {
                problems.Add(new ConfigProblem(string.Empty, $"{FileName} is too large and was ignored"));
                return null;
            }

            return await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problems.Add(new ConfigProblem(string.Empty, $"{FileName} could not be read: {exception.Message}"));
            return null;
        }
    }
}
