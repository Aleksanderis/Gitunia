using Gitunia.App.Blame;
using Xunit;

namespace Gitunia.App.Tests.Blame;

public class BlameStoreTests
{
    [Fact]
    public void GivenNewerData_WhenOlderDataArrivesLate_ThenTheNewerDataStays()
    {
        var store = new BlameStore();
        store.Publish(new BlameData(4, ["new"]));

        store.Publish(new BlameData(2, ["old"]));

        Assert.Equal(4, store.Current!.Generation);
    }

    [Fact]
    public void GivenDataOfAClosedRead_WhenClearing_ThenOnlyOlderOrEqualDataIsDropped()
    {
        var store = new BlameStore();
        store.Publish(new BlameData(4, ["x"]));

        store.Clear(2);
        Assert.NotNull(store.Current);

        store.Clear(4);
        Assert.Null(store.Current);
    }
}
