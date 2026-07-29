using System.Windows;
using Osdu.Client.Apis.Search;
using Osdu.Client.Extensions;
using Osdu.Client.Schemas.MasterData;

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
        ResultsListBox.Items.Clear();

        try
        {
            var request = new QueryRequest
            {
                Kind = KindTextBox.Text,
                Limit = 20,
                Query = "*"
            };

            QueryResponse? response = await _searchClient.PostQueryAsync(request);

            IEnumerable<Wellbore_1_3_0> list = response.Results.DeserializeList<Wellbore_1_3_0>();

            StatusText.Text = $"Found {response.TotalCount} result(s).";

            foreach (var result in response.Results ?? [])
            {
                ResultsListBox.Items.Add(result.ToString());
            }
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
