using System.Globalization;
using Gitunia.Core.Editing;
using Gitunia.Core.Effects;
using Gitunia.Core.Status;

namespace Gitunia.Core.Jump;

/// <summary>
/// The <c>&lt;space&gt;j</c> prompt (flash/easymotion style) as the pure transition
/// <c>(QuickJumpSession?, QuickJumpCommand) -> QuickJumpTransition</c>: type text, the rows in view that contain it get
/// a label key, and pressing a label moves the cursor to that row. <c>Enter</c> jumps to the nearest match, <c>Esc</c>
/// cancels. The matching and the labels are the pure <see cref="QuickJumpLabeler"/>; the list that shows them is view
/// code, so its labels arrive with the command.
/// </summary>
public static class QuickJumpReducer
{
    /// <summary>Shown when the focused pane has no quick jump.</summary>
    public const string UnavailableNotice = "Quick jump is not available here";

    /// <summary>Shown when Enter is pressed and nothing in view matches.</summary>
    public const string NoMatchNotice = "No match in view";

    private const string Prefix = "jump ";

    /// <summary>Applies a command; <paramref name="session"/> is the open prompt, null when none is open.</summary>
    public static QuickJumpTransition Reduce(QuickJumpSession? session, QuickJumpCommand command) => command switch
    {
        QuickJumpAccepted accepted => Accept(session, accepted),
        QuickJumpClosed => new QuickJumpTransition(null, []),
        QuickJumpKeyPressed pressed => Key(session, pressed),
        QuickJumpOpenRequested open => Open(open),
        _ => throw new UnhandledCommandException(command),
    };

    /// <summary>The status bar line of an open prompt.</summary>
    /// <param name="text">The typed text.</param>
    /// <param name="matches">How many rows in view match.</param>
    /// <param name="caret">The caret column in the text.</param>
    public static string StatusLine(string text, int matches, int caret = PromptCaret.End)
    {
        if (text.Length == 0)
        {
            return $"{Prefix}{PromptCaret.EndGlyph}  type text, then press a label";
        }

        var count = matches.ToString(CultureInfo.InvariantCulture);
        var typed = PromptCaret.Mark(text, caret);

        return matches == 0 ? $"{Prefix}{typed}  no match in view" : $"{Prefix}{typed}  {count} in view";
    }

    private static QuickJumpTransition Open(QuickJumpOpenRequested open)
    {
        if (open.TabId is not { } tabId || !open.HasTarget)
        {
            return Notice(UnavailableNotice);
        }

        if (open.UnavailableReason is { } reason)
        {
            return Notice(reason);
        }

        return new QuickJumpTransition(new QuickJumpSession(tabId, open.Pane, string.Empty), []);
    }

    private static QuickJumpTransition Accept(QuickJumpSession? session, QuickJumpAccepted accepted)
    {
        if (session is null)
        {
            return new QuickJumpTransition(null, []);
        }

        if (accepted.Labels.IsDefaultOrEmpty)
        {
            return new QuickJumpTransition(null, [new PostStatusEffect(NoMatchNotice, StatusSeverity.Info)]);
        }

        return new QuickJumpTransition(null, [new JumpToRowEffect(accepted.Labels[0].Row)]);
    }

    private static QuickJumpTransition Key(QuickJumpSession? session, QuickJumpKeyPressed pressed)
    {
        if (session is null)
        {
            return new QuickJumpTransition(null, []);
        }

        var update = QuickJumpLabeler.Apply(session.Text, session.Caret, pressed.Key, pressed.Labels);
        if (update.JumpRow is { } row)
        {
            return new QuickJumpTransition(null, [new JumpToRowEffect(row)]);
        }

        return new QuickJumpTransition(session with { Text = update.Text, Caret = update.Caret }, []);
    }

    private static QuickJumpTransition Notice(string text) =>
        new(null, [new PostStatusEffect(text, StatusSeverity.Info)]);
}
