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
    // Native error codes of "file not found" and "path not found": Windows ERROR_FILE_NOT_FOUND/ERROR_PATH_NOT_FOUND, Unix ENOENT.
    private const int FileNotFoundCode = 2;
    private const int PathNotFoundCode = 3;
    private const int ReadBufferSize = 64 * 1024;
    private const int ErrorBufferSize = 4096;
    private const char LineFeed = '\n';
    private const char CarriageReturn = '\r';

    // After git exits, a background child of a hook can still hold the pipes open; a read that stays pending this long
    // after the exit ends the output. The cut only cancels a read that has nothing to deliver, so output git wrote
    // before exiting is never dropped, however late the reader gets scheduled.
    private static readonly TimeSpan DrainGrace = TimeSpan.FromMilliseconds(1500);

    // Absolute bound after the exit for RunAsync, for a background child that keeps writing so the grace never lapses.
    private static readonly TimeSpan PostExitLimit = TimeSpan.FromSeconds(5);

    private readonly GitRunnerOptions _options;
    private int _startedCount;

    public GitRunner(GitRunnerOptions? options = null)
    {
        _options = options ?? new GitRunnerOptions();
    }

    /// <summary>The number of git processes this runner has started; tests compare it before and after an action to count its processes.</summary>
    public int StartedCount => Volatile.Read(ref _startedCount);

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

    // Reports each line-feed or carriage-return terminated segment and restarts the idle timer on every read. A grace
    // cut (git exited, a pipe is still held and nothing arrives) ends the read with what arrived so far.
    private static async Task<string> ReadErrorAsync(
        Stream stream,
        GitRunOptions runOptions,
        CancellationTokenSource timeoutSource,
        TimeSpan timeout,
        Task exitTask,
        CancellationToken capToken,
        CancellationToken hardToken)
    {
        var all = new StringBuilder();
        var segment = new StringBuilder();
        var bytes = new byte[ErrorBufferSize];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(ErrorBufferSize)];
        var decoder = Encoding.UTF8.GetDecoder();
        while (await ReadChunkAsync(stream, bytes, exitTask, capToken, hardToken).ConfigureAwait(false) is var count and > 0)
        {
            if (runOptions.OnStandardErrorLine is not null)
            {
                timeoutSource.CancelAfter(timeout);
            }

            var charCount = decoder.GetChars(bytes, 0, count, chars, 0);
            all.Append(chars, 0, charCount);
            SplitSegments(chars, charCount, segment, runOptions.OnStandardErrorLine);
        }

        if (runOptions.OnStandardErrorLine is { } onLine)
        {
            FlushSegment(segment, onLine);
        }

        return all.ToString();
    }

    private static void SplitSegments(char[] buffer, int count, StringBuilder segment, Action<string>? onLine)
    {
        if (onLine is null)
        {
            return;
        }

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

    private static bool IsDrainCut(CancellationToken drainToken, CancellationToken hardToken) =>
        drainToken.IsCancellationRequested && !hardToken.IsCancellationRequested;

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
        Interlocked.Increment(ref _startedCount);
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is FileNotFoundException or Win32Exception { NativeErrorCode: FileNotFoundCode or PathNotFoundCode })
        {
            process.Dispose();
            throw new GitNotFoundException(_options.Executable, exception);
        }
        catch (Win32Exception exception)
        {
            process.Dispose();
            throw new GitStartFailedException(_options.Executable, exception);
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
            using var drainSource = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
            using var watchSource = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
            var watchTask = CutReadersAfterExitAsync(process, drainSource, watchSource.Token);
            var exitTask = process.WaitForExitAsync(linked.Token);
            var outputTask = ReadAllAsync(process.StandardOutput.BaseStream, exitTask, drainSource.Token, linked.Token);
            var errorTask = ReadErrorAsync(
                process.StandardError.BaseStream,
                runOptions,
                timeoutSource,
                timeout,
                exitTask,
                drainSource.Token,
                linked.Token);
            var inputTask = WriteInputAsync(process, runOptions.StandardInput, drainSource.Token, linked.Token);
            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            await inputTask.ConfigureAwait(false);
            await exitTask.ConfigureAwait(false);
            await watchSource.CancelAsync().ConfigureAwait(false);
            await watchTask.ConfigureAwait(false);
            if (!IsAccepted(process.ExitCode, runOptions))
            {
                throw new GitCommandFailedException(arguments, process.ExitCode, error, Encoding.UTF8.GetString(output));
            }

            return new GitResult(process.ExitCode, output, error);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            ProcessKiller.KillQuietly(process);
            throw new GitTimeoutException(arguments, timeout);
        }
        catch
        {
            ProcessKiller.KillQuietly(process);
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
        using var drainSource = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        using var watchSource = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
        var watchTask = CutReadersAfterExitAsync(process, drainSource, watchSource.Token);
        var exitTask = process.WaitForExitAsync(linked.Token);
        var errorTask = ReadTextAsync(process.StandardError.BaseStream, exitTask, CancellationToken.None, linked.Token);
        await WriteInputAsync(process, null, drainSource.Token, linked.Token).ConfigureAwait(false);
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
                    count = await ReadChunkAsync(output, buffer, exitTask, CancellationToken.None, linked.Token).ConfigureAwait(false);
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
            try
            {
                ProcessKiller.KillQuietly(process);
                await watchSource.CancelAsync().ConfigureAwait(false);
                await watchTask.ConfigureAwait(false);
                await Task.WhenAny(exitTask).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
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

    // The grace period only runs while a read is pending after git exited, never while the consumer is busy, so output
    // that is still in the pipe is not lost to a slow consumer. A cut ends the stream like end of file. The cap token is
    // an absolute bound after the exit (a chatty background child would otherwise keep the grace restarting); it cancels
    // a pending read only, so pass none where a consumer can be busy.
    private static async Task<int> ReadChunkAsync(
        Stream output,
        byte[] buffer,
        Task exitTask,
        CancellationToken capToken,
        CancellationToken hardToken)
    {
        using var readSource = CancellationTokenSource.CreateLinkedTokenSource(hardToken, capToken);
        using var stopSource = CancellationTokenSource.CreateLinkedTokenSource(hardToken);
        var cutTask = CutReadAfterExitAsync(exitTask, readSource, stopSource.Token);
        try
        {
            return await output.ReadAsync(buffer, readSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (IsDrainCut(readSource.Token, hardToken))
        {
            return 0;
        }
        finally
        {
            await stopSource.CancelAsync().ConfigureAwait(false);
            await cutTask.ConfigureAwait(false);
        }
    }

    private static async Task CutReadAfterExitAsync(
        Task exitTask,
        CancellationTokenSource readSource,
        CancellationToken stopToken)
    {
        try
        {
            await exitTask.WaitAsync(stopToken).ConfigureAwait(false);
            readSource.CancelAfter(DrainGrace);
        }
        catch (OperationCanceledException)
        {
            // The read ended first.
        }
    }

    // Once git has exited, gives the readers the grace period, then cuts them.
    private static async Task CutReadersAfterExitAsync(
        Process process,
        CancellationTokenSource drainSource,
        CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(PostExitLimit, cancellationToken).ConfigureAwait(false);
            await drainSource.CancelAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The call ended first.
        }
    }

    private static async Task<string> ReadTextAsync(
        Stream stream,
        Task exitTask,
        CancellationToken capToken,
        CancellationToken hardToken)
    {
        var all = new StringBuilder();
        var bytes = new byte[ErrorBufferSize];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(ErrorBufferSize)];
        var decoder = Encoding.UTF8.GetDecoder();
        while (await ReadChunkAsync(stream, bytes, exitTask, capToken, hardToken).ConfigureAwait(false) is var count and > 0)
        {
            all.Append(chars, 0, decoder.GetChars(bytes, 0, count, chars, 0));
        }

        return all.ToString();
    }

    /// <summary>Feeds stdin and closes it (git must never wait for input). A git that exits before reading everything is not an error here; its exit code tells.</summary>
    private static async Task WriteInputAsync(
        Process process,
        byte[]? input,
        CancellationToken cancellationToken,
        CancellationToken hardToken)
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
        catch (OperationCanceledException) when (IsDrainCut(cancellationToken, hardToken))
        {
            // Git exited without reading its input.
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

    private static async Task<byte[]> ReadAllAsync(
        Stream stream,
        Task exitTask,
        CancellationToken capToken,
        CancellationToken hardToken)
    {
        using var memory = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(ReadBufferSize);
        try
        {
            while (await ReadChunkAsync(stream, buffer, exitTask, capToken, hardToken).ConfigureAwait(false) is var count and > 0)
            {
                memory.Write(buffer, 0, count);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return memory.ToArray();
    }
}
