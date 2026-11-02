using Gitunia.Tui.Rendering.Diff;

namespace Gitunia.Tui.Views.Lists;

/// <summary>Group buttons for a diff that has none.</summary>
public sealed class NoGroupButtons : IGroupButtonSource
{
    public static readonly NoGroupButtons Instance = new();

    /// <inheritdoc />
    public WorkingTreeButtons Buttons() => WorkingTreeButtons.None;
}
