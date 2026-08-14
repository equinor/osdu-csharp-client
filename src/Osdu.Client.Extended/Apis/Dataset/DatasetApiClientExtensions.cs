using Osdu.Client.Apis.Dataset;

namespace Osdu.Client.Extended.Apis.Dataset;

/// <summary>
/// Convenience extension methods for <see cref="IDatasetApiClient"/>.
/// </summary>
public static class DatasetApiClientExtensions
{
    /// <summary>
    /// Requests storage instructions and returns the result as a strongly-typed
    /// <see cref="AzureStorageLocation"/>.
    /// </summary>
    public static async Task<AzureStorageLocation?> GetAzureStorageLocationAsync(this IDatasetApiClient client, string kindSubType, string? expiryTime = null, CancellationToken cancellationToken = default)
    {
        var response = await client.PostStorageInstructionsAsync(kindSubType, expiryTime!, cancellationToken);
        return response.ToAzureStorageLocation();
    }
}
