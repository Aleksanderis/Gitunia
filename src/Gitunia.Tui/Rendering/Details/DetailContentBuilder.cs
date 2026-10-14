using System.Globalization;
using Gitunia.Core.Graph;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;
using Gitunia.Git.Refs.Models;
using Gitunia.Tui.Loading.Commits;
using Gitunia.Tui.Rendering.Styling;

namespace Gitunia.Tui.Rendering.Details;

/// <summary>Turns the selected graph entry and its loaded details into the rows of the detail pane.</summary>
public static class DetailContentBuilder
{
    private const int HashLength = 7;
    private const string TimeFormat = "yyyy-MM-dd HH:mm zzz";
    private const string Indent = "  ";
    private const string FilesPlaceholder = "The file tree is not implemented yet";

    /// <param name="entry">The selected graph row, null when nothing is selected. Its subject and refs show before the details arrive.</param>
    /// <param name="state">The loading state of the commit's details.</param>
    /// <param name="tab">The tab to build the rows of.</param>
    public static DetailContent Build(GraphEntry? entry, CommitDetailsState state, BottomTab tab)
    {
        if (entry is null)
        {
            return DetailContent.EmptyFor(tab);
        }

        var details = state.Commit == entry.Id ? state.Details : null;
        var rows = tab switch
        {
            BottomTab.Details => BuildDetails(entry, state, details),
            BottomTab.Changes => BuildChanges(entry, state, details),
            _ => BuildFilesPlaceholder(),
        };
        var firstFileRow = tab == BottomTab.Changes && details is not null ? 1 : rows.Count;
        return new DetailContent(ShortHash(entry.Id), rows, firstFileRow, details?.Files ?? [], tab, details?.Files.Count);
    }

    /// <summary>Mark and path of a file as plain text, for example <c>R100 old.txt -&gt; new.txt</c>.</summary>
    public static string DescribeFile(CommitFileChange file) => file.OldPath is { } oldPath
        ? $"{Mark(file)} {oldPath} -> {file.Path}"
        : $"{Mark(file)} {file.Path}";

    private static List<DetailRow> BuildDetails(GraphEntry entry, CommitDetailsState state, CommitDetails? details)
    {
        var rows = new List<DetailRow>
        {
            Labelled("commit", new StyledSegment(entry.Id.ToString(), SegmentRole.Hash)),
        };
        if (details is not null)
        {
            AddPeople(rows, details);
            AddParents(rows, details);
            AddRefs(rows, entry.Decorations);
            rows.Add(Blank());
            AddMessage(rows, details.Message);
            return rows;
        }

        AddRefs(rows, entry.Decorations);
        rows.Add(Blank());
        rows.Add(Plain(Indent + entry.Subject));
        rows.Add(Blank());
        rows.Add(Status(entry, state));
        return rows;
    }

    private static List<DetailRow> BuildChanges(GraphEntry entry, CommitDetailsState state, CommitDetails? details)
    {
        if (details is null)
        {
            return [Status(entry, state)];
        }

        var rows = new List<DetailRow> { new([new StyledSegment(FilesHeading(details.Files.Count), SegmentRole.Faint)]) };
        AddFiles(rows, details.Files);
        return rows;
    }

    private static List<DetailRow> BuildFilesPlaceholder() =>
    [
        new([new StyledSegment(FilesPlaceholder, SegmentRole.Faint)]),
    ];

    private static DetailRow Status(GraphEntry entry, CommitDetailsState state) => new([new StyledSegment(
        state.Commit == entry.Id && state.Status == CommitDetailsStatus.Failed
            ? "Could not load the commit (r retries): " + state.Error
            : "Loading...",
        SegmentRole.Faint)]);

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
                new StyledSegment(mark, role),
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
