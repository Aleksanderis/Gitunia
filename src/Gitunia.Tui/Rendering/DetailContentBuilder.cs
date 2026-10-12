using System.Globalization;
using Gitunia.Core.Graph;
using Gitunia.Git;
using Gitunia.Tui.Loading;

namespace Gitunia.Tui.Rendering;

/// <summary>Turns the selected graph entry and its loaded details into the rows of the detail pane.</summary>
public static class DetailContentBuilder
{
    private const int HashLength = 7;
    private const string TimeFormat = "yyyy-MM-dd HH:mm zzz";
    private const string Indent = "  ";

    /// <param name="entry">The selected graph row, null when nothing is selected. Its subject and refs show before the details arrive.</param>
    /// <param name="state">The loading state of the commit's details.</param>
    public static DetailContent Build(GraphEntry? entry, CommitDetailsState state)
    {
        if (entry is null)
        {
            return DetailContent.Empty;
        }

        var rows = new List<DetailRow>
        {
            Labelled("commit", new StyledSegment(entry.Id.ToString(), SegmentRole.Hash)),
        };
        if (state.Commit == entry.Id && state.Details is { } details)
        {
            AddPeople(rows, details);
            AddParents(rows, details);
            AddRefs(rows, entry.Decorations);
            rows.Add(Blank());
            AddMessage(rows, details.Message);
            rows.Add(Blank());
            rows.Add(new DetailRow([new StyledSegment(FilesHeading(details.Files.Count), SegmentRole.Faint)]));
            var firstFileRow = rows.Count;
            AddFiles(rows, details.Files);
            return new DetailContent(ShortHash(entry.Id), rows, firstFileRow, details.Files);
        }

        AddRefs(rows, entry.Decorations);
        rows.Add(Blank());
        rows.Add(Plain(Indent + entry.Subject));
        rows.Add(Blank());
        var status = state.Commit == entry.Id && state.Status == CommitDetailsStatus.Failed
            ? "Could not load the commit (r retries): " + state.Error
            : "Loading...";
        rows.Add(new DetailRow([new StyledSegment(status, SegmentRole.Faint)]));
        return new DetailContent(ShortHash(entry.Id), rows, rows.Count, []);
    }

    /// <summary>Mark and path of a file as plain text, for example <c>R100 old.txt -&gt; new.txt</c>.</summary>
    public static string DescribeFile(CommitFileChange file) => file.OldPath is { } oldPath
        ? $"{Mark(file)} {oldPath} -> {file.Path}"
        : $"{Mark(file)} {file.Path}";

    private static string ShortHash(ObjectId id) => id.ToString()[..HashLength];

    private static void AddPeople(List<DetailRow> rows, CommitDetails details)
    {
        rows.Add(Labelled("author", Person(details.AuthorName, details.AuthorEmail, details.AuthorTime)));
        if (details.HasDistinctCommitter)
        {
            rows.Add(Labelled("committer", Person(details.CommitterName, details.CommitterEmail, details.CommitterTime)));
        }
    }

    private static StyledSegment Person(string name, string email, DateTimeOffset time) =>
        new($"{name} <{email}>  {time.ToString(TimeFormat, CultureInfo.InvariantCulture)}", SegmentRole.Plain);

    private static void AddParents(List<DetailRow> rows, CommitDetails details)
    {
        rows.Add(details.Parents.Count == 0
            ? Labelled("parents", new StyledSegment("none (root commit)", SegmentRole.Faint))
            : Labelled("parents", new StyledSegment(string.Join(' ', details.Parents.Select(ShortHash)), SegmentRole.Hash)));
    }

    private static void AddRefs(List<DetailRow> rows, IReadOnlyList<CommitDecoration> decorations)
    {
        if (decorations.Count == 0)
        {
            return;
        }

        var segments = new List<StyledSegment> { Label("refs") };
        for (var index = 0; index < decorations.Count; index++)
        {
            if (index > 0)
            {
                segments.Add(new StyledSegment(", ", SegmentRole.Punctuation));
            }

            var decoration = decorations[index];
            segments.Add(decoration.Kind switch
            {
                RefKind.LocalBranch when decoration.IsCurrent => new StyledSegment("HEAD -> " + decoration.Name, SegmentRole.CurrentBranch),
                RefKind.LocalBranch => new StyledSegment(decoration.Name, SegmentRole.LocalBranch),
                RefKind.RemoteBranch => new StyledSegment(decoration.Name, SegmentRole.RemoteBranch),
                RefKind.Tag => new StyledSegment("tag: " + decoration.Name, SegmentRole.Tag),
                RefKind.Head => new StyledSegment("HEAD", SegmentRole.CurrentBranch),
                _ => new StyledSegment(decoration.Name, SegmentRole.Faint),
            });
        }

        rows.Add(new DetailRow(segments));
    }

    private static void AddMessage(List<DetailRow> rows, string message)
    {
        foreach (var line in message.Split('\n'))
        {
            var text = line.TrimEnd('\r').Replace('\t', ' ');
            rows.Add(text.Length == 0 ? Blank() : Plain(Indent + text));
        }
    }

    private static void AddFiles(List<DetailRow> rows, IReadOnlyList<CommitFileChange> files)
    {
        foreach (var file in files)
        {
            var role = file.Kind switch
            {
                FileChangeKind.Added => SegmentRole.FileAdded,
                FileChangeKind.Deleted => SegmentRole.FileDeleted,
                _ => SegmentRole.FileChanged,
            };
            var mark = Mark(file);
            var text = DescribeFile(file);
            rows.Add(new DetailRow([
                new StyledSegment(Indent + mark, role),
                new StyledSegment(text[mark.Length..], SegmentRole.Plain),
            ]));
        }
    }

    private static string FilesHeading(int count) => count switch
    {
        0 => "no files changed",
        1 => "1 file changed",
        _ => count.ToString(CultureInfo.InvariantCulture) + " files changed",
    };

    private static string Mark(CommitFileChange file) => file.Kind switch
    {
        FileChangeKind.Added => "A",
        FileChangeKind.Deleted => "D",
        FileChangeKind.Renamed => "R" + file.Similarity?.ToString(CultureInfo.InvariantCulture),
        FileChangeKind.Copied => "C" + file.Similarity?.ToString(CultureInfo.InvariantCulture),
        FileChangeKind.TypeChanged => "T",
        FileChangeKind.UpdatedButUnmerged => "U",
        _ => "M",
    };

    private static StyledSegment Label(string text) => new(text.PadRight(10), SegmentRole.Faint);

    private static DetailRow Labelled(string label, StyledSegment value) => new([Label(label), value]);

    private static DetailRow Plain(string text) => new([new StyledSegment(text, SegmentRole.Plain)]);

    private static DetailRow Blank() => new([]);
}
