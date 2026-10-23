namespace Gitunia.Core.Sidebar;

/// <summary>Groups branch rows by their <c>/</c>-separated prefix into nested folder rows (pure, no git).</summary>
public static class SidebarFolders
{
    private const char Separator = '/';

    /// <summary>The key of a prefix folder in <see cref="SidebarState.ToggledFolders"/>.</summary>
    public static string KeyOf(SidebarSection section, string path) => $"{section}:{path}";

    /// <summary>
    /// The rows with folders: each branch below its prefix folders (in first-seen order), the entries of a closed
    /// folder left out. The branch keeps its full name as label; below a folder it also gets a short name.
    /// </summary>
    /// <param name="branches">The branch rows, already filtered and sorted.</param>
    /// <param name="state">The sidebar state, for the filter and the toggled folders.</param>
    public static IEnumerable<SidebarRow> Group(IReadOnlyList<SidebarRow> branches, SidebarState state)
    {
        var entries = branches.Select(row => new Entry(row, row.Label.Split(Separator))).ToList();
        var rows = new List<SidebarRow>();
        Emit(entries, 0, string.Empty, state, rows);

        return rows;
    }

    private static void Emit(
        List<Entry> entries,
        int depth,
        string parentPath,
        SidebarState state,
        List<SidebarRow> rows)
    {
        var folders = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        var slots = new List<object>();
        foreach (var entry in entries)
        {
            if (entry.Segments.Length - 1 == depth)
            {
                slots.Add(entry);
                continue;
            }

            var name = entry.Segments[depth];
            if (!folders.TryGetValue(name, out var members))
            {
                members = [];
                folders[name] = members;
                slots.Add(name);
            }

            members.Add(entry);
        }

        foreach (var slot in slots)
        {
            if (slot is Entry leaf)
            {
                rows.Add(depth == 0 ? leaf.Row : leaf.Row with { Depth = depth, ShortName = leaf.Segments[^1] });
                continue;
            }

            EmitFolder((string)slot, folders[(string)slot], depth, parentPath, state, rows);
        }
    }

    private static void EmitFolder(
        string name,
        List<Entry> members,
        int depth,
        string parentPath,
        SidebarState state,
        List<SidebarRow> rows)
    {
        var path = parentPath.Length == 0 ? name : $"{parentPath}{Separator}{name}";
        var section = members[0].Row.Section;
        var key = KeyOf(section, path);
        var isOpen = state.HasFilter || IsOpenByDefault(members, section, depth) != state.ToggledFolders.Contains(key);
        rows.Add(new SidebarRow(
            SidebarRowKind.Folder,
            section,
            name,
            string.Empty,
            null,
            false,
            isOpen,
            members.Count,
            null,
            null,
            Depth: depth,
            Key: key));
        if (isOpen)
        {
            Emit(members, depth + 1, path, state, rows);
        }
    }

    // The folder holding the current branch is open; so is a remote's own folder (the remote name), which only groups.
    private static bool IsOpenByDefault(List<Entry> members, SidebarSection section, int depth) =>
        (section == SidebarSection.Remotes && depth == 0) || members.Any(member => member.Row.IsCurrent);

    private sealed record Entry(SidebarRow Row, string[] Segments);
}
