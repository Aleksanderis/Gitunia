using System.Collections.Immutable;
using Gitunia.Core.Effects;
using Gitunia.Core.Picker;
using Gitunia.Core.Prompts;
using Gitunia.Core.Status;
using Gitunia.Git.Repositories;
using Gitunia.Git.Repositories.Models;

namespace Gitunia.Core.Creation;

/// <summary>
/// Clone and init from the repository picker as pure transitions (ux-workflows.md, workflow 0): <c>Alt-c</c> asks for a
/// source, then for the directory (default: the folder typed in the picker, else the first scan root, plus the
/// repository name) and clones with the progress as the status bar line; <c>Alt-i</c> asks for a directory and runs
/// <c>git init</c> there. Both open the result in a new tab. One creation runs at a time and belongs to no tab. A clone
/// never prompts for credentials: a failure says so and names the way out (#45 offers the real terminal; the
/// failure text is built where the clone runs).
/// </summary>
public static class CreationReducer
{
    /// <summary>Shown when a clone or init is asked for while one runs.</summary>
    public const string BusyNotice = "Busy: a clone or init is still running";

    /// <summary>Shown when a clone or init is asked for while the picker locates a missing repository.</summary>
    public const string LocatingNotice = "Not while locating a repository";

    /// <summary>Shown when an empty source or directory was accepted.</summary>
    public const string EmptyNotice = "Type something first";

    /// <summary>Shown when a clone was cancelled.</summary>
    public const string CancelledText = "Clone cancelled; nothing was left behind";

    private const string CloneTitle = "Clone repository";
    private const string CloneSourceContext = "URL or path of the repository to clone";
    private const string CloneSourceLabel = "next";
    private const string CloneLabel = "clone";
    private const string InitTitle = "Initialise repository";
    private const string InitContext = "Directory for git init (created when missing, files are kept)";
    private const string InitLabel = "init";

    /// <summary>Applies a command to the creation session.</summary>
    public static CreationUpdate Reduce(CreationSession session, CreationCommand command, CreationContext context) =>
        command switch
        {
            CreationRequested request => Request(session, request, context),
            CloneSourceAnswered source => SourceAnswered(session, source, context),
            CloneParentResolved parent => ParentResolved(session, parent, context),
            CloneTargetAnswered target => TargetAnswered(session, target, context),
            InitDirectoryAnswered init => InitAnswered(session, init, context),
            CloneCancelRequested => Cancel(session),
            CloneProgressed progressed => Progressed(session, progressed),
            CloneSucceeded cloned => Finished(
                session with { RunningClone = null },
                $"Cloned {Path.GetFileName(cloned.Location.RootPath)}",
                StatusSeverity.Info,
                cloned.Location),
            CloneFailed failed => Finished(session with { RunningClone = null }, failed.Text, StatusSeverity.Error, null),
            CloneCancelled => Finished(session with { RunningClone = null }, CancelledText, StatusSeverity.Warning, null),
            InitSucceeded init => Finished(
                session with { IsInitRunning = false },
                InitText(init),
                StatusSeverity.Info,
                init.Location),
            InitFailed failed => Finished(
                session with { IsInitRunning = false },
                failed.Text,
                StatusSeverity.Error,
                null),
            _ => throw new UnhandledCommandException(command),
        };

    private static CreationUpdate Request(CreationSession session, CreationRequested request, CreationContext context)
    {
        if (context.Picker.State is not { } state)
        {
            return Plain(session, []);
        }

        if (state.LocatingTabId is not null)
        {
            return Warn(session, LocatingNotice);
        }

        if (session.IsBusy)
        {
            return Warn(session, BusyNotice);
        }

        if (request.Kind == CreationKind.Clone)
        {
            return Ask(session, SourcePrompt(string.Empty));
        }

        var directory = state.IsPathMode ? Resolve(state.Query, context) : context.BaseDirectory;
        var prompt = new PromptRequest(
            InitTitle,
            InitContext,
            InitLabel,
            new InitDirectoryAnswer(),
            Abbreviate(directory, context));

        return Ask(session, prompt);
    }

    private static CreationUpdate SourceAnswered(
        CreationSession session,
        CloneSourceAnswered answered,
        CreationContext context)
    {
        var typed = answered.Text.Trim();
        if (typed.Length == 0)
        {
            return Reask(session, SourcePrompt(answered.Text));
        }

        var source = CloneSource.Resolve(typed, context.BaseDirectory, context.Picker.HomeDirectory);
        var query = context.Picker.State is { IsPathMode: true } state ? state.Query : null;
        var path = query is null ? null : Resolve(query, context);
        var endsWithSeparator = query is not null && (query.EndsWith('/') || query.EndsWith('\\'));
        ImmutableArray<AppEffect> effects =
        [
            new ResolveCloneParentEffect(
                source,
                path,
                endsWithSeparator,
                context.Picker.ScanRoots,
                context.Picker.HomeDirectory),
        ];

        return Plain(session, effects);
    }

