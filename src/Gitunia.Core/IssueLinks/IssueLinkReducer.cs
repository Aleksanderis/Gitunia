using System.Collections.Immutable;
using Gitunia.Core.Clipboard;
using Gitunia.Core.Effects;
using Gitunia.Core.Status;

namespace Gitunia.Core.IssueLinks;

/// <summary>
/// Opens the tracker link on the selected line of the commit message (<c>gx</c>, or <c>Enter</c> and a double-click
/// on a line that has one) as the pure transition <c>IssueLinkCommand -> Effects</c>. Where no browser can be started
/// the address is copied instead.
/// </summary>
public static class IssueLinkReducer
{
    /// <summary>Shown when the open-link key is pressed on a line without a link.</summary>
    public const string NoLinkNotice = "No issue link on this line";

    /// <summary>Applies a command and returns the effects to run.</summary>
    public static ImmutableArray<AppEffect> Reduce(IssueLinkCommand command) => command switch
    {
        IssueLinkOpenFinished finished => Finished(finished),
        IssueLinkOpenRequested request => Open(request),
        _ => throw new UnhandledCommandException(command),
    };

    private static ImmutableArray<AppEffect> Open(IssueLinkOpenRequested request)
    {
        var references = request.References;
        if (references.IsDefaultOrEmpty)
        {
            return request.IsExplicit ? [Warn(NoLinkNotice)] : [];
        }

        var number = request.Number ?? 1;
        if (number < 1 || number > references.Length)
        {
            var noun = references.Length == 1 ? "issue link" : "issue links";

            return [Warn($"This line has {references.Length} {noun}: there is no link number {number}")];
        }

        var url = references[number - 1].Url;

        return [new PostStatusEffect($"Opening {url}", StatusSeverity.Info), new OpenUrlEffect(url)];
    }

    private static ImmutableArray<AppEffect> Finished(IssueLinkOpenFinished finished)
    {
        if (finished.Opened)
        {
            return [new PostStatusEffect($"Opened {finished.Url}", StatusSeverity.Info)];
        }

        return
        [
            new CopyToClipboardEffect(finished.Url, "issue link"),
            Warn($"Could not open the link ({finished.Error}); tried copying {finished.Url} instead"),
        ];
    }

    private static PostStatusEffect Warn(string text) => new(text, StatusSeverity.Warning);
}
