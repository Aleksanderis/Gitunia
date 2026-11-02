using System.Runtime.CompilerServices;
using Gitunia.Core.Conflicts;
using Gitunia.Core.Overlays;
using Gitunia.Core.Pick;
using Gitunia.Core.Prompts;
using Gitunia.Core.Rebase;
using Gitunia.Core.Reflog;
using Gitunia.Core.Settings;
using Gitunia.Core.Shell;
using Xunit;

namespace Gitunia.Core.Tests.Shell;

[Trait("Os", "Independent")]
public sealed class ShellStateTests
{
    // Every session of the shell state is either listed in one of the meanings or named here as no overlay, so a new
    // session cannot be forgotten silently.
    private static readonly string[] NotOverlaySessions = ["Creation", "Picker", "Compare", "QuickJump", "Sidebar", "TabActions"];

    private static readonly string[] EditorSessions = ["Reflog", "RebaseEditor", "Conflict"];

    [Theory]
    [InlineData("Confirm")]
    [InlineData("Prompt")]
    [InlineData("Output")]
    public void HasModal_is_true_for_each_dialog_prompt_and_output(string open)
    {
        var shell = With(open);

        Assert.True(shell.HasModal);
        Assert.True(shell.HasOverlay);
        Assert.False(shell.HasEditor);
    }

    [Theory]
    [InlineData("Reflog")]
    [InlineData("RebaseEditor")]
    [InlineData("Conflict")]
    public void HasEditor_is_true_for_each_editor_session(string open)
    {
        var shell = With(open);

        Assert.True(shell.HasEditor);
        Assert.True(shell.HasOverlay);
        Assert.False(shell.HasModal);
    }

    [Theory]
    [InlineData("Mainline")]
    [InlineData("Settings")]
    [InlineData("OverlayLayer")]
    public void HasOverlay_covers_the_remaining_overlays_but_neither_modal_nor_editor(string open)
    {
        var shell = With(open);

        Assert.True(shell.HasOverlay);
        Assert.False(shell.HasModal);
        Assert.False(shell.HasEditor);
    }

    [Fact]
    public void HasOverlay_is_false_for_an_empty_shell()
    {
        Assert.False(ShellState.Empty.HasOverlay);
    }

    [Theory]
    [InlineData(ShellSession.Reflog, "Reflog")]
    [InlineData(ShellSession.RebaseEditor, "RebaseEditor")]
    [InlineData(ShellSession.Conflict, "Conflict")]
    public void IsCoveredExcept_ignores_only_its_own_session(ShellSession own, string open)
    {
        Assert.False(With(open).IsCoveredExcept(own));

        foreach (var covering in new[] { "Confirm", "Prompt", "Output", "Mainline", "Settings", "OverlayLayer" })
        {
            Assert.True(With(covering).IsCoveredExcept(own));
        }

        foreach (var other in EditorSessions.Where(name => name != open))
        {
            Assert.True(With(other).IsCoveredExcept(own));
        }
    }

    // Guards the session types only; a plain overlay field (Confirm, Prompt, Output, Mainline) is covered by the
    // theories above.
    [Fact]
    public void Every_session_of_the_shell_state_is_an_editor_or_named_as_no_overlay()
    {
        var sessions = typeof(ShellState).GetProperties()
            .Where(property => property.PropertyType.Name.EndsWith("Session", StringComparison.Ordinal))
            .Select(property => property.Name)
            .Where(name => name != nameof(ShellState.Settings))
            .Order();

        Assert.Equal(EditorSessions.Concat(NotOverlaySessions).Order(), sessions);
    }

    private static ShellState With(string open) => open switch
    {
        "Confirm" => ShellState.Empty with { Confirm = Uninitialised<ConfirmDialog>() },
        "Prompt" => ShellState.Empty with { Prompt = Uninitialised<OpenPrompt>() },
        "Output" => ShellState.Empty with { Output = Uninitialised<OutputOverlay>() },
        "Mainline" => ShellState.Empty with { Mainline = Uninitialised<MainlineQuestion>() },
        "Reflog" => ShellState.Empty with
        {
            Reflog = ReflogSession.Empty with { List = Uninitialised<ReflogState>() },
        },
        "RebaseEditor" => ShellState.Empty with
        {
            RebaseEditor = RebaseEditorSession.Empty with { Editor = Uninitialised<RebaseEditorState>() },
        },
        "Conflict" => ShellState.Empty with
        {
            Conflict = ConflictSession.Empty with { Editor = Uninitialised<ConflictEditorState>() },
        },
        "Settings" => ShellState.Empty with
        {
            Settings = SettingsSession.Empty with { Screen = Uninitialised<SettingsState>() },
        },
        "OverlayLayer" => ShellState.Empty with { Overlay = OverlayKind.Help },
        _ => throw new ArgumentOutOfRangeException(nameof(open)),
    };

    // Only whether the state is open is read, so the contents do not matter.
    private static T Uninitialised<T>()
        where T : class
    {
#pragma warning disable IL2087 // Reflection is fine in tests; they are never trimmed.
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
#pragma warning restore IL2087
    }
}
