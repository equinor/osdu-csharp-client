using System.Text.Json;
using Osdu.Client.Apis;

namespace Osdu.Client.ExampleApp.Examples;

public class GetStorageRecordsExample(IOsduClient osduClient) : ExampleBase
{
    public override string Text => "Get Storage Records";

    public override string ShortDescription => "Retrieves up to 10 records from the Storage API.";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.Storage.GetRecordsAsync(10);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
