using Gitunia.Tui.App.Panes;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>What the Changes view needs to stage and unstage from its lists and its diff box.</summary>
/// <param name="Staging">Stages and unstages files, hunks and lines.</param>
/// <param name="Diff">The diff box, which acts on its own actions while it has focus; null in tests that do not need it.</param>
/// <param name="Visual">Visual mode, which the diff box uses for line ranges; null in tests that do not need it.</param>
public sealed record StagingContext(
    StagingActions Staging,
    WorkingDiffController? Diff = null,
    VisualRangeInput? Visual = null);
