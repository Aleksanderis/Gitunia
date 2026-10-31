using Gitunia.Git.Repositories.Models;
using Gitunia.Git.Stash;
using Gitunia.Git.Stash.Models;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>
/// Records the stash writes and answers with a chosen outcome, or throws <see cref="Failure"/> like git failing.
/// </summary>
public sealed class FakeStashWriter : IStashWriter
{
    /// <summary>The calls in order, such as <c>push|msg|True</c> or <c>drop|stash@{0}</c>.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>What every call answers.</summary>
    public StashOutcome Outcome { get; set; } = StashOutcome.Done;

    /// <summary>Thrown by every call when set.</summary>
    public Exception? Failure { get; set; }

    public Task<StashResult> PushAsync(
        RepositoryLocation location,
        string message,
        bool includeUntracked,
        CancellationToken cancellationToken) =>
        Answer($"push|{message}|{includeUntracked}", "0123456789abcdef");

    public Task<StashResult> ApplyAsync(
        RepositoryLocation location,
        StashEntry entry,
        CancellationToken cancellationToken) =>
        Answer($"apply|{entry.Name}", entry.CommitHash);

    public Task<StashResult> PopAsync(
        RepositoryLocation location,
        StashEntry entry,
        CancellationToken cancellationToken) =>
        Answer($"pop|{entry.Name}", entry.CommitHash);

    public Task<StashResult> DropAsync(
        RepositoryLocation location,
        StashEntry entry,
        CancellationToken cancellationToken) =>
        Answer($"drop|{entry.Name}", entry.CommitHash);

    /// <summary>What the restore check and the restore find.</summary>
    public StashRestoreState RestoreState { get; set; } = StashRestoreState.Overwrites;

    public Task<StashRestoreState> CheckRestoreAsync(
        RepositoryLocation location,
        StashEntry entry,
        StashFileChange file,
        CancellationToken cancellationToken)
    {
        Calls.Add($"check-restore|{entry.Name}|{file.Change.Path}|{file.Origin}");

        return Failure is null ? Task.FromResult(RestoreState) : Task.FromException<StashRestoreState>(Failure);
    }

    public Task<StashRestoreResult> RestoreFileAsync(
        RepositoryLocation location,
        StashEntry entry,
        StashFileChange file,
        CancellationToken cancellationToken)
    {
        Calls.Add($"restore|{entry.Name}|{file.Change.Path}|{file.Origin}");
        if (Failure is not null)
        {
            return Task.FromException<StashRestoreResult>(Failure);
        }

        var backup = RestoreState == StashRestoreState.Overwrites ? "feedbeef" : null;

        return Task.FromResult(new StashRestoreResult(RestoreState, backup));
    }

    private Task<StashResult> Answer(string call, string hash)
    {
        Calls.Add(call);

        if (Failure is null)
        {
            return Task.FromResult(new StashResult(Outcome, hash));
        }

        return Task.FromException<StashResult>(Failure);
    }
}
