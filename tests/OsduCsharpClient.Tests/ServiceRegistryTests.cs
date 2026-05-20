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
            "search", "storage", "schema", "entitlements", "legal", "file",
            "dataset", "geospatial", "indexer", "notification", "partition", "policy",
            "register", "unit", "crs_catalog", "crs_conversion", "seismic_ddms",
            "wellbore_ddms", "workflow",
        };

        foreach (var attr in expected)
            Assert.True(ServiceRegistry.ByAttr.ContainsKey(attr), $"Missing service: {attr}");
    }

    [Fact]
    public void ByAttr_ContainsSameCountAsServices()
    {
        Assert.Equal(ServiceRegistry.Services.Count, ServiceRegistry.ByAttr.Count);
    }

    [Fact]
    public void AllDefaultEndpoints_StartWithSlash()
    {
        foreach (var spec in ServiceRegistry.Services)
            Assert.True(spec.DefaultEndpoint.StartsWith('/'),
                $"{spec.Attr} endpoint '{spec.DefaultEndpoint}' must start with '/'");
    }
}
