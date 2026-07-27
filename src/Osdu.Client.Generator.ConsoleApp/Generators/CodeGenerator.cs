using Microsoft.Extensions.Logging;
using Osdu.Client.Generator.ConsoleApp.Configuration;
using Osdu.Client.Generator.ConsoleApp.Extensions;

namespace Osdu.Client.Generator.ConsoleApp.Generators;

public class CodeGenerator
{
    private readonly ILogger<CodeGenerator> _logger;
    private readonly AppConfiguration _configuration;
    private readonly ApiGenerator _apiGenerator;
    private readonly SchemaGenerator _schemaGenerator;

    public CodeGenerator(ILogger<CodeGenerator> logger, AppConfiguration configuration, ApiGenerator apiGenerator, SchemaGenerator schemaGenerator)
    {
        _logger = logger;
        _configuration = configuration;
        _apiGenerator = apiGenerator;
        _schemaGenerator = schemaGenerator;
    }

    public void Generate()
    {
        GenerateApiClientsAndSchemas();
        GenerateDataSchemas();
    }

    private void GenerateApiClientsAndSchemas()
    {
        _logger.LogInformation("Generating API clients and schemas...");

        if (!Directory.Exists(_configuration.Api.DefinitionsDir))
        {
            _logger.LogError($"No API clients/schemas generated because API definitions directory not found: {_configuration.Api.DefinitionsDir}");
            return;
        }

        if (!Directory.Exists(_configuration.Api.OutputDir))
        {
            Directory.CreateDirectory(_configuration.Api.OutputDir);
        }

        _logger.LogInformation($"  Reading API definitions from directory: {_configuration.Api.DefinitionsDir}");
        string[] jsonFiles = Directory.GetFiles(_configuration.Api.DefinitionsDir, "*.json", SearchOption.AllDirectories);

        _logger.LogInformation($"  Found {jsonFiles.Length} API definitions");

        foreach (string jsonFile in jsonFiles)
        {
            _logger.LogInformation($"  Building API client/schema from definition file: {jsonFile}");

            // Generate API client
            _apiGenerator.Generate(jsonFile, _configuration.Api.OutputDir, _configuration.Api.Namespace);

            // Generate API schema
            string apiClientName = Path.GetFileNameWithoutExtension(jsonFile).ToPascalCase();
            string outputDir = Path.Combine(_configuration.Api.OutputDir, apiClientName);

            _schemaGenerator.GenerateNew(jsonFile, outputDir, _configuration.Api.Namespace);

            //break; // Remove this break statement to process all files        
        }

    }

    private void GenerateApiSchemas()
    {
        _logger.LogInformation("Generating API schemas...");

        if (!Directory.Exists(_configuration.Api.DefinitionsDir))
        {
            _logger.LogError($"No API schemas generated because API definitions directory not found: {_configuration.Api.DefinitionsDir}");
            return;
        }

    }

    private void GenerateDataSchemas()
    {
        _logger.LogInformation("Generating Data schemas...");
    }
}
