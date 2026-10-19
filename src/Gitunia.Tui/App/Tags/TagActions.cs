using System.Globalization;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Search;
using Gitunia.Core.Sidebar;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Repositories;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Git.Execution.Exceptions;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Remotes;
using Gitunia.Git.Tags;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.App.WorkingTree;
using Gitunia.Tui.Input;

// Core.Overlays has a FuzzyMatcher too (ConfirmPrompt lives there); the remote picker uses the Search one.
using RemoteMatcher = Gitunia.Core.Search.FuzzyMatcher;

namespace Gitunia.Tui.App.Tags;

/// <summary>
/// Create, delete and push tags, and delete one on a remote (ux.md, workflow 3, "Tags"). A name and an optional
/// message are typed in prompt popups (an empty message makes a lightweight tag), a remote is chosen in a
/// type-to-filter prompt, and both deletes go through the Confirm dialog. A push is one ref with no progress and no
/// prompts; git's first error line is the failure text. Refusals that need no git are decided from the tab refs.
/// Everything else runs off the UI thread, one operation per tab, and the tab is refreshed afterwards. What the
/// action acts on is fixed when the prompt opens.
/// </summary>
/// <param name="host">The workspace.</param>
/// <param name="writer">Runs the git commands.</param>
/// <param name="statuses">Where the outcome is reported.</param>
/// <param name="postToUi">Runs an action on the UI thread.</param>
/// <param name="cancellationOf">The token that ends the work of a tab (the tab was closed).</param>
/// <param name="confirm">Shows the confirm dialog and runs the callback on <c>y</c>; false when it cannot show.</param>
/// <param name="refresh">Reads a tab again (refs, graph, status); false when it cannot.</param>
/// <param name="setMode">Switches the keymap mode (Insert while a prompt is open).</param>
/// <param name="redraw">Redraws the status bar.</param>
/// <param name="selection">What is selected now: a sidebar row or the graph commit.</param>
/// <param name="offerTerminal">
/// Offers the terminal retry (tab, operation name, git arguments) when a remote tag call failed for lack of
/// credentials (#512).
/// </param>
public sealed class TagActions(
    WorkspaceHost host,
    ITagWriter writer,
    StatusChannel statuses,
    Action<Action> postToUi,
    Func<TabId, CancellationToken> cancellationOf,
    Func<ConfirmPrompt, Action, bool> confirm,
    Func<TabId, bool> refresh,
    Action<KeyMode> setMode,
    Action redraw,
    Func<BranchSelection> selection,
    Action<TabId, string, IReadOnlyList<string>> offerTerminal) : IPromptHandler
{
    private const int ShortHashLength = 7;
    private const string BusyNotice = "Busy: the last tag operation is still running";
    private const string TooSmallNotice = "The screen is too small to ask: nothing was changed";
    private const string SelectNotice = "Select a tag (sidebar) or a commit with one tag on it";

    private readonly HashSet<TabId> _busy = [];
    private TagPromptKind _kind;
    private TabId _tabId;
    private string _text = string.Empty;
    private string _startPoint = string.Empty;
    private string _label = string.Empty;
    private string _tagName = string.Empty;
    private string _newName = string.Empty;
    private IReadOnlyList<string> _remotes = [];
    private List<string> _candidates = [];
    private int _index;

    public string Notice { get; private set; } = string.Empty;

    public string StatusLine => string.Empty;

    public PromptPopup? Popup => _kind == TagPromptKind.None || host.Workspace.ActiveTabId != _tabId
        ? null
        : _kind switch
        {
            TagPromptKind.CreateName => PromptPopup.ForText("New tag", $"At {_label}", _text, "next"),
            TagPromptKind.CreateMessage => PromptPopup.ForText(
                "Tag message", $"{_newName}: empty makes a lightweight tag", _text, "create"),
            TagPromptKind.PushRemote => RemotePopup("Push tag", $"Remote to push {_tagName} to"),
            _ => RemotePopup("Delete tag on remote", $"Remote to delete {_tagName} from"),
        };

    public bool TryExecute(ActionId action, int? count)
    {
        Notice = string.Empty;
        if (_kind != TagPromptKind.None && host.Workspace.ActiveTabId != _tabId)
        {
            // The prompt belongs to the tab it was opened in; leaving that tab drops it.
            Close();
        }

        if (_kind != TagPromptKind.None)
        {
            return TryHandleOpenPrompt(action);
        }

        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        if (action == ActionIds.TagCreate)
        {
            Notice = BeginOperation(tab) ?? OpenCreate(tab);
        }
        else if (action == ActionIds.TagDelete)
        {
            Notice = BeginOperation(tab) ?? AskDelete(tab);
        }
        else if (action == ActionIds.TagPush || action == ActionIds.TagDeleteRemote)
        {
            Notice = BeginOperation(tab) ?? LoadRemotes(tab, action == ActionIds.TagPush);
        }
        else
        {
            return false;
        }

        redraw();
        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        if (_kind == TagPromptKind.None || host.Workspace.ActiveTabId != _tabId)
        {
            return false;
        }

        var normalized = key.Normalize();
        var isPicker = _kind is TagPromptKind.PushRemote or TagPromptKind.DeleteRemote;
        if (isPicker && IsMove(normalized, out var step))
        {
            _index = _candidates.Count == 0 ? 0 : (_index + step + _candidates.Count) % _candidates.Count;
        }
        else if (PromptTextEditor.Apply(_text, key) is { } edited && edited != _text)
        {
            _text = edited;
            if (isPicker)
            {
                RefreshCandidates();
            }
        }

        redraw();
        return true;
    }

    public void OnGraphChanged()
    {
    }

    private static bool IsMove(KeyEvent key, out int step)
    {
        step = 0;
        if (key is { Name: KeyNames.Up, Modifiers: KeyModifiers.None })
        {
            step = -1;
        }
        else if (key is { Name: KeyNames.Down or KeyNames.Tab, Modifiers: KeyModifiers.None })
        {
            step = 1;
        }

        return step != 0;
    }

    private static string ShortHash(string hash) => hash[..Math.Min(ShortHashLength, hash.Length)];

    private static bool HasTag(RepositoryState tab, string name) =>
        tab.Refs.Any(reference => reference.Kind == RefKind.Tag && reference.Name == name);

    private bool TryHandleOpenPrompt(ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            Notice = Accept();
        }
        else if (action == ActionIds.Cancel)
        {
            Close();
        }
        else
        {
            return false;
        }

        redraw();
        return true;
    }

    private string? BeginOperation(RepositoryState tab) => _busy.Contains(tab.Id) ? BusyNotice : null;

    private void Close()
    {
        _kind = TagPromptKind.None;
        setMode(KeyMode.Normal);
    }

    private void Open(TagPromptKind kind, RepositoryState tab, string text)
    {
        _kind = kind;
        _tabId = tab.Id;
        _text = text;
        setMode(KeyMode.Insert);
    }

    private string OpenCreate(RepositoryState tab)
    {
        var chosen = selection();
        if (chosen.CommitHash is not { } hash)
        {
            return "Select a commit or a ref first";
        }

        _startPoint = hash;
        _label = chosen.Name ?? ShortHash(hash);
        Open(TagPromptKind.CreateName, tab, string.Empty);
        return string.Empty;
    }

    // Finds the local tag a delete or a push acts on, or returns why there is none.
    private string? ResolveTag(RepositoryState tab, out GitRef? tag)
    {
        tag = null;
        var chosen = selection();
        var tags = tab.Refs.Where(reference => reference.Kind == RefKind.Tag).ToList();
        if (chosen.RowKind is not null)
        {
            tag = chosen.RowKind == SidebarRowKind.Tag
                ? tags.FirstOrDefault(reference => reference.Name == chosen.Name)
                : null;
            return tag is null ? "Select a tag first" : null;
        }

        var here = chosen.CommitHash is null
            ? []
            : tags.Where(reference => reference.CommitHash == chosen.CommitHash).ToList();
        if (here.Count > 1)
        {
            return $"{here.Count} tags point here: pick one in the sidebar";
        }

        tag = here.FirstOrDefault();
        return tag is null ? SelectNotice : null;
    }

    private string Accept()
    {
        var kind = _kind;
        if (host.Workspace.FindTab(_tabId) is not { } tab)
        {
            Close();
            return string.Empty;
        }

        var text = _text.Trim();
        switch (kind)
        {
            case TagPromptKind.CreateName:
                return AcceptName(tab, text);
            case TagPromptKind.CreateMessage:
                Close();
                return AcceptCreate(tab, _text.Trim());
            default:
                return AcceptRemote(tab, kind);
        }
    }

    private string AcceptName(RepositoryState tab, string name)
    {
        if (name.Length == 0)
        {
            Close();
            return "No name: nothing was created";
        }

        if (HasTag(tab, name))
        {
            Close();
            return $"Tag {name} already exists";
        }

        _newName = name;
        Open(TagPromptKind.CreateMessage, tab, string.Empty);
        return string.Empty;
    }

    private string AcceptCreate(RepositoryState tab, string message)
    {
        var name = _newName;
        var start = _startPoint;
        var kind = message.Length == 0 ? "lightweight tag" : "annotated tag";
        var done = $"Created {kind} {name} at {_label}";
        Start(tab, "Could not create the tag", async token =>
        {
            if (!await writer.IsValidNameAsync(tab.Location, name, token).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"'{name}' is not a valid tag name");
            }

            await writer.CreateAsync(tab.Location, name, start, message, token).ConfigureAwait(false);
            return done;
        });
        return string.Empty;
    }

    private string AskDelete(RepositoryState tab)
    {
        if (ResolveTag(tab, out var tag) is { } refusal)
        {
            return refusal;
        }

        var prompt = new ConfirmPrompt(
            "Delete tag",
            [
                $"Delete the local tag {tag!.Name} (at {ShortHash(tag.CommitHash)})?",
                "A copy on a remote is not touched.",
            ],
            $"Undo: git tag {tag.Name} {tag.ObjectHash}",
            "Delete tag");
        return confirm(prompt, () => StartDelete(tab.Id, tag)) ? string.Empty : TooSmallNotice;
    }

    private void StartDelete(TabId tabId, GitRef tag)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab || _busy.Contains(tabId))
        {
            statuses.Warning($"{BusyNotice}; nothing was deleted");
            return;
        }

        var name = tag.Name;
        Start(tab, "Could not delete the tag", async token =>
        {
            await writer.DeleteAsync(tab.Location, name, token).ConfigureAwait(false);
            return $"Deleted tag {name} (was {ShortHash(tag.ObjectHash)}; restore: git tag {name} {tag.ObjectHash})";
        });
    }

    private string LoadRemotes(RepositoryState tab, bool push)
    {
        if (ResolveTag(tab, out var tag) is { } refusal)
        {
            return refusal;
        }

        var name = tag!.Name;
        _busy.Add(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => ReadRemotesAsync(tab, name, push, token), CancellationToken.None);
        return string.Empty;
    }

    private async Task ReadRemotesAsync(RepositoryState tab, string name, bool push, CancellationToken token)
    {
        try
        {
            var remotes = await writer.ListRemotesAsync(tab.Location, token).ConfigureAwait(false);
            postToUi(() => OpenRemotePrompt(tab, name, push, remotes));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _busy.Remove(tab.Id));
        }
        catch (Exception exception)
        {
            var failure = $"Could not list the remotes: {OperationText.Reason(exception)}";
            postToUi(() => Finish(tab.Id, failure, StatusSeverity.Error, false));
        }
    }

    private void OpenRemotePrompt(RepositoryState tab, string name, bool push, IReadOnlyList<string> remotes)
    {
        _busy.Remove(tab.Id);
        if (remotes.Count == 0)
        {
            statuses.Warning("There is no remote: add one first");
        }
        else if (host.Workspace.ActiveTabId != tab.Id || _kind != TagPromptKind.None)
        {
            return;
        }
        else
        {
            _remotes = remotes;
            _tagName = name;
            Open(push ? TagPromptKind.PushRemote : TagPromptKind.DeleteRemote, tab, string.Empty);
            RefreshCandidates();
        }

        redraw();
    }

    private void RefreshCandidates()
    {
        var pattern = _text.Trim();
        _candidates =
        [
            .. _remotes
                .Where(remote => RemoteMatcher.Score(pattern, remote) is not null)
                .OrderByDescending(remote => RemoteMatcher.Score(pattern, remote)),
        ];
        _index = 0;
    }

    private PromptPopup RemotePopup(string title, string context)
    {
        var detail = "No matching remote";
        if (_index < _candidates.Count)
        {
            var position = (_index + 1).ToString(CultureInfo.InvariantCulture);
            var total = _candidates.Count.ToString(CultureInfo.InvariantCulture);
            detail = $"{_candidates[_index]} ({position}/{total})";
        }

        return PromptPopup.ForFilter(title, context, _text, detail, "choose");
    }

    private string AcceptRemote(RepositoryState tab, TagPromptKind kind)
    {
        if (_index >= _candidates.Count)
        {
            return "No matching remote";
        }

        var remote = _candidates[_index];
        var name = _tagName;
        Close();
        if (kind == TagPromptKind.PushRemote)
        {
            Start(tab, "Could not push the tag", async token =>
            {
                await writer.PushAsync(tab.Location, remote, name, token).ConfigureAwait(false);
                return $"Pushed tag {name} to {remote}";
            }, new TerminalRetry("Tag push", RemoteCommands.BuildTagPush(remote, name, false)));
            return string.Empty;
        }

        var prompt = new ConfirmPrompt(
            "Delete tag on remote",
            [$"Delete the tag {name} on the remote {remote}?", "The local tag stays."],
            $"Undo: git push {remote} refs/tags/{name}",
            "Delete on remote");
        return confirm(prompt, () => StartDeleteOnRemote(tab.Id, remote, name)) ? string.Empty : TooSmallNotice;
    }

    private void StartDeleteOnRemote(TabId tabId, string remote, string name)
    {
        if (host.Workspace.FindTab(tabId) is not { } tab || _busy.Contains(tabId))
        {
            statuses.Warning($"{BusyNotice}; nothing was deleted");
            return;
        }

        Start(tab, "Could not delete the tag on the remote", async token =>
        {
            await writer.DeleteOnRemoteAsync(tab.Location, remote, name, token).ConfigureAwait(false);
            return $"Deleted tag {name} on {remote} (restore: git push {remote} refs/tags/{name})";
        }, new TerminalRetry("Tag delete", RemoteCommands.BuildTagDelete(remote, name, false)));
    }

    // Own busy set, not the shared operation gate: tag writes take ref locks, never the index lock.
    private void Start(
        RepositoryState tab,
        string failure,
        Func<CancellationToken, Task<string>> work,
        TerminalRetry? retry = null)
    {
        _busy.Add(tab.Id);
        var token = cancellationOf(tab.Id);
        _ = Task.Run(() => RunAsync(tab.Id, failure, work, retry, token), CancellationToken.None);
    }

    private async Task RunAsync(
        TabId tabId,
        string failure,
        Func<CancellationToken, Task<string>> work,
        TerminalRetry? retry,
        CancellationToken token)
    {
        try
        {
            var done = await work(token).ConfigureAwait(false);
            postToUi(() => Finish(tabId, done, StatusSeverity.Info, true));
        }
        catch (OperationCanceledException)
        {
            postToUi(() => _busy.Remove(tabId));
        }
        catch (Exception exception)
        {
            var text = $"{failure}: {OperationText.Reason(exception)}";
            var offer = retry is not null && exception is GitCommandFailedException failed
                && RemoteAuthFailure.Matches(failed.StandardError);
            postToUi(() =>
            {
                Finish(tabId, text, StatusSeverity.Error, true);
                if (offer)
                {
                    offerTerminal(tabId, retry!.Noun, retry.Arguments);
                }
            });
        }
    }

    private void Finish(TabId tabId, string text, StatusSeverity severity, bool refreshTab)
    {
        _busy.Remove(tabId);
        statuses.Post(text, severity);
        if (refreshTab)
        {
            refresh(tabId);
        }
    }

    private sealed record TerminalRetry(string Noun, IReadOnlyList<string> Arguments);
}
