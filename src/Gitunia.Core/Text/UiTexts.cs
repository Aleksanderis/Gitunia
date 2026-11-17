namespace Gitunia.Core.Text;

/// <summary>
/// The wording of empty, no-match, nothing-to-do and unchanged answers, so every screen says them the same way
/// (docs/internal/ui-consistency.md, decision 1): sentence case, no full stop. Drawn in the empty-text (muted) role
/// where it is a row and not a status message.
/// </summary>
public static class UiTexts
{
    /// <summary>A load in progress.</summary>
    public const string Loading = "Loading...";

    /// <summary>A search repeat (next or previous match) before any search was started.</summary>
    public const string NoSearch = "No search yet: press / to start one";

    /// <summary>A filter or search that matches nothing, naming the thing looked for: <c>No matching branch</c>.</summary>
    public static string NoMatch(string subject) => $"No matching {subject}";

    /// <summary>A search for a query that found nothing: <c>No match for fix</c>.</summary>
    public static string NoMatchFor(string query) => $"No match for {query}";

    /// <summary>An action with nothing to act on: <c>Nothing to copy</c>.</summary>
    public static string Nothing(string verb) => $"Nothing to {verb}";

    /// <summary>The same inside a sentence: <c>nothing to copy</c>.</summary>
    public static string NothingTo(string verb) => $"nothing to {verb}";

    /// <summary>The tail of a refusal that changed nothing: <c>nothing was deleted</c> (past participle).</summary>
    public static string NothingWas(string participle) => $"nothing was {participle}";

    /// <summary>A value that did not change: <c>The subject unchanged</c>.</summary>
    public static string Unchanged(string subject) => $"{subject} unchanged";

    /// <summary>An answer that equals the current value: <c>The name unchanged: nothing was renamed</c>.</summary>
    public static string Unchanged(string subject, string participle) =>
        $"{Unchanged(subject)}: {NothingWas(participle)}";

    /// <summary>A required answer left empty: <c>No name</c>.</summary>
    public static string Empty(string subject) => $"No {subject}";
}
