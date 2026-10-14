using Gitunia.CustomRenderTui.Demo.Views;

namespace Gitunia.CustomRenderTui.Demo;

public sealed partial class App
{
    private async Task LoadRefsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var text = await GitText.RunAsync(_repositoryPath, ["for-each-ref", "--format=%(refname)", "refs/heads", "refs/remotes", "refs/tags"], cancellationToken).ConfigureAwait(false);
            var branches = new List<SidebarItem>();
            var tags = new List<SidebarItem>();
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("refs/tags/", StringComparison.Ordinal))
                {
                    tags.Add(new SidebarItem(line["refs/tags/".Length..], false));
                }
                else
                {
                    branches.Add(new SidebarItem(line[(line.IndexOf('/', 5) + 1)..], false));
                }
            }

            var items = new List<SidebarItem> { new($"Branches ({branches.Count})", true) };
            items.AddRange(branches);
            items.Add(new SidebarItem($"Tags ({tags.Count})", true));
            items.AddRange(tags);
            _events.Writer.TryWrite(new Refs(items));
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Loads `git show --stat` for the selected commit; a newer selection cancels the older request.</summary>
    private void RequestDetails()
    {
        if (_graph.Count == 0)
        {
            return;
        }

        var hash = _graph.Store.Hash(_graphCursor.Selection);
        if (hash == _detailHash)
        {
            return;
        }

        _detailHash = hash;
        _detailCancellationTokenSource?.Cancel();
        _detailCancellationTokenSource?.Dispose();
        _detailCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _detailCancellationTokenSource.Token;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(40, cancellationToken).ConfigureAwait(false); // Coalesce held keys into one git spawn
                    var text = await GitText.RunAsync(_repositoryPath, ["show", "--stat", "--no-color", "--format=fuller", hash], cancellationToken).ConfigureAwait(false);
                    _events.Writer.TryWrite(new DetailsLoaded(hash, text));
                }
                catch (OperationCanceledException)
                {
                }
            },
            cancellationToken);
    }
}
