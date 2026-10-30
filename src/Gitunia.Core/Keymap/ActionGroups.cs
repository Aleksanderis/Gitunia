namespace Gitunia.Core.Keymap;

/// <summary>The one small vocabulary of action groups, shared by the palette, the row menu and the help.</summary>
public static class ActionGroups
{
    public static ActionGroup Branch { get; } = new("Branch", 1);

    public static ActionGroup Merge { get; } = new("Merge and rebase", 2);

    public static ActionGroup Tag { get; } = new("Tag", 3);

    public static ActionGroup Remote { get; } = new("Remote", 4);

    public static ActionGroup Worktree { get; } = new("Worktree", 5);

    public static ActionGroup Stash { get; } = new("Stash", 6);

    public static ActionGroup WorkingTree { get; } = new("Working tree", 7);

    public static ActionGroup Copy { get; } = new("Copy", 8);

    public static ActionGroup Diff { get; } = new("Diff", 9);

    public static ActionGroup Details { get; } = new("Details", 10);

    public static ActionGroup Tools { get; } = new("External tools", 11);

    public static ActionGroup Navigate { get; } = new("Navigate", 12);

    public static ActionGroup View { get; } = new("View", 13);

    public static ActionGroup Tabs { get; } = new("Tabs", 14);

    public static ActionGroup General { get; } = new("General", 15);
}
