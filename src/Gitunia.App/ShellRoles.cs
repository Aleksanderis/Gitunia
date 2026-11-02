namespace Gitunia.App;

/// <summary>The objects that perform the shell effects, one per role; the runner calls the role an effect belongs to.</summary>
public sealed record ShellRoles
{
    /// <summary>Roles that do nothing, for a runner without a terminal.</summary>
    public static ShellRoles None { get; } = From(NoShellEffects.Instance);

    /// <summary>The clipboard and the terminal retry.</summary>
    public required ICopyEffects Copy { get; init; }

    /// <summary>The list cursors.</summary>
    public required ICursorEffects Cursor { get; init; }

    /// <summary>The flows the terminal layer still owns.</summary>
    public required IFlowEffects Flow { get; init; }

    /// <summary>Options, theme, session saving and quit.</summary>
    public required ISessionEffects Session { get; init; }

    /// <summary>The stash view.</summary>
    public required IStashEffects Stash { get; init; }

    /// <summary>External tools and the config editor.</summary>
    public required IToolEffects Tools { get; init; }

    /// <summary>The routed key action.</summary>
    public required IActionRouteEffects Route { get; init; }

    /// <summary>Uses one object for every role.</summary>
    /// <param name="shell">The object that implements all of them.</param>
    public static ShellRoles From(IShellEffects shell) =>
        new()
        {
            Copy = shell,
            Cursor = shell,
            Flow = shell,
            Session = shell,
            Stash = shell,
            Tools = shell,
            Route = shell,
        };
}
