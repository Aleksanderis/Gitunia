namespace Gitunia.Core.Keymap;

/// <summary>The one small vocabulary of action groups, shared by the palette, the row menu and which-key.</summary>
public static class ActionGroups
{
    public static ActionGroup Branch { get; } = new("Branch", 1, "branch operations");

    public static ActionGroup Merge { get; } = new("Merge and rebase", 2, "merge, rebase, reset, pick");

    public static ActionGroup Tag { get; } = new("Tag", 3, "tag operations");

    public static ActionGroup Worktree { get; } = new("Worktree", 4, "worktree operations");

    public static ActionGroup Stash { get; } = new("Stash", 5, "stash operations");

    public static ActionGroup WorkingTree { get; } = new("Working tree", 6);

    public static ActionGroup Copy { get; } = new("Copy", 7);

    public static ActionGroup Diff { get; } = new("Diff", 8, "diff view options");

    public static ActionGroup Details { get; } = new("Details", 9);

    public static ActionGroup Tools { get; } = new("External tools", 10, "external tools");

    public static ActionGroup Navigate { get; } = new("Navigate", 11);

    public static ActionGroup View { get; } = new("View", 12);

    public static ActionGroup Tabs { get; } = new("Tabs", 13);

    public static ActionGroup General { get; } = new("General", 14);
}
