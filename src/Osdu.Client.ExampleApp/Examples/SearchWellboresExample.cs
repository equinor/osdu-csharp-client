using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.Apis.Search;

namespace Osdu.Client.ExampleApp.Examples;

public class SearchWellboresExample(IOsduClient osduClient) : ExampleBase
{
    public override string Text => "Search Wellbores";

    public override string ShortDescription => "Searches for Wellbore records using the Search API with kind 'osdu:wks:master-data--Wellbore:1.3.0'.";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var request = new QueryRequest
        {
            Kind = "osdu:wks:master-data--Wellbore:1.3.0",
            Limit = 20,
            Query = "*"
        };

        var response = await osduClient.Search.PostQueryAsync(request, cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
