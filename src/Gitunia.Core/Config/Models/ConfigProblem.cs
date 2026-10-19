namespace Gitunia.Core.Config.Models;

/// <summary>Something wrong in the config file. The rest of the file still applies.</summary>
/// <param name="Path">Where in the file, such as <c>keys[2]</c>; empty for the whole file.</param>
/// <param name="Message">What is wrong, in a sentence a user can act on.</param>
public sealed record ConfigProblem(string Path, string Message)
{
    public override string ToString() => Path.Length == 0 ? Message : $"{Path}: {Message}";
}
