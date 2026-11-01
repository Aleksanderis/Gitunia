namespace Gitunia.Core.Input;

/// <summary>What a click on an overlay line may hit, in the order of <see cref="InputRouting.ClickTargets"/>.</summary>
public enum OverlayClickTarget
{
    /// <summary>The settings screen.</summary>
    Settings,

    /// <summary>The conflict editor.</summary>
    ConflictEditor,

    /// <summary>The interactive rebase editor.</summary>
    RebaseEditor,

    /// <summary>The reflog list.</summary>
    Reflog,

    /// <summary>A tab label of the help.</summary>
    HelpTab,

    /// <summary>A row of the command palette.</summary>
    Palette,

    /// <summary>A row of the actions menu.</summary>
    ContextMenu,

    /// <summary>A parent row of the mainline question.</summary>
    Mainline,

    /// <summary>The yes or no button of a confirm dialog.</summary>
    Confirm,
}
