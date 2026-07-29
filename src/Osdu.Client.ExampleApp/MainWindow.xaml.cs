using System.Text.Json;
using System.Windows;
using Osdu.Client.Apis;
using Osdu.Client.Apis.Search;

namespace Osdu.Client.ExampleApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IOsduClient _osduClient;

    public MainWindow(IOsduClient osduClient)
    {
        InitializeComponent();
        _osduClient = osduClient;
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchButton.IsEnabled = false;
        StatusText.Text = "Searching...";
        ResultsTextBox.Clear();

        try
        {
            QueryRequest request = new QueryRequest
            {
                Kind = KindTextBox.Text,
                Limit = 20,
                Query = "*"
            };

            QueryResponse? response = await _osduClient.Search.PostQueryAsync(request);

            StatusText.Text = $"Found {response.TotalCount} result(s).";

            string prettyJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            ResultsTextBox.Text = prettyJson;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            SearchButton.IsEnabled = true;
        }
    }
}
