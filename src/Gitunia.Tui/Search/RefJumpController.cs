using System.Globalization;
using Gitunia.Core.Keymap;
using Gitunia.Core.Search;
using Gitunia.Git;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.Search;

/// <summary>The <c>gr</c> prompt: type to filter refs (fuzzy) or a hash prefix, <c>Up</c>/<c>Down</c> or <c>Tab</c> to choose, <c>Enter</c> to select that commit.</summary>
public sealed class RefJumpController(GraphViewHandle view, Func<IEnumerable<GitRef>> refs) : IPromptHandler
{
    private RefJumpState? prompt;
    private ObjectId? pendingCommit;

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine
    {
        get
        {
            if (prompt is null)
            {
                return string.Empty;
            }

            var line = "gr " + prompt.Text + "_  ";
            return prompt.Selected is { } selected
                ? line + selected.Label + " (" + (prompt.SelectedIndex + 1).ToString(CultureInfo.InvariantCulture) + "/" + prompt.Candidates.Length.ToString(CultureInfo.InvariantCulture) + ")"
                : line + "no matching ref or commit";
        }
    }

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (action == ActionIds.GraphJumpToRef && view.ActiveSession() is { } session)
        {
            prompt = RefJumpResolver.Resolve(string.Empty, refs(), session.Graph);
            view.SetMode(KeyMode.Insert);
        }
        else if (action == ActionIds.InputAccept && prompt is not null)
        {
            var selected = prompt.Selected;
            prompt = null;
            view.SetMode(KeyMode.Normal);
            Jump(selected);
        }
        else if (action == ActionIds.Cancel && prompt is not null)
        {
            prompt = null;
            view.SetMode(KeyMode.Normal);
        }
        else
        {
            return false;
        }

        view.Redraw();
        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (prompt is null || view.ActiveSession() is not { } session)
        {
            return false;
        }

        var normalized = key.Normalize();
        if (normalized is { Name: KeyNames.Up, Modifiers: KeyModifiers.None })
        {
            prompt = prompt.Move(-1);
        }
        else if (normalized is { Name: KeyNames.Down or KeyNames.Tab, Modifiers: KeyModifiers.None })
        {
            prompt = prompt.Move(1);
        }
        else if (PromptTextEditor.Apply(prompt.Text, key) is { } text && text != prompt.Text)
        {
            prompt = RefJumpResolver.Resolve(text, refs(), session.Graph);
        }

        view.Redraw();
        return true;
    }

    public void OnGraphChanged()
    {
        if (pendingCommit is { } commitId && view.ActiveSession() is { } session)
        {
            var index = session.Graph.IndexOf(commitId);
            if (index >= 0)
            {
                pendingCommit = null;
                view.SelectRow(index);
            }
            else if (!session.IsLoading)
            {
                pendingCommit = null;
            }
        }
    }

    private void Jump(RefJumpCandidate? candidate)
    {
        if (candidate is null)
        {
            Notice = "No matching ref or commit";
            return;
        }

        if (view.ActiveSession() is not { } session)
        {
            return;
        }

        var index = session.Graph.IndexOf(candidate.CommitId);
        if (index >= 0)
        {
            view.SelectRow(index);
        }
        else if (session.IsLoading)
        {
            // The log is still streaming in; the jump happens as soon as the commit arrives.
            pendingCommit = candidate.CommitId;
            Notice = "Loading " + candidate.Label + "...";
        }
        else
        {
            Notice = candidate.Label + " is not in the graph";
        }
    }
}
