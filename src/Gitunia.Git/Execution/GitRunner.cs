using System.Buffers;
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
    private const int ErrorBufferSize = 4096;
    private const char LineFeed = '\n';
    private const char CarriageReturn = '\r';

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

    /// <summary>
    /// Streams stdout in chunks as git produces it. Errors surface after the last chunk; disposing early kills git. A
    /// chunk is a view of a pooled buffer: it is valid only until the next chunk is requested, so parse or copy it first.
    /// </summary>
    public IAsyncEnumerable<ReadOnlyMemory<byte>> StreamAsync(
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

    private static void ApplyEnvironment(ProcessStartInfo startInfo, IReadOnlyDictionary<string, string?>? variables)
    {
        if (variables is null)
        {
            return;
        }

        foreach ((var name, var value) in variables)
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

    // Reports each line-feed or carriage-return terminated segment and restarts the idle timer on every read.
    private static async Task<string> ReadErrorAsync(
        Process process,
        GitRunOptions runOptions,
        CancellationTokenSource timeoutSource,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (runOptions.OnStandardErrorLine is not { } onLine)
        {
            return await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        var all = new StringBuilder();
        var segment = new StringBuilder();
        var buffer = new char[ErrorBufferSize];
        var reader = process.StandardError;
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            timeoutSource.CancelAfter(timeout);
            all.Append(buffer, 0, count);
            for (var index = 0; index < count; index++)
            {
                if (buffer[index] is LineFeed or CarriageReturn)
                {
                    FlushSegment(segment, onLine);
                }
                else
                {
                    segment.Append(buffer[index]);
                }
            }
        }

        FlushSegment(segment, onLine);
        return all.ToString();
    }

    private static void FlushSegment(StringBuilder segment, Action<string> onLine)
    {
        if (segment.Length > 0)
        {
            onLine(segment.ToString());
            segment.Clear();
        }
    }

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

        ApplyEnvironment(startInfo, runOptions.Environment);
        ApplyEnvironment(startInfo, _options.EnvironmentOverrides);

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
            var errorTask = ReadErrorAsync(process, runOptions, timeoutSource, timeout, linked.Token);
            var inputTask = WriteInputAsync(process, runOptions.StandardInput, linked.Token);
            await Task.WhenAll(outputTask, errorTask, inputTask).ConfigureAwait(false);
            var output = outputTask.Result;
            var error = errorTask.Result;
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            if (!IsAccepted(process.ExitCode, runOptions))
            {
                throw new GitCommandFailedException(arguments, process.ExitCode, error, Encoding.UTF8.GetString(output));
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

    private async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamCoreAsync(
        IReadOnlyList<string> arguments,
        GitRunOptions runOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var timeout = runOptions.Timeout ?? _options.EffectiveTimeout;
        using var timeoutSource = new CancellationTokenSource(Timeout.InfiniteTimeSpan);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        using var process = await Task.Run(() => Start(arguments, runOptions), CancellationToken.None).ConfigureAwait(false);
        var errorTask = process.StandardError.ReadToEndAsync(linked.Token);
        await WriteInputAsync(process, null, linked.Token).ConfigureAwait(false);
        var output = process.StandardOutput.BaseStream;
        var buffer = ArrayPool<byte>.Shared.Rent(ReadBufferSize);
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

                yield return buffer.AsMemory(0, count);
            }

            timeoutSource.CancelAfter(timeout);
            await FinishStreamAsync(process, errorTask, arguments, runOptions, timeoutSource, timeout, cancellationToken, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            // Early disposal, cancellation, timeout and failures all end here.
            KillQuietly(process);
            ArrayPool<byte>.Shared.Return(buffer);
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

    /// <summary>Feeds stdin and closes it (git must never wait for input). A git that exits before reading everything is not an error here; its exit code tells.</summary>
    private static async Task WriteInputAsync(Process process, byte[]? input, CancellationToken cancellationToken)
    {
        var stream = process.StandardInput.BaseStream;
        try
        {
            if (input is { Length: > 0 })
            {
                await stream.WriteAsync(input, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (IOException)
        {
            // Broken pipe: git stopped reading.
        }
        finally
        {
            try
            {
                stream.Close();
            }
            catch (IOException)
            {
                // Same.
            }
        }
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ReadBufferSize, cancellationToken).ConfigureAwait(false);
        return memory.ToArray();
    }
}
