using System.Text;
using Microsoft.Extensions.Logging;
using Osdu.Client.Generator.ConsoleApp;
using Osdu.Client.Generator.ConsoleApp.Extensions;

namespace Osdu.Client.Generator.ConsoleApp.Generators.Api;

/// <summary>
/// Generates the ServicesExtension class that registers all generated API clients with DI.
/// </summary>
public class ServicesExtensionGenerator
{
    private readonly ILogger<ServicesExtensionGenerator> _logger;
    private readonly AppConfiguration _configuration;

    public ServicesExtensionGenerator(ILogger<ServicesExtensionGenerator> logger, AppConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Generates the ServicesExtension.cs file based on discovered API definition files.
    /// </summary>
    /// <param name="apiClientNames">List of PascalCase API client names (e.g., "Dataset", "Search", "Storage").</param>
    public void Generate(IReadOnlyList<string> apiClientNames)
    {
        string outputDir = _configuration.Extension.OutputDir;
        string extensionNamespace = _configuration.Extension.Namespace;
        string apiBaseNamespace = _configuration.Api.Namespace;

        Directory.CreateDirectory(outputDir);

        var sb = new StringBuilder();
        CodeGenerator.BuildAutogenComment(sb);
        BuildUsings(sb);

        // Add using for each API client namespace
        foreach (string name in apiClientNames.OrderBy(n => n))
        {
            sb.AppendLine($"using {apiBaseNamespace}.{name};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {extensionNamespace};");
        sb.AppendLine();
        sb.AppendLine("""
                      /// <summary>
                      /// Extension methods for registering OSDU API clients with dependency injection.
                      /// </summary>
                      public static class ServicesExtension
                      {
                          /// <summary>
                          /// Registers all generated OSDU API clients with the service collection.
                          /// </summary>
                          /// <param name="services">The service collection.</param>
                          /// <param name="configureClient">Optional configuration for the underlying <see cref="HttpClient"/>.</param>
                          /// <returns>The service collection for chaining.</returns>
                          public static IServiceCollection AddOsduApiClients(this IServiceCollection services, Action<HttpClient>? configureClient = null)
                          {
                      """);

        foreach (string name in apiClientNames.OrderBy(n => n))
        {
            sb.AppendLine($$"""
                                    services.AddHttpClient<I{{name}}ApiClient, {{name}}ApiClient>(client => { configureClient?.Invoke(client); });
                            """);
        }

        // Register the aggregated IOsduClient / OsduClient
        sb.AppendLine();
        sb.AppendLine("        services.AddScoped<IOsduClient, OsduClient>();");
        sb.AppendLine("""
                      
                              return services;
                          }
                      }
                      """);

        string outputFile = Path.Combine(outputDir, "ServicesExtension.cs");
        File.WriteAllText(outputFile, sb.ToString());

        _logger.LogInformation($"    Generated services extension: ServicesExtension.cs with {apiClientNames.Count} API client(s)");
    }

    public void BuildUsings(StringBuilder sb)
    {
        sb.AppendLine($"""
                       using System;
                       using System.Net.Http;
                       using Microsoft.Extensions.DependencyInjection;
                       using {_configuration.Api.Namespace};

                       """);
    }
}
