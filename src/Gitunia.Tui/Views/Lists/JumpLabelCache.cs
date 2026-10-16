using Gitunia.Core.Jump;
using Gitunia.Core.Jump.Models;

namespace Gitunia.Tui.Views.Lists;

/// <summary>
/// The quick-jump labels of a list, worked out once per frame instead of once per row. A list source owns one and asks it for
/// the labels of the row it draws; the quick-jump prompt asks the same source for the same labels, so what is drawn is what a key
/// selects.
/// </summary>
public sealed class JumpLabelCache
{
    private readonly Dictionary<int, JumpLabel> _byRow = [];
    private IReadOnlyList<JumpLabel> _labels = [];
    private JumpKey? _key;

    /// <summary>The text of an open quick jump, whose targets in view get labels; null when none is open.</summary>
    public string? Text { get; set; }

    /// <summary>Forgets the labels because the rows changed under them.</summary>
    public void Invalidate() => _key = null;

    /// <summary>The targets of <paramref name="text"/> in view, nearest to the cursor first.</summary>
    /// <param name="text">The typed text.</param>
    /// <param name="window">The rows in view.</param>
    /// <param name="stamp">Tells apart the states of the rows (see <see cref="JumpKey"/>).</param>
    /// <param name="rows">Creates the rows to look through; only called when the labels are not up to date.</param>
    public IReadOnlyList<JumpLabel> Labels(string text, JumpWindow window, int stamp, Func<IJumpRows> rows)
    {
        var key = new JumpKey(text, window, stamp);
        if (_key != key)
        {
            _key = key;
            _labels = QuickJumpLabeler.Compute(text, rows(), window);
            _byRow.Clear();
            foreach (var label in _labels)
            {
                _byRow[label.Row] = label;
            }
        }

        return _labels;
    }

    /// <summary>The label of a row, null when it has none or no jump is open. Valid after <see cref="Labels"/> for the frame.</summary>
    /// <param name="row">The row.</param>
    public JumpLabel? LabelOf(int row) => _byRow.TryGetValue(row, out var found) ? found : null;
}
