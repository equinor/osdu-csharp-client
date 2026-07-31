using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.ExampleApp.ExamplesBuilder;

namespace Osdu.Client.ExampleApp.Examples;

public class SchemaInfoExample(IOsduClient osduClient) : ExampleBase
{
    public override string Text => "Schema Version Info";

    public override string ShortDescription => "Retrieves version information from the Schema service.";

    public override string Category => "Schema";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.Schema.GetInfoAsync(cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
