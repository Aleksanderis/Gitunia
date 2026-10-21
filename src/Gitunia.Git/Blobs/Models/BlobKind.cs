namespace Gitunia.Git.Blobs.Models;

/// <summary>What <see cref="BlobContent"/> holds.</summary>
public enum BlobKind
{
    /// <summary>Text: <see cref="BlobContent.Text"/> is the whole content.</summary>
    Text,

    /// <summary>Binary data (a NUL byte near the start, like git's own check); there is no text.</summary>
    Binary,

    /// <summary>Larger than <see cref="BlobReader.MaximumBytes"/>; there is no text.</summary>
    TooLarge,
}
