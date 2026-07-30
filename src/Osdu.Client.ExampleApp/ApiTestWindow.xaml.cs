using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Osdu.Client.ExampleApp;

/// <summary>
/// Interaction logic for ApiTestWindow.xaml
/// </summary>
public partial class ApiTestWindow : Window
{
    private readonly string _definitionsPath;
    private readonly HttpClient _httpClient;
    private Button? _selectedButton;
    private AppTheme _currentTheme = AppTheme.Light;
    private string? _lastLoadedFilePath;

    public ApiTestWindow(IHttpClientFactory httpClientFactory)
    {
        InitializeComponent();
        _definitionsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Definitions", "Api");
        _httpClient = httpClientFactory.CreateClient("OsduApi");
        ApplyTheme();
        LoadApiFiles();
    }

    private void LoadApiFiles()
    {
        if (!Directory.Exists(_definitionsPath))
        {
            ResponseTextBox.Text = $"Definitions folder not found: {_definitionsPath}";
            return;
        }

        RebuildSidebarButtons();
    }

    private void RebuildSidebarButtons()
    {
        ApiButtonsPanel.Children.Clear();
        _selectedButton = null;

        var jsonFiles = Directory.GetFiles(_definitionsPath, "*.json");

        foreach (var file in jsonFiles)
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(file);
            var button = new Button
            {
                Content = fileName[..1].ToUpperInvariant() + fileName[1..],
                Tag = file,
                Background = Brushes.Transparent,
                Foreground = _currentTheme.TextSecondaryBrush,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 10, 14, 10),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            button.Click += ApiButton_Click;
            ApiButtonsPanel.Children.Add(button);

            // Re-select previously active API
            if (file == _lastLoadedFilePath)
            {
                _selectedButton = button;
                button.Foreground = _currentTheme.AccentBrush;
            }
        }
    }

    private void ApiButton_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var filePath = (string)button.Tag;

        // Update sidebar selection highlight
        if (_selectedButton != null)
        {
            _selectedButton.Foreground = _currentTheme.TextSecondaryBrush;
        }

        _selectedButton = button;
        _selectedButton.Foreground = _currentTheme.AccentBrush;

        _lastLoadedFilePath = filePath;
        LoadApiFromFile(filePath);
    }

    private void LoadApiFromFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var title = root.GetProperty("info").GetProperty("title").GetString() ?? "API";
            ApiTitleText.Text = title;

            var description = root.GetProperty("info").TryGetProperty("description", out var desc)
                ? desc.GetString() ?? ""
                : "";
            ApiDescriptionText.Text = description;

            EndpointsPanel.Children.Clear();
            ResponseTextBox.Clear();
            ResponseStatusText.Text = "";

            var generator = new ApiUiBuilder(ResponseTextBox, ResponseStatusText, _httpClient, _currentTheme);
            generator.BuildEndpointsUi(root, EndpointsPanel);
        }
        catch (Exception ex)
        {
            ResponseTextBox.Text = $"Error loading API: {ex.Message}";
        }
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        _currentTheme = _currentTheme.IsDark ? AppTheme.Light : AppTheme.Dark;
        ApplyTheme();
        RebuildSidebarButtons();

        // Reload current API if one was loaded
        if (_lastLoadedFilePath != null)
        {
            LoadApiFromFile(_lastLoadedFilePath);
        }
    }

    private void ApplyTheme()
    {
        // Window background
        Background = _currentTheme.SurfaceBrush;

        // Sidebar
        SidebarBorder.Background = _currentTheme.SidebarBrush;
        SidebarBorder.BorderBrush = _currentTheme.BorderBrush;
        SidebarTitle.Foreground = _currentTheme.TextPrimaryBrush;
        SidebarSubtitle.Foreground = _currentTheme.TextSecondaryBrush;
        SidebarHeaderBorder.BorderBrush = _currentTheme.BorderBrush;

        // Theme toggle button
        ThemeToggleButton.Content = _currentTheme.IsDark ? "☀️ Light" : "🌙 Dark";
        ThemeToggleButton.Foreground = _currentTheme.TextSecondaryBrush;
        ThemeToggleButton.Background = _currentTheme.TagBrush;

        // Title bar
        TitleBarBorder.BorderBrush = _currentTheme.BorderBrush;
        ApiTitleText.Foreground = _currentTheme.TextPrimaryBrush;
        ApiDescriptionText.Foreground = _currentTheme.TextSecondaryBrush;

        // Response panel
        ResponsePanelBorder.Background = _currentTheme.ResponseBgBrush;
        ResponsePanelBorder.BorderBrush = _currentTheme.BorderBrush;
        ResponseHeaderBorder.BorderBrush = _currentTheme.BorderBrush;
        ResponseLabel.Foreground = _currentTheme.TextPrimaryBrush;
        ResponseStatusText.Foreground = _currentTheme.TextSecondaryBrush;
        ResponseTextBox.Background = Brushes.Transparent;
        ResponseTextBox.Foreground = _currentTheme.IsDark
            ? new SolidColorBrush(Color.FromRgb(200, 200, 212))
            : _currentTheme.TextPrimaryBrush;
        ResponseTextBox.CaretBrush = _currentTheme.TextPrimaryBrush;

        // Splitters
        ColumnSplitter.Background = _currentTheme.BorderBrush;
        RowSplitter.Background = _currentTheme.BorderBrush;

        // Main area
        ContentGrid.Background = _currentTheme.SurfaceBrush;
    }
}
