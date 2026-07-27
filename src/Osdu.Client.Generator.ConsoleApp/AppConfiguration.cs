using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Osdu.Client.Generator.ConsoleApp.Configuration;

public class AppConfiguration
{
    public string ApplicationDir { get; private set; }
    public string OutputBaseDir { get; private set; }

    public ApiConfiguration Api { get; init; }

    public SchemaConfiguration Schema { get; init; }

    public void ResolvePaths()
    {
        string appDir = GetAppDirectory();
        string parentDir = Directory.GetParent(appDir)?.FullName ?? throw new InvalidOperationException("Failed to get parent directory of source directory.");

        ApplicationDir = appDir;
        OutputBaseDir = Path.Combine(parentDir, "Osdu.Client");

        Api.DefinitionsDir = Path.Combine(appDir, Api.DefinitionsDir);
        Api.OutputDir = Path.Combine(OutputBaseDir, Api.OutputDir);

        Schema.DefinitionsDir = Path.Combine(appDir, Schema.DefinitionsDir);
        Schema.OutputDir = Path.Combine(OutputBaseDir, Schema.OutputDir);
    }

    static string GetAppDirectory([CallerFilePath] string sourceFilePath = "")
    {
        return Path.GetDirectoryName(sourceFilePath)!;
    }
}

public class ApiConfiguration
{
    [JsonPropertyName("definitionsDir")]
    public required string DefinitionsDir { get; set; }
    public required string OutputDir { get; set; }

    public required string Namespace { get; set; }
}

public class SchemaConfiguration
{
    public required string DefinitionsDir { get; set; }
    public required string OutputDir { get; set; }

    public required string Namespace { get; set; }
}
