// Windows-only smoke harness: runs an exe under a ConPTY pseudoconsole, sends keys, reads the VT output.
// Usage: ConPtyHarness <exe> <repositoryPath> [--strict] [--expect-first <text>] [--expect-after-j <text>]
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

if (!OperatingSystem.IsWindows())
{
    Console.WriteLine("Windows only");
    return 2;
}

var executablePath = args[0];
var repositoryPath = args[1];
var strict = args.Contains("--strict");
string Opt(string name, string defaultValue) { var index = Array.IndexOf(args, name); return index >= 0 ? args[index + 1] : defaultValue; }
var expectFirst = Opt("--expect-first", "SUBJ-LATEST");
var expectAfterJ = Opt("--expect-after-j", string.Empty);
var expectAfterResize = Opt("--expect-after-resize", string.Empty);
var expectAfterG = Opt("--expect-after-G", string.Empty);

var results = new List<(string Name, bool Ok, string Detail)>();
void Check(string name, bool ok, string detail) { results.Add((name, ok, detail)); Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}"); }

using var session = new PtySession(executablePath, $"\"{executablePath}\" \"{repositoryPath}\"", 100, 20);
var stopwatch = Stopwatch.StartNew();

var firstOk = session.WaitFor(output => output.Contains(expectFirst, StringComparison.Ordinal), 20000);
Check("first frame shows newest commit", firstOk, $"{stopwatch.ElapsedMilliseconds} ms to first frame");
Check("alt screen entered", session.Output.Contains("\e[?1049h", StringComparison.Ordinal), string.Empty);
Check("non-ASCII commit subject rendered as UTF-8", session.Output.Contains("café ✓", StringComparison.Ordinal), string.Empty);

session.Settle(500);
var before = session.Length;
stopwatch.Restart();
session.Send("j");
var jOk = session.WaitFor(_ => session.Length > before, 5000);
var jDetail = $"{stopwatch.ElapsedMilliseconds} ms, +{session.Length - before} bytes";
if (expectAfterJ.Length > 0)
{
    session.Settle(300);
    var found = session.Output[before..].Contains(expectAfterJ, StringComparison.Ordinal);
    jOk &= found;
    jDetail += $", contains '{expectAfterJ}': {found}";
}

Check("j changes the frame", jOk, jDetail);

session.Settle(300);
before = session.Length;
stopwatch.Restart();
session.Resize(60, 15);
var rOk = session.WaitFor(_ => session.Length > before, 5000);
var rDetail = $"{stopwatch.ElapsedMilliseconds} ms, +{session.Length - before} bytes";
if (expectAfterResize.Length > 0)
{
    session.Settle(300);
    var found = session.Output[before..].Contains(expectAfterResize, StringComparison.Ordinal);
    rOk &= found;
    rDetail += $", app redrew ('{expectAfterResize}'): {found}";
}

Check("resize triggers a redraw", rOk, rDetail);

if (strict)
{
    // Ctrl-D (0x04) must arrive as a plain input byte (ENABLE_PROCESSED_INPUT off); Ctrl-C (0x03) is used below as the quit key,
    // which proves it is delivered as input data rather than raising a console control event.
    session.Settle(300);
    before = session.Length;
    session.Send("\u0004");
    Check("Ctrl-D delivered as input and scrolls", session.WaitFor(_ => session.Length > before, 5000), $"+{session.Length - before} bytes");
    session.Settle(300);
    before = session.Length;
    session.Send("G");
    var gOk = session.WaitFor(_ => session.Length > before, 5000);
    if (expectAfterG.Length > 0)
    {
        session.Settle(300);
        var found = session.Output[before..].Contains(expectAfterG, StringComparison.Ordinal);
        gOk &= found;
    }

    Check("G jumps to the last commit", gOk, $"+{session.Length - before} bytes");
}

session.Settle(300);
session.Send(strict ? "\u0003" : "q");
var exited = session.WaitExit(8000, out var exitCode);
Check("quit key exits cleanly", exited && exitCode == 0, exited ? $"exit code {exitCode}" : "did not exit within 8 s");
session.Settle(300);
var tail = session.Output[Math.Max(0, session.Length - 200)..];
Check("alt screen left on exit", tail.Contains("\e[?1049l", StringComparison.Ordinal), string.Empty);
Check("cursor shown on exit", tail.Contains("\e[?25h", StringComparison.Ordinal), string.Empty);

Console.WriteLine("---- last 300 chars of VT output (escaped) ----");
Console.WriteLine(session.Output[Math.Max(0, session.Length - 300)..].Replace("\e", "<ESC>", StringComparison.Ordinal));

var failed = results.Count(result => !result.Ok);
if (!strict)
{
    // Non-strict: only the core steps decide the exit code.
    var core = new[] { "first frame shows newest commit", "j changes the frame", "quit key exits cleanly" };
    failed = results.Count(result => !result.Ok && core.Contains(result.Name));
}

Console.WriteLine($"{results.Count - results.Count(result => !result.Ok)}/{results.Count} checks passed");
return failed == 0 ? 0 : 1;

internal sealed class PtySession : IDisposable
{
    private readonly nint _pseudoConsole;
    private readonly nint _inputWrite;
    private readonly nint _outputRead;
    private readonly nint _process;
    private readonly StringBuilder _outputBuffer = new();
    private readonly object _lock = new();
    private readonly Thread _reader;

