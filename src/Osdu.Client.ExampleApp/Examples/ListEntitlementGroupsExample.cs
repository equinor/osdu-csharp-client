using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.ExampleApp.ExamplesBuilder;

namespace Osdu.Client.ExampleApp.Examples;

public class ListEntitlementGroupsExample(IOsduClient osduClient) : ExampleBase
{
    public override string Text => "List Entitlement Groups";

    public override string ShortDescription => "Retrieves all entitlement groups for the current data partition.";

    public override string Category => "Entitlement";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.Entitlement.GetGroupsAsync(cancellationToken: cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
