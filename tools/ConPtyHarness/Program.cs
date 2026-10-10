// Windows-only smoke harness: runs an exe under a ConPTY pseudoconsole, sends keys, reads the VT output.
// Usage: ConPtyHarness <exe> <repoPath> [--strict] [--expect-first <text>] [--expect-after-j <text>]
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

if (!OperatingSystem.IsWindows())
{
    Console.WriteLine("Windows only");
    return 2;
}

var exe = args[0];
var repo = args[1];
var strict = args.Contains("--strict");
string Opt(string name, string def) { var i = Array.IndexOf(args, name); return i >= 0 ? args[i + 1] : def; }
var expectFirst = Opt("--expect-first", "SUBJ-LATEST");
var expectAfterJ = Opt("--expect-after-j", string.Empty);
var expectAfterResize = Opt("--expect-after-resize", string.Empty);
var expectAfterG = Opt("--expect-after-G", string.Empty);

var results = new List<(string Name, bool Ok, string Detail)>();
void Check(string name, bool ok, string detail) { results.Add((name, ok, detail)); Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}"); }

using var session = new PtySession(exe, $"\"{exe}\" \"{repo}\"", 100, 20);
var sw = Stopwatch.StartNew();

var firstOk = session.WaitFor(s => s.Contains(expectFirst, StringComparison.Ordinal), 20000);
Check("first frame shows newest commit", firstOk, $"{sw.ElapsedMilliseconds} ms to first frame");
Check("alt screen entered", session.Output.Contains("\e[?1049h", StringComparison.Ordinal), string.Empty);
Check("non-ASCII commit subject rendered as UTF-8", session.Output.Contains("café ✓", StringComparison.Ordinal), string.Empty);

session.Settle(500);
var before = session.Length;
sw.Restart();
session.Send("j");
var jOk = session.WaitFor(_ => session.Length > before, 5000);
var jDetail = $"{sw.ElapsedMilliseconds} ms, +{session.Length - before} bytes";
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
sw.Restart();
session.Resize(60, 15);
var rOk = session.WaitFor(_ => session.Length > before, 5000);
var rDetail = $"{sw.ElapsedMilliseconds} ms, +{session.Length - before} bytes";
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
var exited = session.WaitExit(8000, out var code);
Check("quit key exits cleanly", exited && code == 0, exited ? $"exit code {code}" : "did not exit within 8 s");
session.Settle(300);
var tail = session.Output[Math.Max(0, session.Length - 200)..];
Check("alt screen left on exit", tail.Contains("\e[?1049l", StringComparison.Ordinal), string.Empty);
Check("cursor shown on exit", tail.Contains("\e[?25h", StringComparison.Ordinal), string.Empty);

Console.WriteLine("---- last 300 chars of VT output (escaped) ----");
Console.WriteLine(session.Output[Math.Max(0, session.Length - 300)..].Replace("\e", "<ESC>", StringComparison.Ordinal));

var failed = results.Count(r => !r.Ok);
if (!strict)
{
    // Non-strict: only the core steps decide the exit code.
    var core = new[] { "first frame shows newest commit", "j changes the frame", "quit key exits cleanly" };
    failed = results.Count(r => !r.Ok && core.Contains(r.Name));
}

Console.WriteLine($"{results.Count - results.Count(r => !r.Ok)}/{results.Count} checks passed");
return failed == 0 ? 0 : 1;

internal sealed class PtySession : IDisposable
{
    private readonly nint _hPc;
    private readonly nint _inWrite;
    private readonly nint _outRead;
    private readonly nint _process;
    private readonly StringBuilder _out = new();
    private readonly object _lock = new();
    private readonly Thread _reader;

