namespace Gitunia.Git.Commits;

/// <summary>Reads the space-separated parent hashes of a commit as git prints them.</summary>
internal static class ParentIds
{
    private static readonly ObjectId[] NoParents = [];

    /// <summary>The parents in order (empty for a root commit), or null when a hash is malformed.</summary>
    public static ObjectId[]? Parse(ReadOnlySpan<byte> field)
    {
        if (field.Length == 0)
        {
            return NoParents;
        }

        var parents = new List<ObjectId>(2);
        while (field.Length > 0)
        {
            var space = field.IndexOf((byte)' ');
            var hash = space < 0 ? field : field[..space];
            if (!ObjectId.TryParse(hash, out var parent))
            {
                return null;
            }

            parents.Add(parent);
            field = space < 0 ? [] : field[(space + 1)..];
        }

        return [.. parents];
    }
}
