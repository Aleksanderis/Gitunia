using System.Globalization;
using Gitunia.Git;
using Gitunia.Git.Commits.Models;

namespace Gitunia.Core.Tests.Graph;

/// <summary>Synthetic commits for layout tests: commit <c>n</c> gets the id whose hex form is <c>n</c> zero-padded.</summary>
public static class GraphTestData
{
    public static ObjectId Id(int number) =>
        ObjectId.TryParse(number.ToString("x40", CultureInfo.InvariantCulture), out var objectId)
            ? objectId
            : throw new InvalidOperationException("Cannot build an id.");

    public static CommitInfo Commit(int number, params int[] parents) => new(
        Id(number),
        parents.Select(Id).ToArray(),
        "Test",
        "test@example.com",
        DateTimeOffset.UnixEpoch,
        $"Commit {number.ToString(CultureInfo.InvariantCulture)}",
        []);

    /// <summary>
    /// A random DAG of <paramref name="count"/> commits in topological order (index 1 is the newest): every commit has
    /// zero to three distinct parents among the next <paramref name="window"/> commits, and the last commit is a root.
    /// </summary>
    public static IReadOnlyList<CommitInfo> RandomHistory(int seed, int count, int window)
    {
        var random = new Random(seed);
        var commits = new List<CommitInfo>(count);
        for (var number = 1; number <= count; number++)
        {
            var candidates = Math.Min(window, count - number);
            var parents = new List<int>();
            if (candidates > 0)
            {
                var roll = random.Next(100);
                var parentCount = roll switch
                {
                    < 3 => 0,
                    < 75 => 1,
                    < 95 => 2,
                    _ => 3,
                };
                for (var attempt = 0; attempt < parentCount; attempt++)
                {
                    // Bias towards the immediate successor so histories stay mostly linear like real ones.
                    var offset = attempt == 0 && random.Next(100) < 70 ? 1 : random.Next(1, candidates + 1);
                    if (!parents.Contains(number + offset))
                    {
                        parents.Add(number + offset);
                    }
                }
            }

            commits.Add(Commit(number, parents.ToArray()));
        }

        return commits;
    }
}
