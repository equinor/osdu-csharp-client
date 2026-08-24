using Equinor.OsduCsharpClient.Facade;
using Xunit;

namespace OsduCsharpClient.Tests;

public class ServiceRegistryTests
{
    [Fact]
    public void AllExpectedServicesAreRegistered()
    {
        var expected = new[]
        {
            "search", "storage", "schema_service", "entitlements", "legal", "file",
            "dataset", "indexer", "notification", "partition", "policy",
            "register", "unit_v2", "unit_v3", "crs_catalog", "crs_conversion",
            "wellbore_ddms", "workflow",
        };

        foreach (var attr in expected)
            Assert.True(ServiceRegistry.ByAttr.ContainsKey(attr), $"Missing service: {attr}");
    }

    [Fact]
    public void ByAttr_ContainsEveryServiceAndAlias()
    {
        var expected = ServiceRegistry.Services.Count
                     + ServiceRegistry.Services.Sum(s => s.Aliases?.Count ?? 0);
        Assert.Equal(expected, ServiceRegistry.ByAttr.Count);
    }

    [Fact]
    public void AllDefaultEndpoints_StartWithSlash()
    {
        foreach (var spec in ServiceRegistry.Services)
            Assert.True(spec.DefaultEndpoint.StartsWith('/'),
                $"{spec.Attr} endpoint '{spec.DefaultEndpoint}' must start with '/'");
    }
}
