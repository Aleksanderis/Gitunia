using Gitunia.Git.Execution;
using Gitunia.Git.Remotes;
using Gitunia.Git.Remotes.Models;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Git.Repositories;

/// <summary>Clone and init with the git CLI.</summary>
public sealed class RepositoryCreator(RemoteRunner remoteRunner, GitRunner runner, RepositoryDiscovery discovery)
    : IRepositoryCreator
{
    private const string Clone = "clone";
    private const string Init = "init";
    private const string MarkerName = ".git";
    private const int RemoveAttempts = 10;
    private static readonly TimeSpan RemoveRetryDelay = TimeSpan.FromMilliseconds(100);

    public async Task<RepositoryLocation> CloneAsync(
        CloneRequest request,
        IProgress<RemoteProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var target = Path.GetFullPath(request.TargetDirectory);
        var undo = PrepareTarget(target, "clone into");
        try
        {
            await remoteRunner
                .RunGlobalAsync(
                    [Clone, RemoteRunner.ProgressOption, GitOptions.EndOfOptions, request.Source, target],
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
            return await discovery.DiscoverAsync(target, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await FailAsync(undo, exception).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<InitResult> InitAsync(string directory, CancellationToken cancellationToken)
    {
        var target = Path.GetFullPath(directory);
        if (File.Exists(target))
        {
            throw new CloneTargetException($"Cannot initialise {target}: it is a file");
        }

        var marker = Path.Combine(target, MarkerName);
        if (Directory.Exists(marker) || File.Exists(marker))
        {
            var existing = await discovery.DiscoverAsync(target, cancellationToken).ConfigureAwait(false);
            return new InitResult(existing, true);
        }

        var undo = Directory.Exists(target) ? TargetUndo.None : PrepareTarget(target, "initialise");
        try
        {
            await runner
                .RunGlobalAsync([Init, GitOptions.EndOfOptions, target], cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var created = await discovery.DiscoverAsync(target, cancellationToken).ConfigureAwait(false);
            return new InitResult(created, false);
        }
        catch (Exception exception)
        {
            await FailAsync(undo, exception).ConfigureAwait(false);
            throw;
        }
    }

    // Removes what the run made. A killed git may still hold files for a moment, so removal is retried; when it still
    // fails the caller is told instead of being promised a clean state.
    private static async Task FailAsync(TargetUndo undo, Exception original)
    {
        if (original is CloneTargetException || await undo.RunAsync().ConfigureAwait(false))
        {
            return;
        }

        throw new CloneLeftoverException(undo.Path ?? string.Empty, original);
    }

    // Checks the target and remembers what a failed run must remove again.
    private static TargetUndo PrepareTarget(string target, string verb)
    {
        if (File.Exists(target))
        {
            throw new CloneTargetException($"Cannot {verb} {target}: it is a file");
        }

        if (Directory.Exists(target))
        {
            if (Directory.EnumerateFileSystemEntries(target).Any())
            {
                throw new CloneTargetException($"Cannot {verb} {target}: the directory is not empty");
            }

            return new TargetUndo(target, true);
        }

        // Git creates the missing parents too; the topmost one that does not exist yet is the one to remove.
        var topmost = target;
        var parent = Path.GetDirectoryName(topmost);
        while (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
        {
            topmost = parent;
            parent = Path.GetDirectoryName(parent);
        }

        return new TargetUndo(topmost, false);
    }

    private sealed record TargetUndo(string? Path, bool KeepDirectory)
    {
        public static TargetUndo None { get; } = new(null, false);

        public async Task<bool> RunAsync()
        {
            for (var attempt = 0; attempt < RemoveAttempts; attempt++)
            {
                if (TryRemove())
                {
                    return true;
                }

                await Task.Delay(RemoveRetryDelay).ConfigureAwait(false);
            }

            return TryRemove();
        }

        private bool TryRemove()
        {
            if (Path is null || !Directory.Exists(Path))
            {
                return true;
            }

            try
            {
                if (KeepDirectory)
                {
                    Empty(Path);
                }
                else
                {
                    Directory.Delete(Path, recursive: true);
                }

                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void Empty(string directory)
        {
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                Directory.Delete(child, recursive: true);
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                File.Delete(file);
            }
        }
    }
}
