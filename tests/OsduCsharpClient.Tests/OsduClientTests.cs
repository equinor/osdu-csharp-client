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
        Assert.NotNull(client.SchemaService);
        Assert.NotNull(client.Entitlements);
        Assert.NotNull(client.Legal);
        Assert.NotNull(client.File);
        Assert.NotNull(client.Dataset);
        Assert.NotNull(client.Indexer);
        Assert.NotNull(client.Notification);
        Assert.NotNull(client.Partition);
        Assert.NotNull(client.Policy);
        Assert.NotNull(client.Register);
        Assert.NotNull(client.UnitV2);
        Assert.NotNull(client.UnitV3);
        Assert.NotNull(client.CrsCatalog);
        Assert.NotNull(client.CrsConversion);
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
    public void Constructor_Throws_WhenTokenProviderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new OsduClient(MakeConfig(), null!));
    }

    [Fact]
    public void TransportHandler_RetiresPooledConnectionsByAge()
    {
        // The framework default is infinite, which lets a continuously busy connection
        // stay pinned to a stale address across a DNS change.
        using var handler = OsduClient.CreateTransportHandler();

        Assert.NotEqual(Timeout.InfiniteTimeSpan, handler.PooledConnectionLifetime);
        Assert.True(handler.PooledConnectionLifetime > TimeSpan.Zero);
        Assert.Equal(OsduClient.PooledConnectionLifetime, handler.PooledConnectionLifetime);
    }

    [Fact]
    public void TransportHandler_KeepsHttpClientHandlerDefaults()
    {
        // SocketsHttpHandler is used only to reach PooledConnectionLifetime; nothing else
        // about the transport should change from the HttpClientHandler it replaced.
        using var reference = new HttpClientHandler();
        using var handler = OsduClient.CreateTransportHandler();

        Assert.Equal(reference.AutomaticDecompression, handler.AutomaticDecompression);
        Assert.Equal(reference.UseProxy, handler.UseProxy);
        Assert.Equal(reference.UseCookies, handler.UseCookies);
        Assert.Equal(reference.AllowAutoRedirect, handler.AllowAutoRedirect);
        Assert.Equal(reference.MaxAutomaticRedirections, handler.MaxAutomaticRedirections);
        Assert.Equal(reference.MaxConnectionsPerServer, handler.MaxConnectionsPerServer);
        Assert.Equal(reference.PreAuthenticate, handler.PreAuthenticate);
    }
}
