using Gitunia.Core.Text;
using Xunit;
using Xunit.Sdk;

namespace Gitunia.Core.Tests.Text;

[Trait("Os", "Independent")]
public class FileSizeTextTests
{
    [Fact]
    public void GivenAByteCount_WhenFormatting_ThenItUsesTheLargestFittingUnit()
    {
        var failures = new List<string>();
        (long bytes, string expected)[] rows =
        [
            (0, "0 B"),
            (1023, "1023 B"),
            (1024, "1.0 KB"),
            (1536, "1.5 KB"),
            (10 * 1024, "10 KB"),
            (1_500_000, "1.4 MB"),
            (5L * 1024 * 1024 * 1024, "5.0 GB"),
        ];

        foreach (var (bytes, expected) in rows)
        {
            try
            {
                CheckItUsesTheLargestFittingUnit(bytes, expected);
            }
            catch (XunitException exception)
            {
                failures.Add($"{bytes}, {expected}: {exception.Message}");
            }
        }

        Assert.Empty(failures);
    }

    private static void CheckItUsesTheLargestFittingUnit(long bytes, string expected)
    {
        Assert.Equal(expected, FileSizeText.Of(bytes));
    }
}
