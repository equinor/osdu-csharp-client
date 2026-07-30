using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.Apis.Search;

namespace Osdu.Client.ExampleApp.Examples;

public class SearchWellLogsExample(IOsduClient osduClient) : ExampleBase
{
    public override string Text => "Search Well Logs";

    public override string ShortDescription => "Searches for WellLog work product components with projected fields (id, kind, createTime).";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var request = new QueryRequest
        {
            Kind = "osdu:wks:work-product-component--WellLog:*",
            Query = "*",
            Limit = 10,
            ReturnedFields = ["id", "kind", "createTime"],
        };

        var response = await osduClient.Search.PostQueryAsync(request, cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
