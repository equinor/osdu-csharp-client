using Equinor.OsduCsharpClient.Facade;
using Xunit;

namespace OsduCsharpClient.Tests;

public class OsduConfigTests
{
    [Fact]
    public void UrlFor_KnownService_ReturnsServerPlusEndpoint()
    {
        var config = MakeConfig("https://osdu.example.com");
        Assert.Equal("https://osdu.example.com/api/search/v2", config.UrlFor("search"));
        Assert.Equal("https://osdu.example.com/api/storage/v2", config.UrlFor("storage"));
        Assert.Equal("https://osdu.example.com/api/entitlements/v2", config.UrlFor("entitlements"));
    }

    [Fact]
    public void UrlFor_TrimsTrailingSlash()
    {
        var config = MakeConfig("https://osdu.example.com/");
        Assert.Equal("https://osdu.example.com/api/search/v2", config.UrlFor("search"));
    }

    [Fact]
    public void UrlFor_UnknownService_ThrowsOsduException()
    {
        var config = MakeConfig("https://osdu.example.com");
        Assert.Throws<OsduException>(() => config.UrlFor("nonexistent_service"));
    }

    [Fact]
    public void UrlFor_EndpointOverride_ReturnsOverride()
    {
        var config = MakeConfig("https://osdu.example.com") with
        {
            EndpointOverrides = new Dictionary<string, string>
            {
                ["search"] = "https://custom.example.com/search"
            }
        };
        Assert.Equal("https://custom.example.com/search", config.UrlFor("search"));
    }

    [Fact]
    public void ScopesArray_SplitsOnSpaces()
    {
        var config = MakeConfig("https://x.com") with
        {
            Scopes = "scope1 scope2 scope3"
        };
        Assert.Equal(["scope1", "scope2", "scope3"], config.ScopesArray);
    }

    [Fact]
    public void FromEnvironment_MissingRequired_ThrowsOsduException()
    {
        // Ensure none of the required vars are set
        Environment.SetEnvironmentVariable("SERVER", null);
        Assert.Throws<OsduException>(() => OsduConfig.FromEnvironment());
    }

    [Fact]
    public void FromEnvironment_AllSet_ReturnsConfig()
    {
        Environment.SetEnvironmentVariable("SERVER", "https://test.example.com");
        Environment.SetEnvironmentVariable("DATA_PARTITION_ID", "test-partition");
        Environment.SetEnvironmentVariable("AUTHORITY", "https://login.microsoftonline.com/tenant");
        Environment.SetEnvironmentVariable("CLIENT_ID", "client-id");
        Environment.SetEnvironmentVariable("SCOPES", "https://test.example.com/.default");

        try
        {
            var config = OsduConfig.FromEnvironment();
            Assert.Equal("https://test.example.com", config.Server);
            Assert.Equal("test-partition", config.DataPartitionId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SERVER", null);
            Environment.SetEnvironmentVariable("DATA_PARTITION_ID", null);
            Environment.SetEnvironmentVariable("AUTHORITY", null);
            Environment.SetEnvironmentVariable("CLIENT_ID", null);
            Environment.SetEnvironmentVariable("SCOPES", null);
        }
    }

    private static OsduConfig MakeConfig(string server) => new()
    {
        Server = server,
        DataPartitionId = "test",
        Authority = "https://login.microsoftonline.com/tenant",
        ClientId = "client-id",
        Scopes = "https://example.com/.default",
    };
}
