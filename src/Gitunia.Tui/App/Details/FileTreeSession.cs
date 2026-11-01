using Gitunia.App.Loading.Trees;
using Gitunia.Core.FileTree;

namespace Gitunia.Tui.App.Details;

/// <summary>
/// The Files tab of one repository tab: the store that lists the tree of the selected commit, the directories the
/// user opened (kept while the selection moves to other commits, so a path stays open) and the filter. The visible
/// rows are worked out from these and cached until one of them changes. UI thread only.
/// </summary>
public sealed class FileTreeSession(CommitTreeStore store) : IDisposable
{
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private IReadOnlyList<FileTreeRow>? _rows;
    private FileTreeModel? _rowsModel;

    public CommitTreeStore Store { get; } = store;

    /// <summary>The text a path must contain to be listed, empty for no filter.</summary>
    public string Filter { get; private set; } = string.Empty;

    /// <summary>The filter prompt is open (typed keys edit <see cref="Filter"/>).</summary>
    public bool IsEditingFilter { get; set; }

    /// <summary>The visible rows of the loaded tree; empty while it is not loaded.</summary>
    public IReadOnlyList<FileTreeRow> Rows
    {
        get
        {
            var model = Store.Current.Model;
            if (model is null)
            {
                return [];
            }

            if (_rows is null || !ReferenceEquals(_rowsModel, model))
            {
                _rows = model.Rows(_expanded, Filter);
                _rowsModel = model;
            }

            return _rows;
        }
    }

    public void SetFilter(string text)
    {
        if (text != Filter)
        {
            Filter = text;
            _rows = null;
        }
    }

    /// <summary>Opens a directory (no effect on files and on a directory that is open).</summary>
    /// <returns>True when the rows changed.</returns>
    public bool Expand(FileTreeRow row) =>
        row.Kind == FileTreeRowKind.Directory && Filter.Length == 0 && Set(row, true);

    /// <summary>Closes a directory.</summary>
    /// <returns>True when the rows changed.</returns>
    public bool Collapse(FileTreeRow row) =>
        row.Kind == FileTreeRowKind.Directory && Filter.Length == 0 && Set(row, false);

    public void Dispose() => Store.Dispose();

    private bool Set(FileTreeRow row, bool open)
    {
        var changed = open ? _expanded.Add(row.Path) : _expanded.Remove(row.Path);
        if (changed)
        {
            _rows = null;
        }

        return changed;
    }
}