    private static CreationUpdate ParentResolved(
        CreationSession session,
        CloneParentResolved resolved,
        CreationContext context)
    {
        if (!context.Picker.IsOpen)
        {
            return Plain(session, []);
        }

        var target = Path.Combine(resolved.Parent, CloneSource.RepositoryName(resolved.Source));

        return Ask(session, TargetPrompt(resolved.Source, Abbreviate(target, context)));
    }

    private static CreationUpdate TargetAnswered(
        CreationSession session,
        CloneTargetAnswered answered,
        CreationContext context)
    {
        var typed = answered.Text.Trim();
        if (typed.Length == 0)
        {
            return Reask(session, TargetPrompt(answered.Source, answered.Text));
        }

        var directory = Resolve(typed, context);
        ImmutableArray<AppEffect> effects = [new CloneRepositoryEffect(new CloneRequest(answered.Source, directory))];

        return Plain(session with { RunningClone = new CloneState(null, false) }, effects);
    }

    private static CreationUpdate InitAnswered(
        CreationSession session,
        InitDirectoryAnswered answered,
        CreationContext context)
    {
        var typed = answered.Text.Trim();
        if (typed.Length == 0)
        {
            return Reask(session, InitPrompt(answered.Text));
        }

        ImmutableArray<AppEffect> effects = [new InitRepositoryEffect(Resolve(typed, context))];

        return Plain(session with { IsInitRunning = true }, effects);
    }

    private static CreationUpdate Cancel(CreationSession session)
    {
        if (session.RunningClone is not { IsCancelling: false } clone)
        {
            return Plain(session, []);
        }

        return Plain(session with { RunningClone = clone with { IsCancelling = true } }, [new CancelCloneEffect()]);
    }

    private static CreationUpdate Progressed(CreationSession session, CloneProgressed progressed)
    {
        if (session.RunningClone is not { } clone)
        {
            return Plain(session, []);
        }

        return Plain(session with { RunningClone = clone with { Latest = progressed.Report } }, []);
    }

    private static string InitText(InitSucceeded init)
    {
        var name = Path.GetFileName(init.Location.RootPath);

        return init.WasRepository ? $"{name} already is a repository: opened it" : $"Initialised {name}";
    }

    private static CreationUpdate Finished(
        CreationSession session,
        string text,
        StatusSeverity severity,
        RepositoryLocation? location)
    {
        var effects = ImmutableArray.Create<AppEffect>(new PostStatusEffect(text, severity));
        if (location is null)
        {
            return Plain(session, effects);
        }

        return new CreationUpdate(session, effects, [new PickerRepositoryCreated(location)]);
    }

    private static PromptRequest SourcePrompt(string text) =>
        new(CloneTitle, CloneSourceContext, CloneSourceLabel, new CloneSourceAnswer(), text);

    private static PromptRequest TargetPrompt(string source, string text) =>
        new(CloneTitle, $"Clone {source} into", CloneLabel, new CloneTargetAnswer(source), text);

    private static PromptRequest InitPrompt(string text) =>
        new(InitTitle, InitContext, InitLabel, new InitDirectoryAnswer(), text);

    private static CreationUpdate Ask(CreationSession session, PromptRequest prompt) =>
        Plain(session, [new ShowPromptEffect(prompt)]);

    // The answer closed the prompt; an empty one asks again with what was typed.
    private static CreationUpdate Reask(CreationSession session, PromptRequest prompt) =>
        Plain(session, [new PostStatusEffect(EmptyNotice, StatusSeverity.Warning), new ShowPromptEffect(prompt)]);

    private static CreationUpdate Warn(CreationSession session, string text) =>
        Plain(session, [new PostStatusEffect(text, StatusSeverity.Warning)]);

    private static CreationUpdate Plain(CreationSession session, ImmutableArray<AppEffect> effects) =>
        new(session, effects, []);

    private static string Resolve(string text, CreationContext context) =>
        PathQuery.Resolve(text, context.Picker.HomeDirectory, context.BaseDirectory);

    private static string Abbreviate(string path, CreationContext context) =>
        PathDisplay.Abbreviate(path, context.Picker.HomeDirectory, OperatingSystem.IsWindows());
}
