using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Osdu.Client.ExampleApp;
/// <summary>
/// Interaction logic for ApiTestWindow.xaml
/// </summary>
public partial class ApiTestWindow : Window
{
    private readonly string _definitionsPath;
    private readonly HttpClient _httpClient;

    public ApiTestWindow(IHttpClientFactory httpClientFactory)
    {
        InitializeComponent();
        _definitionsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Definitions", "Api");
        _httpClient = httpClientFactory.CreateClient("OsduApi");
        LoadApiFiles();
    }

    private void LoadApiFiles()
    {
        if (!Directory.Exists(_definitionsPath))
        {
            ResponseTextBox.Text = $"Definitions folder not found: {_definitionsPath}";
            return;
        }

        var jsonFiles = Directory.GetFiles(_definitionsPath, "*.json");

        foreach (var file in jsonFiles)
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(file);
            var button = new Button
            {
                Content = fileName.ToUpperInvariant(),
                Margin = new Thickness(0, 4, 0, 4),
                Padding = new Thickness(8, 6, 8, 6),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Tag = file
            };
            button.Click += ApiButton_Click;
            ApiButtonsPanel.Children.Add(button);
        }
    }

    private void ApiButton_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var filePath = (string)button.Tag;

        try
        {
            var json = File.ReadAllText(filePath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var title = root.GetProperty("info").GetProperty("title").GetString() ?? "API";
            ApiTitleText.Text = title;

            EndpointsPanel.Children.Clear();
            ResponseTextBox.Clear();

            var generator = new ApiUiBuilder(ResponseTextBox, _httpClient);
            generator.BuildEndpointsUi(root, EndpointsPanel);
        }
        catch (Exception ex)
        {
            ResponseTextBox.Text = $"Error loading API: {ex.Message}";
        }
    }
}
