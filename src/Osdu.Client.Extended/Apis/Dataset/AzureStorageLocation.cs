using System.Text.Json.Serialization;

namespace Osdu.Client.Extended.Apis.Dataset;

public class AzureStorageLocation
{
    [JsonPropertyName("signedUrl")]
    public string SignedUrl { get; init; }

    /// <summary>Relative path within the staging or persistent container.</summary>
    [JsonPropertyName("fileSource")]
    public string FileSource { get; init; }

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; init; }

    [JsonPropertyName("expiryTime")]
    public DateTime ExpiryTime { get; init; }
}
