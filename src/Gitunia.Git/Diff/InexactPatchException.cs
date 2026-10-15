namespace Gitunia.Git.Diff;

/// <summary>A patch was requested for a file whose diff held bytes that are not valid UTF-8, so the text is not an exact copy and <c>git apply</c> could corrupt the file.</summary>
public sealed class InexactPatchException(string path)
    : InvalidOperationException($"'{path}' contains bytes that are not valid UTF-8; a patch cannot be built from it. Stage the whole file instead.")
{
    public string Path { get; } = path;
}
