using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.Apis.Search;
using Osdu.Client.ExampleApp.ExamplesBuilder;
using Osdu.Client.ExampleApp.Extensions;
using Osdu.Client.Extensions;
using Osdu.Client.Schemas.MasterData;
using Osdu.Client.Schemas.WorkProductComponent;

namespace Osdu.Client.ExampleApp.Examples;

public class PostQueryWellLogsExample(IOsduClient osduClient) : ExampleBase
{
    public override string Category => ExampleCategory.Search;
    public override string Text => $"{Category}.{GetType().Name.RemoveExample()}";
    public override string ShortDescription => $"This is an example for 'OsduClient.Search.PostQuery' api endpoint with strongly-typed 'Wellbore_1_3_0' result";

    [ExampleParameter(DisplayName = "Kind", Required = true, Order = 0, Description = "The kind of WellLog record to search for.")]
    public string Kind { get; set; } = "osdu:wks:work-product-component--WellLog:1.3.0";

    [ExampleParameter(DisplayName = "Limit", Order = 1, Description = "Maximum number of results to return.")]
    public int Limit { get; set; } = 10;

    [ExampleParameter(DisplayName = "Returned Fields", Order = 2, Description = "Comma-separated list of fields to return.")]
    public string[] ReturnedFields { get; set; } = []; //["id", "kind", "createTime"];

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var request = new QueryRequest
        {
            Kind = Kind,
            Query = "*",
            Limit = Limit,
            ReturnedFields = ReturnedFields.ToList(),
        };

        var response = await osduClient.Search.PostQueryAsync(request, cancellationToken);

        IEnumerable<WellLog_1_3_0> wellbores = response.Results.DeserializeList<WellLog_1_3_0>();

        return JsonSerializer.Serialize(wellbores, new JsonSerializerOptions { WriteIndented = true });

        //return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
