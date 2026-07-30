using System.Text.Json;
using Osdu.Client.Apis;

namespace Osdu.Client.ExampleApp.Examples;

public class GetStorageRecordsExample(IOsduClient osduClient) : ExampleBase
{
    [ExampleParameter(DisplayName = "Limit", Order = 0, Description = "Maximum number of records to retrieve.")]
    public int Limit { get; set; } = 10;

    public override string Text => "Get Storage Records";

    public override string ShortDescription => "Retrieves records from the Storage API.";

    public override string Category => "Storage";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.Storage.GetRecordsAsync(Limit);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
