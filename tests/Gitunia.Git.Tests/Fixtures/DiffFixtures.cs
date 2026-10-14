namespace Gitunia.Git.Tests.Fixtures;

/// <summary>Patch text in the shape real <c>git diff --no-color</c> prints it, one constant per odd case.</summary>
public static class DiffFixtures
{
    public static string TwoHunks => Lines(
        "diff --git a/src/app.txt b/src/app.txt",
        "index 1111111..2222222 100644",
        "--- a/src/app.txt",
        "+++ b/src/app.txt",
        "@@ -3,4 +3,5 @@ void Run()",
        " context before",
        "-old",
        "+new one",
        "+new two",
        " context after",
        " tail",
        "@@ -20 +21 @@",
        "-single",
        "+single changed");

    public static string RenameWithEdit => Lines(
        "diff --git a/old name.txt b/new dir/new name.txt",
        "similarity index 88%",
        "rename from old name.txt",
        "rename to new dir/new name.txt",
        "index 1111111..2222222 100644",
        "--- a/old name.txt\t",
        "+++ b/new dir/new name.txt\t",
        "@@ -1,2 +1,2 @@",
        " same",
        "-before",
        "+after");

    public static string PureRename => Lines(
        "diff --git a/a.txt b/b.txt",
        "similarity index 100%",
        "rename from a.txt",
        "rename to b.txt");

    public static string Copy => Lines(
        "diff --git a/a.txt b/a copy.txt",
        "similarity index 100%",
        "copy from a.txt",
        "copy to a copy.txt");

    public static string BinaryDiffers => Lines(
        "diff --git a/img/logo.png b/img/logo.png",
        "index 1111111..2222222 100644",
        "Binary files a/img/logo.png and b/img/logo.png differ");

    public static string BinaryPatch => Lines(
        "diff --git a/img/logo.png b/img/logo.png",
        "index 1111111..2222222 100644",
        "GIT binary patch",
        "literal 5",
        "Mc${NM&Pn-0006PZ0RR91",
        string.Empty,
        "literal 3",
        "Kc${NM&Pn-0006PZ",
        string.Empty);

    public static string ModeChange => Lines(
        "diff --git a/run it.sh b/run it.sh",
        "old mode 100644",
        "new mode 100755");

    public static string NoNewlineBothSides => Lines(
        "diff --git a/eol.txt b/eol.txt",
        "index 1111111..2222222 100644",
        "--- a/eol.txt",
        "+++ b/eol.txt",
        "@@ -1,2 +1,2 @@",
        " first",
        "-last",
        "\\ No newline at end of file",
        "+last changed",
        "\\ No newline at end of file");

    public static string AddedAndDeleted => Lines(
        "diff --git a/new.txt b/new.txt",
        "new file mode 100644",
        "index 0000000..1111111",
        "--- /dev/null",
        "+++ b/new.txt",
        "@@ -0,0 +1,2 @@",
        "+one",
        "+two",
        "diff --git a/gone.txt b/gone.txt",
        "deleted file mode 100755",
        "index 1111111..0000000",
        "--- a/gone.txt",
        "+++ /dev/null",
        "@@ -1 +0,0 @@",
        "-bye");

    public static string OddPaths => Lines(
        "diff --git \"a/tab\\there.txt\" \"b/tab\\there.txt\"",
        "index 1111111..2222222 100644",
        "--- \"a/tab\\there.txt\"",
        "+++ \"b/tab\\there.txt\"",
        "@@ -1 +1 @@",
        "-x",
        "+y",
        "diff --git a/with space/日本 ü.txt b/with space/日本 ü.txt",
        "index 1111111..2222222 100644",
        "--- a/with space/日本 ü.txt\t",
        "+++ b/with space/日本 ü.txt\t",
        "@@ -1 +1 @@",
        "-x",
        "+y",
        "diff --git \"a/quote\\\"d.txt\" \"b/quote\\\"d.txt\"",
        "old mode 100644",
        "new mode 100755");

    public static string HeaderLookalikes => Lines(
        "diff --git a/notes.md b/notes.md",
        "index 1111111..2222222 100644",
        "--- a/notes.md",
        "+++ b/notes.md",
        "@@ -1,3 +1,3 @@",
        "---- a/fake",
        "++++ b/fake",
        "-diff --git a/x b/x",
        "+@@ -1 +1 @@",
        " done");

    public static string ShowWithCombined => Lines(
        "commit 1111111111111111111111111111111111111111",
        "Author: Test <test@example.com>",
        "Date:   Thu Jan 1 00:00:00 2026 +0000",
        string.Empty,
        "    diff --git is only text in a message",
        string.Empty,
        "diff --cc merged.txt",
        "index 1111111,2222222..3333333",
        "--- a/merged.txt",
        "+++ b/merged.txt",
        "@@@ -1,2 -1,2 +1,2 @@@",
        "  same",
        "++merged",
        "diff --git a/kept.txt b/kept.txt",
        "index 1111111..2222222 100644",
        "--- a/kept.txt",
        "+++ b/kept.txt",
        "@@ -1 +1 @@",
        "-a",
        "+b");

    public static string TruncatedHunk => Lines(
        "diff --git a/cut.txt b/cut.txt",
        "index 1111111..2222222 100644",
        "--- a/cut.txt",
        "+++ b/cut.txt",
        "@@ -1,3 +1,3 @@",
        " keep",
        "-gone");

    private static string Lines(params string[] lines) => $"{string.Join('\n', lines)}\n";
}
