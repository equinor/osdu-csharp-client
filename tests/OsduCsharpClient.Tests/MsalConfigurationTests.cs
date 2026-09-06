using Equinor.OsduCsharpClient.Facade;
using Equinor.OsduCsharpClient.Facade.Auth;
using Xunit;

namespace OsduCsharpClient.Tests;

public class MsalConfigurationTests
{
    private static OsduConfig Config => new()
    {
        Server = "https://osdu.example.com",
        DataPartitionId = "test",
        Authority = "https://login.microsoftonline.com/organizations",
        ClientId = "00000000-0000-0000-0000-000000000001",
        Scopes = "https://example.com/.default",
    };

    [Theory]
    [InlineData("Authority", "")]
    [InlineData("Authority", " ")]
    [InlineData("ClientId", "")]
    [InlineData("ClientId", null)]
    [InlineData("Scopes", "")]
    [InlineData("Scopes", " ")]
    public void AllProviders_RejectMissingIdentitySettings(string property, string? value)
    {
        var config = property switch
        {
            "Authority" => Config with { Authority = value! },
            "ClientId" => Config with { ClientId = value! },
            "Scopes" => Config with { Scopes = value! },
            _ => throw new ArgumentOutOfRangeException(nameof(property)),
        };

        Assert.Contains(property, Assert.Throws<OsduException>(
            () => new MsalInteractiveTokenProvider(config)).Message);
        Assert.Contains(property, Assert.Throws<OsduException>(
            () => new MsalDeviceFlowTokenProvider(config)).Message);
        Assert.Contains(property, Assert.Throws<OsduException>(
            () => new MsalClientCredentialsTokenProvider(config, "test-secret")).Message);
    }

    [Fact]
    public void AllProviders_AcceptCompleteSettingsWithoutAcquiringTokens()
    {
        Assert.NotNull(new MsalInteractiveTokenProvider(Config));
        Assert.NotNull(new MsalDeviceFlowTokenProvider(Config));
        Assert.NotNull(new MsalClientCredentialsTokenProvider(Config, "test-secret"));
    }

    [Fact]
    public void ClientCredentials_RequiresNonEmptySecret()
    {
        Assert.Throws<ArgumentException>(
            () => new MsalClientCredentialsTokenProvider(Config, " "));
    }
}
