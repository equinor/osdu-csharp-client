using Equinor.OsduCsharpClient.Facade;
using Microsoft.Extensions.Configuration;
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
    public void FromConfiguration_MissingSection_ThrowsOsduException()
    {
        var configuration = new ConfigurationBuilder().Build();
        Assert.Throws<OsduException>(() => OsduConfig.FromConfiguration(configuration));
    }

    [Fact]
    public void FromConfiguration_MissingRequiredValue_ThrowsOsduException()
    {
        // Section exists but a required value (Scopes) is absent.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Osdu:Server"] = "https://test.example.com",
                ["Osdu:DataPartitionId"] = "test-partition",
                ["Osdu:Authority"] = "https://login.microsoftonline.com/tenant",
                ["Osdu:ClientId"] = "client-id",
            })
            .Build();

        var ex = Assert.Throws<OsduException>(() => OsduConfig.FromConfiguration(configuration));
        Assert.Contains("Scopes", ex.Message);
    }

    [Fact]
    public void FromConfiguration_AllSet_BindsConfig()
    {
        // Single binding smoke test: custom section name, defaults override,
        // and the EndpointOverrides dictionary.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MyOsdu:Server"] = "https://test.example.com",
                ["MyOsdu:DataPartitionId"] = "test-partition",
                ["MyOsdu:Authority"] = "https://login.microsoftonline.com/tenant",
                ["MyOsdu:ClientId"] = "client-id",
                ["MyOsdu:Scopes"] = "https://test.example.com/.default",
                ["MyOsdu:TimeoutSeconds"] = "45",
                ["MyOsdu:EndpointOverrides:search"] = "https://custom.example.com/search",
            })
            .Build();

        var config = OsduConfig.FromConfiguration(configuration, "MyOsdu");

        Assert.Equal("https://test.example.com", config.Server);
        Assert.Equal("test-partition", config.DataPartitionId);
        Assert.Equal(45.0, config.TimeoutSeconds);
        Assert.Equal("https://custom.example.com/search", config.UrlFor("search"));
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
