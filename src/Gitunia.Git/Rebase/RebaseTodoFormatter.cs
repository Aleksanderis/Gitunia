using System.Text;

namespace Gitunia.Git.Rebase;

/// <summary>Writes the todo file git reads: one command per line. Pure.</summary>
public static class RebaseTodoFormatter
{
    private const string Pick = "pick";

    private const string Squash = "squash";

    private const string Fixup = "fixup";

    private const string Drop = "drop";

    private const string AmendCommand = "exec git commit --amend --allow-empty -F";

    private const char Quote = '\'';

    private const string QuoteEscape = "'\\''";

    /// <summary>
    /// A reword is a pick followed by an <c>exec</c> that amends the commit with the message in the file named by
    /// <paramref name="messageFiles"/> at the same index (null for entries that are not a reword). The amend runs with
    /// the user's hooks and signing, and the todo does not need the message on the line.
    /// </summary>
    public static string Format(IReadOnlyList<RebaseTodoEntry> entries, IReadOnlyList<string?> messageFiles)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            switch (entry.Action)
            {
                case RebaseTodoAction.Reword:
                    builder.Append(Pick).Append(' ').Append(entry.Commit).Append('\n');
                    builder.Append(AmendCommand).Append(' ').Append(ShellQuote(messageFiles[index]!)).Append('\n');
                    break;
                case RebaseTodoAction.Pick:
                    builder.Append(Pick).Append(' ').Append(entry.Commit).Append('\n');
                    break;
                case RebaseTodoAction.Squash:
                    builder.Append(Squash).Append(' ').Append(entry.Commit).Append('\n');
                    break;
                case RebaseTodoAction.Fixup:
                    builder.Append(Fixup).Append(' ').Append(entry.Commit).Append('\n');
                    break;
                case RebaseTodoAction.Drop:
                    builder.Append(Drop).Append(' ').Append(entry.Commit).Append('\n');
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entries), entry.Action, "Unknown todo action");
            }
        }

        return builder.ToString();
    }

    /// <summary>Quotes a word for the POSIX shell git runs editors and <c>exec</c> lines through.</summary>
    public static string ShellQuote(string value) =>
        $"{Quote}{value.Replace(Quote.ToString(), QuoteEscape, StringComparison.Ordinal)}{Quote}";
}
