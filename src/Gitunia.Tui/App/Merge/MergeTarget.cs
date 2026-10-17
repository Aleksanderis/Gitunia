namespace Gitunia.Tui.App.Merge;

/// <summary>What one merge acts on, fixed when the key is pressed.</summary>
/// <param name="Revision">What is passed to git: a full branch ref or a commit hash.</param>
/// <param name="Label">How the texts name it.</param>
public sealed record MergeTarget(string Revision, string Label);
