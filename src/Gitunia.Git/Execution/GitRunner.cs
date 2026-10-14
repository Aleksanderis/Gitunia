using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Execution.Models;

namespace Gitunia.Git.Execution;

/// <summary>
/// Runs the git CLI without a shell. Every call takes an explicit repository path (<c>-C</c>), is async,
/// cancellable and time-bounded, and captures stderr.
/// </summary>
public sealed class GitRunner
{
    private const int ReadBufferSize = 64 * 1024;

    private readonly GitRunnerOptions _options;

    public GitRunner(GitRunnerOptions? options = null)
    {
        _options = options ?? new GitRunnerOptions();
    }

    /// <summary>Runs git in the repository or worktree at <paramref name="repositoryPath"/> and collects all output.</summary>
    /// <exception cref="GitCommandFailedException">Exit code not accepted.</exception>
    /// <exception cref="GitTimeoutException">The timeout elapsed; the process tree was killed.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled; the process tree was killed.</exception>
    public Task<GitResult> RunAsync(
        string repositoryPath,
        IReadOnlyList<string> arguments,
        GitRunOptions? runOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(repositoryPath);
        return RunCoreAsync(BuildArguments(repositoryPath, arguments), runOptions ?? GitRunOptions.Default, cancellationToken);
    }

    /// <summary>Streams stdout in chunks as git produces it. Errors surface after the last chunk; disposing early kills git.</summary>
    public IAsyncEnumerable<byte[]> StreamAsync(
        string repositoryPath,
        IReadOnlyList<string> arguments,
        GitRunOptions? runOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(repositoryPath);
        return StreamCoreAsync(BuildArguments(repositoryPath, arguments), runOptions ?? GitRunOptions.Default, cancellationToken);
    }

    /// <summary>Runs git without a repository (for example <c>--version</c>).</summary>
    public Task<GitResult> RunGlobalAsync(
        IReadOnlyList<string> arguments,
        GitRunOptions? runOptions = null,
        CancellationToken cancellationToken = default) =>
        RunCoreAsync(arguments, runOptions ?? GitRunOptions.Default, cancellationToken);

    private static List<string> BuildArguments(string repositoryPath, IReadOnlyList<string> arguments)
    {
        var all = new List<string>(arguments.Count + 4) { "-C", repositoryPath, "-c", "core.quotepath=off" };
        all.AddRange(arguments);
        return all;
    }

    private static void KillQuietly(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            // Already exited or exiting.
        }
    }

    private static bool IsAccepted(int exitCode, GitRunOptions runOptions) =>
        exitCode == 0 || runOptions.AcceptedExitCodes?.Contains(exitCode) == true;

    private Process Start(IReadOnlyList<string> arguments, GitRunOptions runOptions)
    {
        var startInfo = new ProcessStartInfo(_options.Executable)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["LC_ALL"] = "C";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        if (runOptions.ReadOnly)
        {
            startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        }

        if (_options.EnvironmentOverrides is not null)
        {
            foreach ((var name, var value) in _options.EnvironmentOverrides)
            {
                if (value is null)
                {
                    startInfo.Environment.Remove(name);
                }
                else
                {
                    startInfo.Environment[name] = value;
                }
            }
        }

        var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or FileNotFoundException)
        {
            process.Dispose();
            throw new GitNotFoundException(_options.Executable, exception);
        }

        // Git must never wait for input.
        process.StandardInput.Close();
        return process;
    }

    private async Task<GitResult> RunCoreAsync(IReadOnlyList<string> arguments, GitRunOptions runOptions, CancellationToken cancellationToken)
    {
        var timeout = runOptions.Timeout ?? _options.EffectiveTimeout;
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        using var process = await Task.Run(() => Start(arguments, runOptions), CancellationToken.None).ConfigureAwait(false);
        try
        {
            var outputTask = ReadAllAsync(process.StandardOutput.BaseStream, linked.Token);
            var errorTask = process.StandardError.ReadToEndAsync(linked.Token);
            await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
            var output = outputTask.Result;
            var error = errorTask.Result;
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            if (!IsAccepted(process.ExitCode, runOptions))
            {
                throw new GitCommandFailedException(arguments, process.ExitCode, error);
            }

            return new GitResult(process.ExitCode, output, error);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            KillQuietly(process);
            throw new GitTimeoutException(arguments, timeout);
        }
        catch
        {
            KillQuietly(process);
            throw;
        }
    }

    private async IAsyncEnumerable<byte[]> StreamCoreAsync(
        IReadOnlyList<string> arguments,
        GitRunOptions runOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var timeout = runOptions.Timeout ?? _options.EffectiveTimeout;
        using var timeoutSource = new CancellationTokenSource(Timeout.InfiniteTimeSpan);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        using var process = await Task.Run(() => Start(arguments, runOptions), CancellationToken.None).ConfigureAwait(false);
        var errorTask = process.StandardError.ReadToEndAsync(linked.Token);
        var output = process.StandardOutput.BaseStream;
        var buffer = new byte[ReadBufferSize];
        try
        {
            while (true)
            {
                int count;
                try
                {
                    // The idle timer only runs while waiting for git, never while the consumer handles a chunk.
                    timeoutSource.CancelAfter(timeout);
                    count = await output.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
                    timeoutSource.CancelAfter(Timeout.InfiniteTimeSpan);
                }
                catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    throw new GitTimeoutException(arguments, timeout);
                }

                if (count == 0)
                {
                    break;
                }

                yield return buffer.AsSpan(0, count).ToArray();
            }

            timeoutSource.CancelAfter(timeout);
            await FinishStreamAsync(process, errorTask, arguments, runOptions, timeoutSource, timeout, cancellationToken, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            // Early disposal, cancellation, timeout and failures all end here.
            KillQuietly(process);
        }
    }

    private static async Task FinishStreamAsync(
        Process process,
        Task<string> errorTask,
        IReadOnlyList<string> arguments,
        GitRunOptions runOptions,
        CancellationTokenSource timeoutSource,
        TimeSpan timeout,
        CancellationToken callerToken,
        CancellationToken linkedToken)
    {
        try
        {
            var error = await errorTask.ConfigureAwait(false);
            await process.WaitForExitAsync(linkedToken).ConfigureAwait(false);
            if (!IsAccepted(process.ExitCode, runOptions))
            {
                throw new GitCommandFailedException(arguments, process.ExitCode, error);
            }
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !callerToken.IsCancellationRequested)
        {
            throw new GitTimeoutException(arguments, timeout);
        }
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ReadBufferSize, cancellationToken).ConfigureAwait(false);
        return memory.ToArray();
    }
}
