using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.ExampleApp.ExamplesBuilder;

namespace Osdu.Client.ExampleApp.Examples;

public class IndexerInfoExample(IOsduClient osduClient) : ExampleBase
{
    public override string Text => "Indexer Version Info";

    public override string ShortDescription => "Retrieves version information from the Indexer service.";

    public override string Category => "Indexer";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.Indexer.GetInfoAsync(cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
