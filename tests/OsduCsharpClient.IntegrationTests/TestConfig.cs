using DotNetEnv;

namespace OsduCsharpClient.IntegrationTests;

/// <summary>
/// Configuration loaded from .env or environment variables.
/// Mirrors the Python CoreConfig in tests/config.py.
/// </summary>
public class TestConfig
{
    public string Server { get; }
    public string DataPartitionId { get; }
    public string Authority { get; }
    public string ClientId { get; }
    public string Scopes { get; }

    private string EntitlementsEndpoint { get; } = "/api/entitlements/v2";
    private string SearchEndpoint { get; } = "/api/search/v2";
    private string WellboreDdmsEndpoint { get; } = "/api/os-wellbore-ddms";

    public string EntitlementsUrl => $"{Server}{EntitlementsEndpoint}";
    public string SearchUrl => $"{Server}{SearchEndpoint}";
    public string WellboreDdmsUrl => $"{Server}{WellboreDdmsEndpoint}";

    public TestConfig()
    {
        Env.NoClobber().TraversePath().Load();

        Server = Required("SERVER").TrimEnd('/');
        DataPartitionId = Required("DATA_PARTITION_ID");
        Authority = Required("AUTHORITY");
        ClientId = Required("CLIENT_ID");
        Scopes = Required("SCOPES");
    }

    private static string Required(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Missing required environment variable: {key}");
}
