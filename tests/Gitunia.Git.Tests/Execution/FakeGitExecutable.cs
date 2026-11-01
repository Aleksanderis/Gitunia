using System.ComponentModel;
using System.Diagnostics;

namespace Gitunia.Git.Tests.Execution;

/// <summary>A throwaway script standing in for git, so tests can simulate slow, failing or chatty processes.</summary>
public sealed class FakeGitExecutable : IDisposable
{
    // Errno of "Text file busy": exec fails while any process still holds the script open for writing.
    private const int TextFileBusyErrorCode = 26;
    private const string ProbeVariable = "GITUNIA_FAKE_PROBE";
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(30);

    private readonly string _directory;

    private FakeGitExecutable(string directory, string path)
    {
        _directory = directory;
        ExecutablePath = path;
    }

    public string ExecutablePath { get; }

    /// <summary>Creates a script from a body for the current OS: <paramref name="unixBody"/> for sh, <paramref name="windowsBody"/> for cmd.</summary>
    public static FakeGitExecutable Create(string unixBody, string windowsBody)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"gitunia-fake-{Guid.NewGuid().ToString("N")}");
        Directory.CreateDirectory(directory);
        string path;
        if (OperatingSystem.IsWindows())
        {
            path = Path.Combine(directory, "fakegit.cmd");
            // The final exit keeps the status of `set /p` (1 on empty input) from becoming the script's exit code.
            File.WriteAllText(path, $"@echo off\r\n{windowsBody}\r\nexit /b 0\r\n");
        }
        else
        {
            path = Path.Combine(directory, "fakegit");
            File.WriteAllText(path, $"#!/bin/sh\n[ -n \"${ProbeVariable}\" ] && exit 0\n{unixBody}\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            WaitUntilExecutable(path);
        }

        return new FakeGitExecutable(directory, path);
    }

    public static FakeGitExecutable Sleeping() =>
        Create("sleep 30", "ping -n 30 127.0.0.1 >nul");

    /// <summary>Records its process id next to the marker (Unix only), prints 'first', sleeps (30 seconds on Unix, so a test thread starved of CPU still kills it long before; two seconds on Windows, where the test outwaits it), then writes the marker file; a killed process never writes it.</summary>
    public static FakeGitExecutable SleepingThenMarking(string markerPath) =>
        Create($"echo $$ > '{markerPath}.pid.tmp' && mv '{markerPath}.pid.tmp' '{markerPath}.pid'\nprintf first\nsleep 30\necho done > '{markerPath}'", $"<nul set /p =\"first\"\r\nping -n 3 127.0.0.1 >nul\r\necho done > \"{markerPath}\"");

    public static FakeGitExecutable EchoingArgumentsAndLocks() =>
        Create("printf '%s\\n' \"$@\"\nprintf 'LOCKS=%s' \"$GIT_OPTIONAL_LOCKS\"", "echo %*\r\n<nul set /p =\"LOCKS=%GIT_OPTIONAL_LOCKS%\"");

    public static FakeGitExecutable Failing(int exitCode, string message) =>
        Create($"echo '{message}' >&2\nexit {exitCode}", $"echo {message} 1>&2\r\nexit /b {exitCode}");

    public static FakeGitExecutable Printing(string text) =>
        Create($"printf '%s' '{text}'", $"<nul set /p =\"{text}\"");

    public static FakeGitExecutable PrintingThenSleeping(string text) =>
        Create($"printf '%s' '{text}'\nsleep 30", $"<nul set /p =\"{text}\"\r\nping -n 30 127.0.0.1 >nul");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort cleanup of a temp directory.
        }
    }

    /// <summary>
    /// Runs the script once in probe mode (it exits at once). A process forked by a parallel test while the script was being written can hold the file
    /// open for writing until its own exec, which makes the first exec fail with "Text file busy"; once the probe succeeds no writer is left.
    /// </summary>
    private static void WaitUntilExecutable(string path)
    {
        var startInfo = new ProcessStartInfo(path) { UseShellExecute = false };
        startInfo.Environment[ProbeVariable] = "1";
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                using var process = Process.Start(startInfo)!;
                process.WaitForExit();
                return;
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode == TextFileBusyErrorCode && elapsed.Elapsed < ProbeTimeout)
            {
                Thread.Yield();
            }
        }
    }
}
