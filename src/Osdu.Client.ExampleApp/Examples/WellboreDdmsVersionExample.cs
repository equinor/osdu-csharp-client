using System.Text.Json;
using Osdu.Client.Apis;
using Osdu.Client.ExampleApp.ExamplesBuilder;

namespace Osdu.Client.ExampleApp.Examples;

public class WellboreDdmsVersionExample(IOsduClient osduClient) : ExampleBase
{
    public override string Text => "Wellbore DDMS Version";

    public override string ShortDescription => "Calls the Wellbore DDMS /version endpoint to retrieve service version information.";

    public override string Category => "Wellbore DDMS";

    public override async Task<string> RunAsync(CancellationToken cancellationToken)
    {
        var response = await osduClient.WellboreDdms.GetVersionAsync(cancellationToken);

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }
}
