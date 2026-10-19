namespace Gitunia.Tui.App.Creation;

/// <summary>Which prompt of the clone and init flow is open.</summary>
public enum CreationPromptKind
{
    None,
    CloneSource,
    CloneTarget,
    InitDirectory,
}
