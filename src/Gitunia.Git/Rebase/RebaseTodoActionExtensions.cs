namespace Gitunia.Git.Rebase;

/// <summary>Which todo actions stand for a commit the user can change and which are structure of the todo.</summary>
public static class RebaseTodoActionExtensions
{
    /// <summary>True for <c>label</c> and <c>reset</c>: lines that name no commit.</summary>
    public static bool IsStructure(this RebaseTodoAction action) =>
        action is RebaseTodoAction.Label or RebaseTodoAction.Reset;

    /// <summary>True for the five actions the user chooses between (pick, reword, squash, fixup, drop).</summary>
    public static bool IsEditable(this RebaseTodoAction action) =>
        action is RebaseTodoAction.Pick
            or RebaseTodoAction.Reword
            or RebaseTodoAction.Squash
            or RebaseTodoAction.Fixup
            or RebaseTodoAction.Drop;
}
