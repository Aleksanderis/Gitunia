using Gitunia.Core.Details;
using Gitunia.Core.FileTree;

namespace Gitunia.App.Loading.Trees;

/// <summary>
/// The Files tab of one repository tab: the store that lists the tree of the selected commit and the cache of its
/// visible rows. What decides the rows (the directories the user opened and the filter) is the tab's
/// <see cref="DetailTreeState"/>; the rows are worked out from it and cached until the loaded tree or that state
/// changes. UI thread only.
/// </summary>
public sealed class FileTreeSession(CommitTreeStore store) : IDisposable
{
    private IReadOnlyList<FileTreeRow>? _rows;
    private FileTreeModel? _rowsModel;
    private DetailTreeState? _rowsState;

    public CommitTreeStore Store { get; } = store;

    /// <summary>The visible rows of the loaded tree for the opened directories and the filter; empty while it is not loaded.</summary>
    /// <param name="state">The tab's file tree state.</param>
    public IReadOnlyList<FileTreeRow> RowsFor(DetailTreeState state)
    {
        var model = Store.Current.Model;
        if (model is null)
        {
            return [];
        }

        if (_rows is null
            || !ReferenceEquals(_rowsModel, model)
            || !ReferenceEquals(_rowsState?.OpenDirectories, state.OpenDirectories)
            || _rowsState.Filter != state.Filter)
        {
            _rows = model.Rows(state.OpenDirectories, state.Filter);
            _rowsModel = model;
            _rowsState = state;
        }

        return _rows;
    }

    public void Dispose() => Store.Dispose();
}
