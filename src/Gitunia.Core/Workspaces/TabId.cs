namespace Gitunia.Core.Workspaces;

/// <summary>Identifies a tab for the lifetime of the workspace. Ids are allocated by the workspace and never reused.</summary>
/// <param name="Value">The numeric id.</param>
public readonly record struct TabId(int Value)
{
    public override string ToString() => $"tab-{Value}";
}
