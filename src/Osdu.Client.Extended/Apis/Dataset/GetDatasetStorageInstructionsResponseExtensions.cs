using System.Text.Json;
using Osdu.Client.Apis.Dataset;

namespace Osdu.Client.Extended.Apis.Dataset;

/// <summary>
/// Extension methods for <see cref="GetDatasetStorageInstructionsResponse"/> to provide
/// strongly-typed access to provider-specific storage locations.
/// </summary>
public static class GetDatasetStorageInstructionsResponseExtensions
{
    /// <summary>
    /// Deserializes the <see cref="GetDatasetStorageInstructionsResponse.StorageLocation"/>
    /// dictionary to an <see cref="AzureStorageLocation"/>.
    /// </summary>
    public static AzureStorageLocation? ToAzureStorageLocation(this GetDatasetStorageInstructionsResponse response)
    {
        if (response.StorageLocation is null)
            return null;

        string json = JsonSerializer.Serialize(response.StorageLocation);
        return JsonSerializer.Deserialize<AzureStorageLocation>(json);
    }
}
