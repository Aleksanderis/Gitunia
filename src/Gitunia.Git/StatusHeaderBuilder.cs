using System.Globalization;

namespace Gitunia.Git;

/// <summary>Collects the <c># branch.*</c> header lines.</summary>
internal sealed class StatusHeaderBuilder
{
    private const string UnbornObjectName = "(initial)";
    private const string DetachedBranchName = "(detached)";

    private string? objectName;
    private string? branchName;
    private string? upstream;
    private int? ahead;
    private int? behind;

    public void Add(string line, List<string> warnings)
    {
        var body = line[2..];
        var space = body.IndexOf(' ', StringComparison.Ordinal);
        var key = space < 0 ? body : body[..space];
        var value = space < 0 ? string.Empty : body[(space + 1)..];
        switch (key)
        {
            case "branch.oid":
                objectName = value;
                break;
            case "branch.head":
                branchName = value;
                break;
            case "branch.upstream":
                upstream = value;
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
        var unborn = objectName == UnbornObjectName;
        var commit = objectName is null || unborn ? null : objectName;
        var head = branchName == DetachedBranchName
            ? new HeadState(HeadKind.Detached, null, commit)
            : new HeadState(unborn ? HeadKind.Unborn : HeadKind.Branch, branchName, commit);
        return new StatusBranch(head, upstream, ahead, behind);
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
            ahead = aheadCount;
            behind = behindCount;
            return true;
        }

        return false;
    }
}
