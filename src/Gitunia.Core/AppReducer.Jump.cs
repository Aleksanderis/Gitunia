using Gitunia.Core.Jump;
using Gitunia.Core.Keymap.Models;

namespace Gitunia.Core;

// The quick jump prompt (#996). It is the keyboard's while it is open, like the other text prompts.
public static partial class AppReducer
{
    private static AppUpdate ReduceQuickJump(AppState state, QuickJumpCommand command)
    {
        var before = state.Shell.QuickJump;
        var update = QuickJumpReducer.Reduce(before, command);
        var shell = state.Shell with { QuickJump = update.Session };
        if ((before is null) != (update.Session is null))
        {
            shell = shell.WithMode(update.Session is null ? KeyMode.Normal : KeyMode.Insert);
        }

        return new AppUpdate(state with { Shell = shell }, update.Effects);
    }
}
