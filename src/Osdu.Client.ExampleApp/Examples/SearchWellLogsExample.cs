using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.Apis.Search;

namespace Osdu.Client.ExampleApp.Examples;

public class SearchWellLogsExample(IOsduClient osduClient) : ExampleBase
{
    [ExampleParameter(
        DisplayName = "Kind",
        Description = "The kind of WellLog record to search for.",
        Required = true,
        Order = 0)]
    public string Kind { get; set; } = "osdu:wks:work-product-component--WellLog:*";

    [ExampleParameter(
        DisplayName = "Limit",
        Description = "Maximum number of results to return.",
        Order = 1)]
    public int Limit { get; set; } = 10;

    public override string Text => "Search Well Logs";

    public override string ShortDescription => "Searches for WellLog work product components with projected fields (id, kind, createTime).";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var request = new QueryRequest
        {
            Kind = Kind,
            Query = "*",
            Limit = Limit,
            ReturnedFields = ["id", "kind", "createTime"],
        };

        var response = await osduClient.Search.PostQueryAsync(request, cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
