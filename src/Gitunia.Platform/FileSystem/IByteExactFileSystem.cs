namespace Gitunia.Platform.FileSystem;

/// <summary>
/// Deletes by raw path bytes, for names that are not valid UTF-8 (possible on Linux) and so cannot be written as a .NET
/// string without losing them. Paths are absolute and NUL-free; the caller builds them from bytes it got from git.
/// </summary>
public interface IByteExactFileSystem
{
    /// <summary>False on an OS without a byte-exact delete (Windows, macOS and any other OS); the members then throw.</summary>
    bool IsSupported { get; }

    /// <summary>Deletes a file or symbolic link (not what it points to). A path that is already gone is not an error.</summary>
    /// <exception cref="NotSupportedException"><see cref="IsSupported"/> is false.</exception>
    /// <exception cref="IOException">The delete failed.</exception>
    void DeleteFile(byte[] path);

    /// <summary>Removes an empty directory; returns false if it is not empty or already gone.</summary>
    /// <exception cref="NotSupportedException"><see cref="IsSupported"/> is false.</exception>
    /// <exception cref="IOException">The removal failed for another reason.</exception>
    bool TryRemoveEmptyDirectory(byte[] path);
}
