using System.Text.Json.Serialization;

namespace Gitunia.Core.Config.Models;

/// <summary>
/// One <c>keys</c> entry: binds <see cref="Key"/> to <see cref="Action"/>, or unbinds it when the
/// action is <c>none</c>.
/// </summary>
[JsonConverter(typeof(ConfigKeyEntryConverter))]
public sealed record ConfigKeyEntry
{
    /// <summary>
    /// Mode name (<c>Normal</c>, <c>Visual</c>, <c>Insert</c>, <c>Command</c>, <c>Confirm</c>); Normal when
    /// missing.
    /// </summary>
    public string? Mode { get; init; }

    /// <summary>View name such as <c>Graph</c> or <c>Lists</c>; <c>Global</c> when missing.</summary>
    public string? View { get; init; }

    /// <summary>
    /// Key sequence in the notation of <c>docs/internal/ux-keymap.md</c>, for example <c>J</c>, <c>gg</c> or
    /// <c>&lt;space&gt;o</c>.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>Action id such as <c>list.down</c>, or <c>none</c> to unbind the key.</summary>
    public string? Action { get; init; }

    /// <summary>
    /// Set by the reader when a field has the wrong type or the entry is not an object; the entry is then skipped
    /// and this text reported.
    /// </summary>
    public string? Problem { get; init; }
}
