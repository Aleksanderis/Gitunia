using Gitunia.Core.Keymap.Models;
using Gitunia.Tui.Input;

namespace Gitunia.Tui.App.WorkingTree;

/// <summary>
/// Lets the commit message box receive the keys typed in Insert mode next to the other prompts. It owns no action: the box's actions
/// reach <see cref="CommitActions"/> through the Changes view's handler, only the typed keys come through here.
/// </summary>
/// <param name="commits">The commit actions that own the editor.</param>
public sealed class CommitMessageInput(CommitActions commits) : IPromptHandler
{
    public string StatusLine => string.Empty;

    public string Notice => string.Empty;

    public bool TryExecute(ActionId action, int? count) => false;

    public bool HandleTextKey(KeyEvent key) => commits.HandleTextKey(key);

    public void OnGraphChanged()
    {
    }
}