    public PtySession(string executablePath, string commandLine, short columns, short rows)
    {
        if (!Native.CreatePipe(out var inputRead, out _inputWrite, 0, 0) || !Native.CreatePipe(out _outputRead, out var outputWrite, 0, 0))
        {
            throw new InvalidOperationException("CreatePipe failed " + Marshal.GetLastWin32Error());
        }

        var result = Native.CreatePseudoConsole(new Native.Coord { X = columns, Y = rows }, inputRead, outputWrite, 0, out _pseudoConsole);
        if (result != 0)
        {
            throw new InvalidOperationException("CreatePseudoConsole failed 0x" + result.ToString("x"));
        }

        nint size = 0;
        Native.InitializeProcThreadAttributeList(0, 1, 0, ref size);
        var attributeList = Marshal.AllocHGlobal(size);
        if (!Native.InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
        {
            throw new InvalidOperationException("InitializeProcThreadAttributeList failed " + Marshal.GetLastWin32Error());
        }

        if (!Native.UpdateProcThreadAttribute(attributeList, 0, 0x20016, _pseudoConsole, nint.Size, 0, 0))
        {
            throw new InvalidOperationException("UpdateProcThreadAttribute failed " + Marshal.GetLastWin32Error());
        }

        var startupInfo = new Native.StartupInfoEx { AttributeList = attributeList };
        startupInfo.StartupInfo.Cb = Marshal.SizeOf<Native.StartupInfoEx>();

        // Without STARTF_USESTDHANDLES (handles left NULL) the child inherited this process's redirected stdio
        // (CI pipes) instead of the pseudoconsole: Console.IsInputRedirected was true in the child.
        startupInfo.StartupInfo.Flags = 0x100;
        if (!Native.CreateProcess(null, commandLine, 0, 0, false, 0x80000, 0, null, ref startupInfo, out var processInformation))
        {
            throw new InvalidOperationException("CreateProcess failed " + Marshal.GetLastWin32Error() + " for " + executablePath);
        }

        _process = processInformation.Process;
        Native.CloseHandle(processInformation.Thread);
        Native.CloseHandle(inputRead);
        Native.CloseHandle(outputWrite);

        _reader = new Thread(ReadLoop) { IsBackground = true };
        _reader.Start();
    }

    public string Output
    {
        get { lock (_lock) { return _outputBuffer.ToString(); } }
    }

    public int Length
    {
        get { lock (_lock) { return _outputBuffer.Length; } }
    }

    public void Send(string keys)
    {
        var bytes = Encoding.UTF8.GetBytes(keys);
        Native.WriteFile(_inputWrite, bytes, (uint)bytes.Length, out _, 0);
    }

    public void Resize(short columns, short rows) => Native.ResizePseudoConsole(_pseudoConsole, new Native.Coord { X = columns, Y = rows });

    public bool WaitFor(Func<string, bool> predicate, int timeoutMilliseconds)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
        {
            if (predicate(Output))
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return predicate(Output);
    }

    /// <summary>Waits until no new output arrived for the given quiet period.</summary>
    public void Settle(int quietMilliseconds)
    {
        var last = Length;
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < quietMilliseconds)
        {
            Thread.Sleep(20);
            if (Length != last)
            {
                last = Length;
                stopwatch.Restart();
            }
        }
    }

    public bool WaitExit(int timeoutMilliseconds, out uint exitCode)
    {
        exitCode = 0;
        if (Native.WaitForSingleObject(_process, (uint)timeoutMilliseconds) != 0)
        {
            return false;
        }

        Native.GetExitCodeProcess(_process, out exitCode);
        return true;
    }

    public void Dispose()
    {
        Native.TerminateProcess(_process, 1);
        Native.ClosePseudoConsole(_pseudoConsole);
    }

    private void ReadLoop()
    {
        var buffer = new byte[8192];
        var decoder = Encoding.UTF8.GetDecoder();
        var chars = new char[8192];
        while (Native.ReadFile(_outputRead, buffer, (uint)buffer.Length, out var bytesRead, 0) && bytesRead > 0)
        {
            var charCount = decoder.GetChars(buffer, 0, (int)bytesRead, chars, 0);
            lock (_lock)
            {
                _outputBuffer.Append(chars, 0, charCount);
            }
        }
    }
}

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Coord
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct StartupInfo
    {
        public int Cb;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public short ShowWindow, Reserved2;
        public nint Reserved2Ptr, StdInput, StdOutput, StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public nint AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessInformation
    {
        public nint Process;
        public nint Thread;
        public int ProcessId;
        public int ThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CreatePipe(out nint read, out nint write, nint attributeList, uint size);

    [DllImport("kernel32.dll")]
    public static extern int CreatePseudoConsole(Coord size, nint input, nint output, uint flags, out nint pseudoConsole);

    [DllImport("kernel32.dll")]
    public static extern int ResizePseudoConsole(nint pseudoConsole, Coord size);

    [DllImport("kernel32.dll")]
    public static extern void ClosePseudoConsole(nint pseudoConsole);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool InitializeProcThreadAttributeList(nint list, int count, int flags, ref nint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool UpdateProcThreadAttribute(nint list, uint flags, nint attribute, nint value, nint size, nint previousValue, nint returnSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcess(string? application, string commandLine, nint processAttributes, nint threadAttributes, bool inheritHandles, uint flags, nint environment, string? currentDirectory, ref StartupInfoEx startupInfo, out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteFile(nint handle, byte[] buffer, uint count, out uint written, nint overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadFile(nint handle, byte[] buffer, uint count, out uint read, nint overlapped);

    [DllImport("kernel32.dll")]
    public static extern uint WaitForSingleObject(nint handle, uint milliseconds);

    [DllImport("kernel32.dll")]
    public static extern bool GetExitCodeProcess(nint handle, out uint exitCode);

    [DllImport("kernel32.dll")]
    public static extern bool TerminateProcess(nint handle, uint exitCode);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(nint handle);
}
