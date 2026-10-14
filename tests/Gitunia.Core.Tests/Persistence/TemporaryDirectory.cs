namespace Gitunia.Core.Tests.Persistence;

/// <summary>A unique directory under the system temp path, removed on dispose.</summary>
public sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), $"gitunia-tests-{Guid.NewGuid().ToString("N")}");
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
