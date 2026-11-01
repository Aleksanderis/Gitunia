using Gitunia.App.Staging;
using Gitunia.App;
using Gitunia.Core.Effects;
using Gitunia.Core.Shell;
using Gitunia.Core.Staging;
using Gitunia.Core.Status;
using Gitunia.Core.Workspaces.Tabs;
using Gitunia.Core;
using Gitunia.Git.Staging;
using Gitunia.Tui.Tests.App;
using Xunit;

namespace Gitunia.Tui.Tests.Fakes;

/// <summary>
/// What the effect runner does for staging, for tests of controllers that send staging commands: runs the real
/// <see cref="StagingEffectHandler"/> on the effects the reducer returns, posts the status messages and opens the dialogs the
/// reducer asks for and stands in for the tab refresher (<see cref="OnRefresh"/> runs on the UI pump before the result is
/// reduced, like a status read that has been applied).
/// </summary>
public sealed class StagingWiring : IStatusRefresher, IDisposable
{
    private readonly Dispatcher _host;
    private readonly UiPump _pump;
    private readonly StagingEffectHandler _handler;

    public StagingWiring(Dispatcher host, IIndexWriter writer, PatchStager stager, UiPump pump)
    {
        _host = host;
        _pump = pump;
        _handler = new StagingEffectHandler(writer, stager, this, host);
        host.EffectRaised += OnEffect;
    }

    /// <summary>Runs on the pump when a status read is requested, before the completion callback.</summary>
    public Action OnRefresh { get; set; } = () => { };

    /// <summary>Reads the status off the pump and returns what applies it on the pump; null runs <see cref="OnRefresh"/>
    /// instead.</summary>
    public Func<Task<Action>>? ReadStatus { get; set; }

    /// <summary>Called for every cursor move the reducer asks the terminal for.</summary>
    public Action<MoveDiffCursorEffect>? OnCursorMove { get; set; }

    /// <summary>The status reads that were requested.</summary>
    public int Refreshes { get; private set; }

    public void RefreshThen(TabId tabId, Action done)
    {
        Refreshes++;
        _ = Task.Run(() => ApplyAsync(done), CancellationToken.None);
    }

    public void Dispose() => _host.EffectRaised -= OnEffect;

    private async Task ApplyAsync(Action done)
    {
        var apply = OnRefresh;
        if (ReadStatus is { } read)
        {
            apply = await read();
        }

        _pump.Post(() =>
        {
            apply();
            done();
        });
    }

    private void OnEffect(AppEffect effect)
    {
        if (effect is StagingEffect staging)
        {
            _handler.Handle(staging, TestContext.Current.CancellationToken);
        }
        else if (effect is PostStatusEffect post)
        {
            _host.Dispatch(new StatusPostedCommand(post.Text, post.Severity, _host.Clock.GetUtcNow()));
        }
        else if (effect is ShowConfirmEffect confirm)
        {
            var refusal = new StatusPostedCommand(confirm.UnavailableNotice, StatusSeverity.Warning, _host.Clock.GetUtcNow());
            _host.Dispatch(new ConfirmRequestedCommand(confirm.Prompt, confirm.OnAccepted, refusal));
        }
        else if (effect is MoveDiffCursorEffect move)
        {
            OnCursorMove?.Invoke(move);
        }
    }
}
