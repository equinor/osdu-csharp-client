using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.ExampleApp.ExamplesBuilder;

namespace Osdu.Client.ExampleApp.Examples;

public class EntitlementInfoExample(IOsduClient osduClient) : ExampleBase
{
    public override string Text => "Entitlement Version Info";

    public override string ShortDescription => "Retrieves version information from the Entitlement service.";

    public override string Category => "Entitlement";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.Entitlement.GetInfoAsync(cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
