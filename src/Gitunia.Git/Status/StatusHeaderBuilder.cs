using System.Globalization;
using Gitunia.Git.Refs.Models;
using Gitunia.Git.Status.Models;

namespace Gitunia.Git.Status;

/// <summary>Collects the <c># branch.*</c> header lines.</summary>
internal sealed class StatusHeaderBuilder
{
    private const string UnbornObjectName = "(initial)";
    private const string DetachedBranchName = "(detached)";

    private string? _objectName;
    private string? _branchName;
    private string? _upstream;
    private int? _ahead;
    private int? _behind;

    public void Add(string line, List<string> warnings)
    {
        var body = line[2..];
        var space = body.IndexOf(' ', StringComparison.Ordinal);
        var key = space < 0 ? body : body[..space];
        var value = space < 0 ? string.Empty : body[(space + 1)..];
        switch (key)
        {
            case "branch.oid":
                _objectName = value;
                break;
            case "branch.head":
                _branchName = value;
                break;
            case "branch.upstream":
                _upstream = value;
                break;
            case "branch.ab":
                if (!TryParseCounts(value))
                {
                    warnings.Add($"Unrecognised ahead/behind counts '{value}'");
                }

                break;
            case "stash":
                break;
            default:
                warnings.Add($"Ignored unknown status header '{line}'");
                break;
        }
    }

    public StatusBranch Build()
    {
        var unborn = _objectName == UnbornObjectName;
        var commit = _objectName is null || unborn ? null : _objectName;
        if (_branchName == DetachedBranchName)
        {
            return new StatusBranch(new HeadState(HeadKind.Detached, null, commit), _upstream, _ahead, _behind);
        }

        var kind = unborn ? HeadKind.Unborn : HeadKind.Branch;

        return new StatusBranch(new HeadState(kind, _branchName, commit), _upstream, _ahead, _behind);
    }

    /// <summary>Reads <c>+2 -1</c>.</summary>
    private bool TryParseCounts(string value)
    {
        var parts = value.Split(' ');
        if (parts.Length == 2
            && parts[0].StartsWith('+')
            && parts[1].StartsWith('-')
            && int.TryParse(parts[0].AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var aheadCount)
            && int.TryParse(parts[1].AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var behindCount))
        {
            _ahead = aheadCount;
            _behind = behindCount;
            return true;
        }

        return false;
    }
}
