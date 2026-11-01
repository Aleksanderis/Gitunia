using Gitunia.App;
using Gitunia.Core.Keymap;
using Gitunia.Core.Keymap.Models;
using Gitunia.Core.Overlays;
using Gitunia.Core.Search;
using Gitunia.Core.Tags;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Tui.App.Branches;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.Tags;

/// <summary>
/// The terminal side of the tag actions: it turns the tag keys and typed text into <see cref="TagCommand"/>s and draws
/// the open prompt from the tab's <see cref="TagState"/>. It decides nothing; the reducer does, and the runner performs
/// the effects. A prompt belongs to the tab it was opened in: leaving that tab cancels it.
/// </summary>
/// <param name="host">Holds the state the prompt is drawn from and takes the commands.</param>
/// <param name="selection">What the actions act on.</param>
public sealed class TagPromptHandler(Dispatcher host, IBranchSelectionSource selection) : IPromptHandler
{
    public string Notice => string.Empty;

    public string StatusLine => string.Empty;

    public PromptPopup? Popup =>
        host.Workspace.ActiveTab?.Tags.Prompt is { } prompt ? TagPromptPopup.Of(prompt) : null;

    public bool TryExecute(ActionId action, int? count)
    {
        CancelPromptsOfOtherTabs();
        if (host.Workspace.ActiveTab is not { } tab)
        {
            return false;
        }

        if (tab.Tags.Prompt is not null)
        {
            return TryHandleOpenPrompt(tab.Id, action);
        }

        var command = Request(tab.Id, action);
        if (command is null)
        {
            return false;
        }

        host.Dispatch(command);
        return true;
    }

    public bool HandleTextKey(KeyEvent key)
    {
        CancelPromptsOfOtherTabs();
        if (host.Workspace.ActiveTab is not { Tags.Prompt: { } prompt } tab)
        {
            return false;
        }

        var normalized = key.Normalize();
        if (prompt.IsPicker && PickerKeys.IsMove(normalized, out var step))
        {
            host.Dispatch(new TagPromptMoved(tab.Id, step));
        }
        else if (PromptTextEditor.Apply(prompt.Text, key) is { } edited && edited != prompt.Text)
        {
            host.Dispatch(new TagPromptTextChanged(tab.Id, edited));
        }

        return true;
    }

    public void OnGraphChanged()
    {
    }

    private TagCommand? Request(TabId tabId, ActionId action)
    {
        if (action != ActionIds.TagCreate && action != ActionIds.TagDelete && action != ActionIds.TagPush
            && action != ActionIds.TagDeleteRemote)
        {
            return null;
        }

        var current = selection.Current();
        var target = new TagTarget(current.RowKind, current.Name, current.CommitHash);
        if (action == ActionIds.TagCreate)
        {
            return new TagCreateRequested(tabId, target);
        }

        if (action == ActionIds.TagDelete)
        {
            return new TagDeleteRequested(tabId, target);
        }

        if (action == ActionIds.TagPush)
        {
            return new TagPushRequested(tabId, target);
        }

        return new TagDeleteOnRemoteRequested(tabId, target);
    }

    private bool TryHandleOpenPrompt(TabId tabId, ActionId action)
    {
        if (action == ActionIds.InputAccept)
        {
            host.Dispatch(new TagPromptAccepted(tabId));
        }
        else if (action == ActionIds.Cancel)
        {
            host.Dispatch(new TagPromptCancelled(tabId));
        }
        else
        {
            return false;
        }

        return true;
    }

    private void CancelPromptsOfOtherTabs()
    {
        var activeId = host.Workspace.ActiveTabId;
        foreach (var tab in host.Workspace.Tabs)
        {
            if (tab.Tags.Prompt is not null && tab.Id != activeId)
            {
                host.Dispatch(new TagPromptCancelled(tab.Id));
            }
        }
    }
}
