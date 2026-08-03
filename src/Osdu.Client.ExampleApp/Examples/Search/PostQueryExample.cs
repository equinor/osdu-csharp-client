using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.Apis.Search;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;

namespace Osdu.Client.ExampleApp.Examples.Search;

public class PostQueryExample(IOsduClient osduClient) : ExampleBase
{
    public override string Category => ExampleCategory.Search;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => $"This is an example for 'OsduClient.{Text}' api endpoint";

    [ExampleParameter(DisplayName = "Kind", Required = true, Order = 0, Description = "The kind of record to search for (supports wildcards).")]
    public string Kind { get; set; } = "osdu:wks:master-data--Wellbore:1.3.0";

    [ExampleParameter(DisplayName = "Query", Order = 1, Description = "Lucene query string.")]
    public string Query { get; set; } = "*";

    [ExampleParameter(DisplayName = "Limit", Order = 2, Description = "Maximum number of results to return.")]
    public int Limit { get; set; } = 20;

    [ExampleParameter(DisplayName = "Returned Fields", Order = 3, Description = "Comma-separated list of fields to return.")]
    public string[] ReturnedFields { get; set; } = []; //["id", "kind", "createTime"];

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var request = new QueryRequest
        {
            Kind = Kind,
            Limit = Limit,
            Query = Query,
            ReturnedFields = ReturnedFields.ToList()
        };

        var response = await osduClient.Search.PostQueryAsync(request, cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
