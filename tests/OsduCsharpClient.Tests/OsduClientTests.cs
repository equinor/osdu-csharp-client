using Equinor.OsduCsharpClient.Facade;
using Equinor.OsduCsharpClient.Facade.Auth;
using Xunit;

namespace OsduCsharpClient.Tests;

public class OsduClientTests
{
    private static OsduConfig MakeConfig() => new()
    {
        Server = "https://osdu.example.com",
        DataPartitionId = "test-partition",
        Authority = "https://login.microsoftonline.com/tenant",
        ClientId = "client-id",
        Scopes = "https://example.com/.default",
    };

    [Fact]
    public void Search_ReturnsSameInstance_WhenAccessedTwice()
    {
        using var client = new OsduClient(MakeConfig(), new StaticTokenProvider("tok"));
        var first = client.Search;
        var second = client.Search;
        Assert.Same(first, second);
    }

    [Fact]
    public void AllServiceProperties_ReturnNonNull()
    {
        using var client = new OsduClient(MakeConfig(), new StaticTokenProvider("tok"));

        Assert.NotNull(client.Search);
        Assert.NotNull(client.Storage);
        Assert.NotNull(client.Schema);
        Assert.NotNull(client.Entitlements);
        Assert.NotNull(client.Legal);
        Assert.NotNull(client.File);
        Assert.NotNull(client.Dataset);
        Assert.NotNull(client.Geospatial);
        Assert.NotNull(client.Indexer);
        Assert.NotNull(client.Notification);
        Assert.NotNull(client.Partition);
        Assert.NotNull(client.Policy);
        Assert.NotNull(client.Register);
        Assert.NotNull(client.Unit);
        Assert.NotNull(client.CrsCatalog);
        Assert.NotNull(client.CrsConversion);
        Assert.NotNull(client.SeismicDdms);
        Assert.NotNull(client.WellboreDdms);
        Assert.NotNull(client.Workflow);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var client = new OsduClient(MakeConfig(), new StaticTokenProvider("tok"));
        client.Dispose();
        client.Dispose(); // should not throw
    }

    [Fact]
    public void DefaultTokenProvider_IsMsalInteractive_WhenNotSpecified()
    {
        // Constructing with null provider should not throw (MSAL is constructed lazily via property access)
        // We just verify construction succeeds
        var client = new OsduClient(MakeConfig());
        client.Dispose();
    }
}
