using Gitunia.Platform.ExternalTools;
using Xunit;

namespace Gitunia.Platform.Tests.ExternalTools;

public class EditorCommandTests
{
    private const string Cwd = "work";
    private const string File = "config.json";

    [Theory]
    [InlineData("code --wait", new[] { "code", "--wait" })]
    [InlineData("  nano  ", new[] { "nano" })]
    [InlineData("\"C:\\P F\\n.exe\" -multiInst", new[] { "C:\\P F\\n.exe", "-multiInst" })]
    [InlineData("C:\\Users\\O'Brien\\code.cmd --wait", new[] { "C:\\Users\\O'Brien\\code.cmd", "--wait" })]
    [InlineData("'/opt/my editor/ed' -w", new[] { "/opt/my editor/ed", "-w" })]
    [InlineData("ed \"\" x", new[] { "ed", "", "x" })]
    [InlineData("ed \"unterminated arg", new[] { "ed", "unterminated arg" })]
    public void GivenAnEditorSetting_WhenSplitting_ThenQuotesGroupWords(string text, string[] expected) =>
        Assert.Equal(expected, EditorCommand.Split(text));

    [Fact]
    public void GivenQuotedPathOnLinux_WhenBuilding_ThenTheWordsAreUsedAsIs()
    {
        var request = EditorCommand.Build(
            "'/opt/my editor/ed' -w", File, Cwd, false, _ => null, _ => false, out var error);

        Assert.Empty(error);
        Assert.Equal("/opt/my editor/ed", request?.FileName);
        Assert.Equal(["-w", File], request?.Arguments);
        Assert.Equal(Cwd, request?.WorkingDirectory);
    }

    [Fact]
    public void GivenCodeOnWindows_WhenBuilding_ThenTheCmdShimOnPathIsFound()
    {
        var shim = Path.Combine("C:\\bin", "code.cmd");

        var request = EditorCommand.Build(
            "code --wait", File, Cwd, true, Env("C:\\other;C:\\bin", ".COM;.EXE;.BAT;.CMD"), IsFile(shim), out _);

        Assert.Equal(shim, request?.FileName, ignoreCase: true);
        Assert.Equal(["--wait", File], request?.Arguments);
    }

    [Fact]
    public void GivenAQuotedFullPathOnWindows_WhenBuilding_ThenTheExeIsUsedWithoutSearchingPath()
    {
        const string exe = "C:\\Program Files\\Notepad++\\notepad++.exe";

        var request = EditorCommand.Build(
            "\"" + exe + "\" -multiInst", File, Cwd, true, Env("C:\\bin", ".EXE"), path => path == exe, out _);

        Assert.Equal(exe, request?.FileName);
        Assert.Equal(["-multiInst", File], request?.Arguments);
    }

    [Fact]
    public void GivenAFullPathWithoutExtensionOnWindows_WhenBuilding_ThenPathextIsTried()
    {
        const string cmd = "D:\\tools\\sub.cmd";

        var request = EditorCommand.Build("D:\\tools\\sub", File, Cwd, true, Env("", ".CMD"), IsFile(cmd), out _);

        Assert.Equal(cmd, request?.FileName, ignoreCase: true);
    }

    [Fact]
    public void GivenAMissingProgramOnWindows_WhenBuilding_ThenTheErrorNamesIt()
    {
        var request = EditorCommand.Build(
            "/usr/bin/vim", File, Cwd, true, Env("C:\\bin", ".EXE"), _ => false, out var error);

        Assert.Null(request);
        Assert.Equal("Could not find the editor /usr/bin/vim", error);
    }

    [Theory]
    [InlineData("code.cmd", "C:\\x\\a&b.json", false)]
    [InlineData("code.cmd", "C:\\x\\a b.json", true)]
    [InlineData("code.exe", "C:\\x\\a&b.json", true)]
    [InlineData("code.bat", "C:\\x\\a&b.json", false)]
    public void GivenCmdMetaCharacters_WhenBuilding_ThenOnlyBatchFilesAreRefused(
        string program,
        string file,
        bool allowed)
    {
        var request = EditorCommand.Build(
            program, file, Cwd, true, Env("C:\\bin", ".EXE;.CMD;.BAT"), _ => true, out var error);

        Assert.Equal(allowed, request is not null);
        Assert.Equal(allowed, error.Length == 0);
    }

    [Fact]
    public void GivenAnEmptySetting_WhenBuilding_ThenItIsRefused()
    {
        var request = EditorCommand.Build("  ", File, Cwd, false, _ => null, _ => false, out var error);

        Assert.Null(request);
        Assert.NotEmpty(error);
    }

    private static Func<string, bool> IsFile(string expected) =>
        path => string.Equals(path, expected, StringComparison.OrdinalIgnoreCase);

    private static Func<string, string?> Env(string path, string pathext) =>
        name => name switch
        {
            "PATH" => path,
            "PATHEXT" => pathext,
            _ => null,
        };
}
