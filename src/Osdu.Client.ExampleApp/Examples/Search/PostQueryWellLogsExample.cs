using System.Text;
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

    [ExampleParameter(DisplayName = "Show Full Response", Order = 3, Description = "Whether to show the full response or only curve information (using strongly-typed result).")]
    public bool ShowFullResponse { get; set; } = false;

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

        if (ShowFullResponse)
        {
            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }

        StringBuilder sb = new StringBuilder();
        foreach (WellLog_1_3_0 wellLog in wellbores)
        {
            sb.AppendLine($"""
                          -----------------------------------------------------------------------------------------------
                          WellLogID: {wellLog.Id}
                          Curves: {wellLog.Data?.Curves?.Count ?? 0}
                          -----------------------------------------------------------------------------------------------
                          """);
            if (wellLog.Data?.Curves is not null)
            {
                foreach (WellLog_1_3_0DataCurves curve in wellLog.Data.Curves)
                {
                    sb.AppendLine($"CurveID={curve.CurveID}, Mnemonic={curve.Mnemonic}, CurveUnit={curve.CurveUnit}, CurveDescription={curve.CurveDescription}, LogCurveMainFamilyID={curve.LogCurveMainFamilyID}");
                }
            }
        }

        return sb.ToString();
    }
}
