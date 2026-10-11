namespace Gitunia.Git;

/// <summary>Parses <c>git worktree list --porcelain -z</c>: NUL-terminated attributes, an extra NUL ends each record.</summary>
public static class WorktreeParser
{
    public static ParseResult<WorktreeInfo> Parse(string output)
    {
        var worktrees = new List<WorktreeInfo>();
        var warnings = new List<string>();
        var attributes = new List<string>();

        // An empty segment is the record terminator. Output without the final terminator is tolerated.
        foreach (string segment in output.Split('\0'))
        {
            if (segment.Length > 0)
            {
                attributes.Add(segment);
                continue;
            }

            Flush(attributes, worktrees, warnings);
        }

        Flush(attributes, worktrees, warnings);
        return new ParseResult<WorktreeInfo>(worktrees, warnings);
    }

    private static void Flush(List<string> attributes, List<WorktreeInfo> worktrees, List<string> warnings)
    {
        if (attributes.Count == 0)
        {
            return;
        }

        string? path = null;
        string? head = null;
        string? branch = null;
        bool detached = false;
        bool bare = false;
        bool locked = false;
        string? lockReason = null;
        bool prunable = false;
        string? pruneReason = null;

        foreach (string attribute in attributes)
        {
            (string key, string value) = SplitAttribute(attribute);
            switch (key)
            {
                case "worktree":
                    path = value;
                    break;
                case "HEAD":
                    head = value.Trim('0').Length == 0 ? null : value;
                    break;
                case "branch":
                    branch = value;
                    break;
                case "detached":
                    detached = true;
                    break;
                case "bare":
                    bare = true;
                    break;
                case "locked":
                    locked = true;
                    lockReason = value.Length > 0 ? value : null;
                    break;
                case "prunable":
                    prunable = true;
                    pruneReason = value.Length > 0 ? value : null;
                    break;
                default:
                    warnings.Add($"Ignored unknown worktree attribute '{attribute}'");
                    break;
            }
        }

        attributes.Clear();
        if (path is null)
        {
            warnings.Add("Skipped a worktree record without a path");
            return;
        }

        worktrees.Add(new WorktreeInfo(path, head, branch, detached, bare, locked, lockReason, prunable, pruneReason, worktrees.Count == 0));
    }

    private static (string Key, string Value) SplitAttribute(string attribute)
    {
        int space = attribute.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? (attribute, string.Empty) : (attribute[..space], attribute[(space + 1)..]);
    }
}
