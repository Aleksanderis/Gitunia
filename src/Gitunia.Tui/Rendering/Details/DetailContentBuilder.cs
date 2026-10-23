using System.Globalization;
using Gitunia.Core.Diff;
using Gitunia.Core.FileTree;
using Gitunia.Core.Graph;
using Gitunia.Core.IssueTracker;
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
    private const string EmptyTree = "The commit has no files";
    private const string NoMatch = "No file matches the filter";
    private const int TreeIndent = 2;

    /// <param name="entry">The selected graph row, null when nothing is selected. Its subject and refs show before the details arrive.</param>
    /// <param name="state">The loading state of the commit's details.</param>
    /// <param name="tab">The tab to build the rows of.</param>
    /// <param name="tree">The file tree to list on the Files tab; null shows it as loading.</param>
    /// <param name="pathStyle">How the Changes tab shows the paths of the files.</param>
    /// <param name="links">The tracker rules of the repository; references in the message become links.</param>
    /// <param name="collapsedFolders">Folders of the tree style that are closed, null when all are open.</param>
    public static DetailContent Build(
        GraphEntry? entry,
        CommitDetailsState state,
        BottomTab tab,
        FileTreeView? tree = null,
        FilePathStyle pathStyle = FilePathStyle.FullPath,
        IssueTrackerLinks? links = null,
        IReadOnlySet<string>? collapsedFolders = null) =>
        BuildRows(entry, state, tab, tree, pathStyle, links ?? IssueTrackerLinks.None, collapsedFolders) with
        {
            PathStyle = pathStyle,
        };

    /// <summary>Mark and path of a file as plain text, for example <c>R100 old.txt -&gt; new.txt</c>.</summary>
    public static string DescribeFile(CommitFileChange file) => file.OldPath is { } oldPath
        ? $"{Mark(file)} {oldPath} -> {file.Path}"
        : $"{Mark(file)} {file.Path}";

    private static DetailContent BuildRows(
        GraphEntry? entry,
        CommitDetailsState state,
        BottomTab tab,
        FileTreeView? tree,
        FilePathStyle pathStyle,
        IssueTrackerLinks links,
        IReadOnlySet<string>? collapsedFolders)
    {
        if (entry is null)
        {
            return DetailContent.EmptyFor(tab);
        }

        var details = state.Commit == entry.Id ? state.Details : null;
        var rows = tab switch
        {
            BottomTab.Details => BuildDetails(entry, state, details, links),
            BottomTab.Changes => BuildChanges(entry, state, details, pathStyle, collapsedFolders),
            _ => BuildFiles(entry, tree),
        };
        if (tab == BottomTab.Files)
        {
            var shown = tree is { State.Commit: { } commit } && commit == entry.Id ? tree : null;
            var treeRows = shown is { State.Status: CommitDetailsStatus.Loaded } ? shown.Rows : [];

            return new DetailContent(
                ShortHash(entry.Id),
                rows,
                rows.Count,
                details?.Files ?? [],
                tab,
                shown?.State.Model?.FileCount)
            {
                Tree = treeRows,
            };
        }

        var firstFileRow = tab == BottomTab.Changes && details is not null ? FirstFileRowOf(rows) : rows.Count;
        return new DetailContent(ShortHash(entry.Id), rows, firstFileRow, details?.Files ?? [], tab, details?.Files.Count);
    }

    private static List<DetailRow> BuildDetails(
        GraphEntry entry,
        CommitDetailsState state,
        CommitDetails? details,
        IssueTrackerLinks links)
    {
        var rows = new List<DetailRow>
        {
            Labelled("commit", new StyledSegment(entry.Id.ToString(), SegmentRole.Hash)),
        };
        if (details is not null)
        {
            AddPeople(rows, details);
            if (details.IsComparison)
            {
                rows.Add(Labelled("compare", new StyledSegment(CompareText(details), SegmentRole.Hash)));
            }
            else
            {
                AddParents(rows, details);
            }

            AddRefs(rows, entry.Decorations);
            rows.Add(Blank());
            AddMessage(rows, details.Message, links);
            return rows;
        }

        AddRefs(rows, entry.Decorations);
        rows.Add(Blank());
        rows.Add(Plain(Indent + entry.Subject));
        rows.Add(Blank());
        rows.Add(Status(entry, state));
        return rows;
    }

    // The tree style puts folder rows above the first file; the cursor lands on the first file.
    private static int FirstFileRowOf(List<DetailRow> rows)
    {
        var index = rows.FindIndex(row => row.FileIndex >= 0);

        return index < 0 ? Math.Min(1, rows.Count) : index;
    }

    private static List<DetailRow> BuildChanges(
        GraphEntry entry,
        CommitDetailsState state,
        CommitDetails? details,
        FilePathStyle pathStyle,
        IReadOnlySet<string>? collapsedFolders)
    {
        if (details is null)
        {
            return [Status(entry, state)];
        }

        var heading = FilesHeading(details.Files.Count) + MergeNote(details) + CompareNote(details);
        var rows = new List<DetailRow> { new([new StyledSegment(heading, SegmentRole.Faint)]) };
        if (pathStyle == FilePathStyle.Tree)
        {
            AddTree(rows, details.Files, collapsedFolders);
        }
        else
        {
            AddFiles(rows, details.Files);
        }

        return rows;
    }

    private static void AddTree(
        List<DetailRow> rows,
        IReadOnlyList<CommitFileChange> files,
        IReadOnlySet<string>? collapsed)
    {
        foreach (var row in ChangesTree.Rows(files, collapsed))
        {
            var indent = new string(' ', row.Depth * TreeIndent);
            if (row.IsFolder)
            {
                var segments = new List<StyledSegment> { new($"{indent}{row.Name}/", SegmentRole.SectionTitle) };
                if (collapsed is not null && collapsed.Contains(row.FolderPath))
                {
                    var count = files.Count(file => FileTreePath.IsUnder(row.FolderPath, file.Path));
                    segments.Add(new StyledSegment($"  {FileCountText(count)}", SegmentRole.Faint));
                }

                rows.Add(new DetailRow(segments, Folder: row.FolderPath));
                continue;
            }

            var file = files[row.FileIndex];
            var mark = Mark(file);
            rows.Add(new DetailRow(
                [
                    new StyledSegment($"{indent}{mark}", FileRole(file)),
                    new StyledSegment($" {TreeName(file, row.Name)}", SegmentRole.Plain),
                ],
                FileIndex: row.FileIndex));
        }
    }

    // A rename shows its old name when that sits in the same folder, else the whole old path.
    private static string TreeName(CommitFileChange file, string name)
    {
        if (file.OldPath is not { } oldPath)
        {
            return name;
        }

        var directory = file.Path[..(file.Path.Length - name.Length)];
        var old = oldPath.StartsWith(directory, StringComparison.Ordinal) && oldPath.IndexOf('/', directory.Length) < 0
            ? oldPath[directory.Length..]
            : oldPath;

        return $"{old} -> {name}";
    }

    private static SegmentRole FileRole(CommitFileChange file) => file.Kind switch
    {
        FileChangeKind.Added => SegmentRole.FileAdded,
        FileChangeKind.Deleted => SegmentRole.FileDeleted,
        _ => SegmentRole.FileChanged,
    };

    private static List<DetailRow> BuildFiles(GraphEntry entry, FileTreeView? tree)
    {
        if (tree is not { State: { Commit: { } commit } state }
            || commit != entry.Id
            || state.Status == CommitDetailsStatus.Loading)
        {
            return [Faint("Loading...")];
        }

        if (state.Status == CommitDetailsStatus.Failed)
        {
            return [Faint($"Could not load the tree (<space>r retries): {state.Error}")];
        }

        if (tree.Rows.Count == 0)
        {
            return [Faint(tree.Filter.Length > 0 ? NoMatch : EmptyTree)];
        }

        return [.. tree.Rows.Select(row => TreeRow(row, tree))];
    }

    private static DetailRow TreeRow(FileTreeRow row, FileTreeView tree)
    {
        var indent = new string(' ', row.Depth * TreeIndent);
        if (row.Kind == FileTreeRowKind.Directory)
        {
            var chevron = row.IsExpanded ? tree.Glyphs.Expanded : tree.Glyphs.Collapsed;
            var segments = new List<StyledSegment>
            {
                new($"{indent}{chevron} ", SegmentRole.Faint),
                new(row.Label, SegmentRole.SectionTitle),
            };
            if (!row.IsExpanded)
            {
                segments.Add(new StyledSegment($"  {FileCountText(row.FileCount)}", SegmentRole.Faint));
            }

            return new DetailRow(segments);
        }

        // Files line up with the names of directories, which start after their chevron.
        var suffix = row.Kind switch
        {
            FileTreeRowKind.Submodule => "  (submodule)",
            FileTreeRowKind.Symlink => $"  (symlink, {FileSizeText.Of(row.Size ?? 0)})",
            FileTreeRowKind.Executable => $"  (executable, {FileSizeText.Of(row.Size ?? 0)})",
            _ => $"  {FileSizeText.Of(row.Size ?? 0)}",
        };

        return new DetailRow([
            new StyledSegment($"{indent}  {row.Label}", SegmentRole.Plain),
            new StyledSegment(suffix, SegmentRole.Faint),
        ]);
    }

    private static string FileCountText(int count) =>
        count == 1 ? "1 file" : $"{count.ToString(CultureInfo.InvariantCulture)} files";

    private static DetailRow Faint(string text) => new([new StyledSegment(text, SegmentRole.Faint)]);

    private static DetailRow Status(GraphEntry entry, CommitDetailsState state) => new([new StyledSegment(
        state.Commit == entry.Id && state.Status == CommitDetailsStatus.Failed
            ? $"Could not load the commit (r retries): {state.Error}"
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
                RefKind.LocalBranch when decoration.IsCurrent => new StyledSegment($"HEAD -> {decoration.Name}", SegmentRole.CurrentBranch),
                RefKind.LocalBranch => new StyledSegment(decoration.Name, SegmentRole.LocalBranch),
                RefKind.RemoteBranch => new StyledSegment(decoration.Name, SegmentRole.RemoteBranch),
                RefKind.Tag => new StyledSegment($"tag: {decoration.Name}", SegmentRole.Tag),
                RefKind.Head => new StyledSegment("HEAD", SegmentRole.CurrentBranch),
                _ => new StyledSegment(decoration.Name, SegmentRole.Faint),
            });
        }

        rows.Add(new DetailRow(segments));
    }

    private static void AddMessage(List<DetailRow> rows, string message, IssueTrackerLinks links)
    {
        foreach (var line in message.Split('\n'))
        {
            var text = line.TrimEnd('\r').Replace('\t', ' ');
            rows.Add(text.Length == 0 ? Blank() : MessageRow(text, links));
        }
    }

    private static DetailRow MessageRow(string text, IssueTrackerLinks links)
    {
        var references = links.Find(text);
        if (references.Count == 0)
        {
            return Plain(Indent + text);
        }

        var segments = new List<StyledSegment> { new(Indent, SegmentRole.Plain) };
        var position = 0;
        foreach (var reference in references)
        {
            if (reference.Start > position)
            {
                segments.Add(new StyledSegment(text[position..reference.Start], SegmentRole.Plain));
            }

            segments.Add(new StyledSegment(text.Substring(reference.Start, reference.Length), SegmentRole.Link));
            position = reference.Start + reference.Length;
        }

        if (position < text.Length)
        {
            segments.Add(new StyledSegment(text[position..], SegmentRole.Plain));
        }

        return new DetailRow(segments, References: references);
    }

    private static void AddFiles(List<DetailRow> rows, IReadOnlyList<CommitFileChange> files)
    {
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            var role = FileRole(file);
            var mark = Mark(file);
            var text = DescribeFile(file);
            rows.Add(new DetailRow(
                [new StyledSegment(mark, role), new StyledSegment(text[mark.Length..], SegmentRole.Plain)],
                new FileRowPath(file.Path, file.OldPath),
                index));
        }
    }

    /// <summary>A merge commit lists its changes against one parent, like the diff pane; name it.</summary>
    private static string MergeNote(CommitDetails details)
    {
        if (details.Parents.Count <= 1 || details.IsComparison)
        {
            return string.Empty;
        }

        var number = (details.ParentIndex + 1).ToString(CultureInfo.InvariantCulture);
        var which = details.ParentIndex == 0 ? "first parent" : $"parent {number}";

        return $" (merge: against {which} {ShortHash(details.Parents[details.ParentIndex])})";
    }

    /// <summary>A comparison names its two ends, older first, like the <c>git diff</c> it shows.</summary>
    private static string CompareNote(CommitDetails details) =>
        details.IsComparison ? $" ({CompareText(details)})" : string.Empty;

    private static string CompareText(CommitDetails details) =>
        $"{ShortHash(details.CompareOlder!.Value)}..{ShortHash(details.CompareNewer!.Value)}";

    private static string FilesHeading(int count) => count switch
    {
        0 => "no files changed",
        1 => "1 file changed",
        _ => $"{count.ToString(CultureInfo.InvariantCulture)} files changed",
    };

    private static string Mark(CommitFileChange file) => file.Kind switch
    {
        FileChangeKind.Added => "A",
        FileChangeKind.Deleted => "D",
        FileChangeKind.Renamed => $"R{file.Similarity?.ToString(CultureInfo.InvariantCulture)}",
        FileChangeKind.Copied => $"C{file.Similarity?.ToString(CultureInfo.InvariantCulture)}",
        FileChangeKind.TypeChanged => "T",
        FileChangeKind.UpdatedButUnmerged => "U",
        _ => "M",
    };

    private static StyledSegment Label(string text) => new(text.PadRight(10), SegmentRole.Faint);

    private static DetailRow Labelled(string label, StyledSegment value) => new([Label(label), value]);

    private static DetailRow Plain(string text) => new([new StyledSegment(text, SegmentRole.Plain)]);

    private static DetailRow Blank() => new([]);
}
