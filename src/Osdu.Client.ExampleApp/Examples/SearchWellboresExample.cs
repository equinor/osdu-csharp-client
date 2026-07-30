using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.Apis.Search;

namespace Osdu.Client.ExampleApp.Examples;

public class SearchWellboresExample(IOsduClient osduClient) : ExampleBase
{
    [ExampleParameter(
        DisplayName = "Kind",
        Description = "The kind of record to search for (supports wildcards).",
        Required = true,
        Order = 0)]
    public string Kind { get; set; } = "osdu:wks:master-data--Wellbore:1.3.0";

    [ExampleParameter(
        DisplayName = "Query",
        Description = "Lucene query string.",
        Order = 1)]
    public string Query { get; set; } = "*";

    [ExampleParameter(
        DisplayName = "Limit",
        Description = "Maximum number of results to return.",
        Order = 2)]
    public int Limit { get; set; } = 20;

    public override string Text => "Search Wellbores";

    public override string ShortDescription => "Searches for Wellbore records using the Search API.";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var request = new QueryRequest
        {
            Kind = Kind,
            Limit = Limit,
            Query = Query
        };

        var response = await osduClient.Search.PostQueryAsync(request, cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
