using System.Reflection;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Osdu.Client.ExampleApp.Authentication;
using Osdu.Client.Extensions;

namespace Osdu.Client.ExampleApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets(Assembly.GetExecutingAssembly())
            .Build();

        string scope = configuration["OsduScope"]!;
        string baseUrl = configuration["OsduBaseUrl"]!;
        string dataPartitionId = configuration["OsduDataPartitionId"]!;
        string tenantId = configuration["AzureTenantId"]!;
        string clientId = configuration["OsduClientId"]!;
        string clientSecret = configuration["OsduClientSecret"]!;

        var services = new ServiceCollection();

        services.AddTransient<OsduAuthHandler>(_ =>
            new OsduAuthHandler(tenantId, clientId, clientSecret, scope));

        // Register a named HttpClient for the dynamic API explorer
        services.AddHttpClient("OsduApi", client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("data-partition-id", dataPartitionId);
        }).AddHttpMessageHandler<OsduAuthHandler>();

        services.AddOsduApiClients(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("data-partition-id", dataPartitionId);
        });

        // Attach the auth handler to all OSDU-registered HttpClients
        services.ConfigureAll<HttpClientFactoryOptions>(options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder =>
            {
                builder.AdditionalHandlers.Add(builder.Services.GetRequiredService<OsduAuthHandler>());
            });
        });

        services.AddTransient<MainWindow>();
        services.AddTransient<ApiTestWindow>();

        Services = services.BuildServiceProvider();

        ApiTestWindow testWindow = Services.GetRequiredService<ApiTestWindow>();
        testWindow.Show();
    }
}
