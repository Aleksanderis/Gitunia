using Gitunia.Git.Refs;
using Gitunia.Git.Remotes.Models;

namespace Gitunia.Git.Remotes;

/// <summary>
/// The git arguments of fetch, pull and push. The background writer and the terminal retry (#45) build their commands
/// here, so a retry in the terminal runs exactly what failed in the background.
/// </summary>
public static class RemoteCommands
{
    private const string Fetch = "fetch";
    private const string Pull = "pull";
    private const string Push = "push";
    private const string DeleteOption = "--delete";
    private const string NoProgressOption = "--no-progress";
    private const string SetUpstreamOption = "--set-upstream";
    private const string LeaseOption = "--force-with-lease";
    private const string AllOption = "--all";
    private const string PruneOption = "--prune";
    private const string NoRebaseOption = "--no-rebase";
    private const string FastForwardOnly = "--ff-only";

    /// <summary>
    /// Arguments of a fetch; <paramref name="progress"/> adds <c>--progress</c> for a redirected stderr.
    /// </summary>
    public static IReadOnlyList<string> BuildFetch(FetchRequest request, bool progress)
    {
        var arguments = new List<string> { Fetch };
        if (progress)
        {
            arguments.Add(RemoteRunner.ProgressOption);
        }

        if (request.Prune)
        {
            arguments.Add(PruneOption);
        }

        if (request.AllRemotes)
        {
            arguments.Add(AllOption);
        }
        else if (request.Remote is not null)
        {
            arguments.Add(GitOptions.EndOfOptions);
            arguments.Add(request.Remote);
        }

        return arguments;
    }

    /// <summary>
    /// Arguments of a single-process pull for the terminal: fast-forward only and never a rebase, whatever
    /// <c>pull.rebase</c> says (the background pull is a fetch then <c>merge --ff-only</c>, the same outcome).
    /// </summary>
    public static IReadOnlyList<string> BuildPull() => [Pull, NoRebaseOption, FastForwardOnly];

    /// <summary>
    /// Arguments of a push; <paramref name="progress"/> adds <c>--progress</c> for a redirected stderr.
    /// </summary>
    public static IReadOnlyList<string> BuildPush(PushRequest request, bool progress)
    {
        var arguments = new List<string> { Push };
        if (progress)
        {
            arguments.Add(RemoteRunner.ProgressOption);
        }

        if (request.SetUpstream)
        {
            arguments.Add(SetUpstreamOption);
        }

        if (request.Lease is { } lease)
        {
            arguments.Add($"{LeaseOption}={GitRefPrefixes.Heads}{lease.RemoteBranch}:{lease.ExpectedHash}");
        }

        if (request.Remote is not null)
        {
            arguments.Add(GitOptions.EndOfOptions);
            arguments.Add(request.Remote);
            if (request.Branch is not null)
            {
                var target = request.Lease is { } leased
                    ? $"{request.Branch}:{GitRefPrefixes.Heads}{leased.RemoteBranch}"
                    : request.Branch;
                arguments.Add(target);
            }
        }

        return arguments;
    }

    /// <summary>
    /// Arguments of a remote branch delete guarded by a lease on the confirmed tip; <paramref name="background"/>
    /// adds <c>--no-progress</c> for a redirected stderr (the terminal retry omits it).
    /// </summary>
    public static IReadOnlyList<string> BuildDeleteBranch(
        string remote,
        string branch,
        string expectedHash,
        bool background)
    {
        var reference = GitRefPrefixes.Heads + branch;
        var arguments = new List<string> { Push };
        if (background)
        {
            arguments.Add(NoProgressOption);
        }

        arguments.Add(DeleteOption);
        arguments.Add($"{LeaseOption}={reference}:{expectedHash}");
        arguments.Add(GitOptions.EndOfOptions);
        arguments.Add(remote);
        arguments.Add(reference);
        return arguments;
    }

    /// <summary>Arguments of a push of one tag; <paramref name="background"/> adds <c>--no-progress</c>.</summary>
    public static IReadOnlyList<string> BuildTagPush(string remote, string tag, bool background) =>
        BuildTagArguments(remote, tag, background, false);

    /// <summary>Arguments of a remote tag delete; <paramref name="background"/> adds <c>--no-progress</c>.</summary>
    public static IReadOnlyList<string> BuildTagDelete(string remote, string tag, bool background) =>
        BuildTagArguments(remote, tag, background, true);

    private static List<string> BuildTagArguments(string remote, string tag, bool background, bool delete)
    {
        var arguments = new List<string> { Push };
        if (background)
        {
            arguments.Add(NoProgressOption);
        }

        if (delete)
        {
            arguments.Add(DeleteOption);
        }

        arguments.Add(GitOptions.EndOfOptions);
        arguments.Add(remote);
        arguments.Add(GitRefPrefixes.Tags + tag);
        return arguments;
    }
}
