namespace Gitunia.Platform.FileSystem;

/// <summary>Windows, macOS and any other OS: explicitly unsupported (names there are always valid text).</summary>
public sealed class UnsupportedByteExactFileSystem : IByteExactFileSystem
{
    private const string Message = "Deleting by raw path bytes is only supported on Linux.";

    public bool IsSupported => false;

    public void DeleteFile(byte[] path) => throw new NotSupportedException(Message);

    public bool TryRemoveEmptyDirectory(byte[] path) => throw new NotSupportedException(Message);
}
