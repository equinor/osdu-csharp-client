using System.Text.Json;
using System.Windows;
using Osdu.Client.Apis.Search;

namespace Osdu.Client.ExampleApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ISearchApiClient _searchClient;

    public MainWindow(ISearchApiClient searchClient)
    {
        InitializeComponent();
        _searchClient = searchClient;
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchButton.IsEnabled = false;
        StatusText.Text = "Searching...";
        ResultsTextBox.Clear();

        try
        {
            var request = new QueryRequest
            {
                Kind = KindTextBox.Text,
                Limit = 20,
                Query = "*"
            };

            QueryResponse? response = await _searchClient.PostQueryAsync(request);

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
