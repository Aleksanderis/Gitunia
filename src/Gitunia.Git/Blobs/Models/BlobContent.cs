namespace Gitunia.Git.Blobs.Models;

/// <summary>The content of one blob as the file view shows it.</summary>
/// <param name="Kind">Text, binary or too large to show.</param>
/// <param name="Text">The decoded text, empty unless <paramref name="Kind"/> is <see cref="BlobKind.Text"/>.</param>
/// <param name="Size">Size of the blob in bytes.</param>
public sealed record BlobContent(BlobKind Kind, string Text, long Size);