    public PtySession(string exe, string cmdLine, short cols, short rows)
    {
        if (!Native.CreatePipe(out var inRead, out _inWrite, 0, 0) || !Native.CreatePipe(out _outRead, out var outWrite, 0, 0))
        {
            throw new InvalidOperationException("CreatePipe failed " + Marshal.GetLastWin32Error());
        }

        var hr = Native.CreatePseudoConsole(new Native.Coord { X = cols, Y = rows }, inRead, outWrite, 0, out _hPc);
        if (hr != 0)
        {
            throw new InvalidOperationException("CreatePseudoConsole failed 0x" + hr.ToString("x"));
        }

        nint size = 0;
        Native.InitializeProcThreadAttributeList(0, 1, 0, ref size);
        var attrs = Marshal.AllocHGlobal(size);
        if (!Native.InitializeProcThreadAttributeList(attrs, 1, 0, ref size))
        {
            throw new InvalidOperationException("InitializeProcThreadAttributeList failed " + Marshal.GetLastWin32Error());
        }

        if (!Native.UpdateProcThreadAttribute(attrs, 0, 0x20016, _hPc, nint.Size, 0, 0))
        {
            throw new InvalidOperationException("UpdateProcThreadAttribute failed " + Marshal.GetLastWin32Error());
        }

        var si = new Native.StartupInfoEx { AttributeList = attrs };
        si.StartupInfo.Cb = Marshal.SizeOf<Native.StartupInfoEx>();

        // Without STARTF_USESTDHANDLES (handles left NULL) the child inherited this process's redirected stdio
        // (CI pipes) instead of the pseudoconsole: Console.IsInputRedirected was true in the child.
        si.StartupInfo.Flags = 0x100;
        if (!Native.CreateProcess(null, cmdLine, 0, 0, false, 0x80000, 0, null, ref si, out var pi))
        {
            throw new InvalidOperationException("CreateProcess failed " + Marshal.GetLastWin32Error() + " for " + exe);
        }

        _process = pi.Process;
        Native.CloseHandle(pi.Thread);
        Native.CloseHandle(inRead);
        Native.CloseHandle(outWrite);

        _reader = new Thread(ReadLoop) { IsBackground = true };
        _reader.Start();
    }

    public string Output
    {
        get { lock (_lock) { return _out.ToString(); } }
    }

    public int Length
    {
        get { lock (_lock) { return _out.Length; } }
    }

    public void Send(string keys)
    {
        var b = Encoding.UTF8.GetBytes(keys);
        Native.WriteFile(_inWrite, b, (uint)b.Length, out _, 0);
    }

    public void Resize(short cols, short rows) => Native.ResizePseudoConsole(_hPc, new Native.Coord { X = cols, Y = rows });

    public bool WaitFor(Func<string, bool> pred, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (pred(Output))
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return pred(Output);
    }

    /// <summary>Waits until no new output arrived for the given quiet period.</summary>
    public void Settle(int quietMs)
    {
        var last = Length;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < quietMs)
        {
            Thread.Sleep(20);
            if (Length != last)
            {
                last = Length;
                sw.Restart();
            }
        }
    }

    public bool WaitExit(int timeoutMs, out uint code)
    {
        code = 0;
        if (Native.WaitForSingleObject(_process, (uint)timeoutMs) != 0)
        {
            return false;
        }

        Native.GetExitCodeProcess(_process, out code);
        return true;
    }

    public void Dispose()
    {
        Native.TerminateProcess(_process, 1);
        Native.ClosePseudoConsole(_hPc);
    }

    private void ReadLoop()
    {
        var buf = new byte[8192];
        var dec = Encoding.UTF8.GetDecoder();
        var chars = new char[8192];
        while (Native.ReadFile(_outRead, buf, (uint)buf.Length, out var n, 0) && n > 0)
        {
            var c = dec.GetChars(buf, 0, (int)n, chars, 0);
            lock (_lock)
            {
                _out.Append(chars, 0, c);
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
    public static extern bool CreatePipe(out nint read, out nint write, nint attrs, uint size);

    [DllImport("kernel32.dll")]
    public static extern int CreatePseudoConsole(Coord size, nint input, nint output, uint flags, out nint hpc);

    [DllImport("kernel32.dll")]
    public static extern int ResizePseudoConsole(nint hpc, Coord size);

    [DllImport("kernel32.dll")]
    public static extern void ClosePseudoConsole(nint hpc);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool InitializeProcThreadAttributeList(nint list, int count, int flags, ref nint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool UpdateProcThreadAttribute(nint list, uint flags, nint attribute, nint value, nint size, nint prev, nint returnSize);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcess(string? app, string cmd, nint pa, nint ta, bool inherit, uint flags, nint env, string? dir, ref StartupInfoEx si, out ProcessInformation pi);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteFile(nint h, byte[] buf, uint count, out uint written, nint overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadFile(nint h, byte[] buf, uint count, out uint read, nint overlapped);

    [DllImport("kernel32.dll")]
    public static extern uint WaitForSingleObject(nint h, uint ms);

    [DllImport("kernel32.dll")]
    public static extern bool GetExitCodeProcess(nint h, out uint code);

    [DllImport("kernel32.dll")]
    public static extern bool TerminateProcess(nint h, uint code);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(nint h);
}
