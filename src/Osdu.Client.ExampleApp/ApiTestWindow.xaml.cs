using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Osdu.Client.ExampleApp.Examples;

namespace Osdu.Client.ExampleApp;

/// <summary>
/// Interaction logic for ApiTestWindow.xaml
/// </summary>
public partial class ApiTestWindow : Window
{
    private readonly string _definitionsPath;
    private readonly HttpClient _httpClient;
    private readonly IEnumerable<IExample> _examples;
    private Button? _selectedButton;
    private AppTheme _currentTheme = AppTheme.Light;
    private string? _lastLoadedFilePath;
    private IExample? _lastSelectedExample;
    private SidebarMode _activeMode = SidebarMode.Apis;
    private bool _showOnlyFailed;

    // Track example execution results
    private readonly Dictionary<IExample, ExampleResult> _exampleResults = new();
    private readonly Dictionary<IExample, Button> _exampleButtons = new();

    private enum SidebarMode { Apis, Examples }

    private record ExampleResult(bool Success, string Output, long ElapsedMs);

    public ApiTestWindow(IHttpClientFactory httpClientFactory, IEnumerable<IExample> examples)
    {
        InitializeComponent();
        _definitionsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Definitions", "Api");
        _httpClient = httpClientFactory.CreateClient("OsduApi");
        _examples = examples;
        ApplyTheme();
        ApplyTabStyles();
        RebuildSidebarForCurrentMode();
    }

    // ─── Tab Switching ───────────────────────────────────────────────

    private void ApisTab_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMode == SidebarMode.Apis) return;
        _activeMode = SidebarMode.Apis;
        _selectedButton = null;
        ApplyTabStyles();
        RebuildSidebarForCurrentMode();
        ClearContent("Select an API from the sidebar", "Choose a service on the left to view its endpoints.");
    }

    private void ExamplesTab_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMode == SidebarMode.Examples) return;
        _activeMode = SidebarMode.Examples;
        _selectedButton = null;
        ApplyTabStyles();
        RebuildSidebarForCurrentMode();
        ClearContent("Select an Example", "Choose an example on the left to run it against the OSDU platform.");
    }

    private void ClearContent(string title, string description)
    {
        ApiTitleText.Text = title;
        ApiDescriptionText.Text = description;
        EndpointsPanel.Children.Clear();
        ResponseTextBox.Clear();
        ResponseStatusText.Text = "";
    }

    private void ApplyTabStyles()
    {
        var activeBackground = _currentTheme.AccentBrush;
        var activeForeground = Brushes.White;
        var inactiveBackground = _currentTheme.TagBrush;
        var inactiveForeground = _currentTheme.TextSecondaryBrush;

        ApisTabButton.Background = _activeMode == SidebarMode.Apis ? activeBackground : inactiveBackground;
        ApisTabButton.Foreground = _activeMode == SidebarMode.Apis ? activeForeground : inactiveForeground;
        ExamplesTabButton.Background = _activeMode == SidebarMode.Examples ? activeBackground : inactiveBackground;
        ExamplesTabButton.Foreground = _activeMode == SidebarMode.Examples ? activeForeground : inactiveForeground;

        SidebarTitle.Text = _activeMode == SidebarMode.Apis ? "🔌 APIs" : "📝 Examples";
        SidebarSubtitle.Text = _activeMode == SidebarMode.Apis ? "Select a service" : "Select an example";
    }

    // ─── Sidebar Rebuilding ──────────────────────────────────────────

    private void RebuildSidebarForCurrentMode()
    {
        ApiButtonsPanel.Children.Clear();
        _selectedButton = null;
        _exampleButtons.Clear();

        if (_activeMode == SidebarMode.Apis)
            RebuildApiButtons();
        else
            RebuildExampleButtons();
    }

    private void RebuildApiButtons()
    {
        if (!Directory.Exists(_definitionsPath)) return;

        var jsonFiles = Directory.GetFiles(_definitionsPath, "*.json");

        foreach (var file in jsonFiles)
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(file);
            var button = CreateSidebarButton(fileName[..1].ToUpperInvariant() + fileName[1..], file);
            button.Click += ApiButton_Click;
            ApiButtonsPanel.Children.Add(button);

            if (file == _lastLoadedFilePath)
            {
                _selectedButton = button;
                button.Foreground = _currentTheme.AccentBrush;
            }
        }
    }

    private void RebuildExampleButtons()
    {
        // ─── Action toolbar ──────────────────────────────────────────
        var toolbarRow = new WrapPanel { Margin = new Thickness(8, 6, 8, 2) };

        var runAllButton = CreateActionSidebarButton("▶  Run All");
        runAllButton.Click += async (_, _) => await RunExamplesAsync(_examples);
        toolbarRow.Children.Add(runAllButton);

        var resetButton = CreateActionSidebarButton("⟲  Reset");
        resetButton.Foreground = _currentTheme.TextSecondaryBrush;
        resetButton.Click += (_, _) =>
        {
            _exampleResults.Clear();
            _showOnlyFailed = false;
            RebuildSidebarForCurrentMode();
            ClearContent("Select an Example", "All results cleared. Choose an example on the left to run it.");
        };
        toolbarRow.Children.Add(resetButton);

        var failedCount = _exampleResults.Count(r => !r.Value.Success);
        var filterButton = CreateActionSidebarButton(_showOnlyFailed ? "✕  Show All" : $"⚠  Failed ({failedCount})");
        filterButton.Foreground = failedCount > 0
            ? new SolidColorBrush(Color.FromRgb(249, 80, 80))
            : _currentTheme.TextMutedBrush;
        filterButton.IsEnabled = failedCount > 0 || _showOnlyFailed;
        filterButton.Click += (_, _) =>
        {
            _showOnlyFailed = !_showOnlyFailed;
            RebuildSidebarForCurrentMode();
        };
        toolbarRow.Children.Add(filterButton);

        ApiButtonsPanel.Children.Add(toolbarRow);

        // ─── Separator ───────────────────────────────────────────────
        ApiButtonsPanel.Children.Add(new Border
        {
            Height = 1,
            Background = _currentTheme.BorderBrush,
            Margin = new Thickness(12, 4, 12, 4)
        });

        // ─── Category groups ─────────────────────────────────────────
        var grouped = _examples
            .GroupBy(ex => ex.Category)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var examplesInGroup = group.ToList();
            var visibleExamples = _showOnlyFailed
                ? examplesInGroup.Where(ex => _exampleResults.TryGetValue(ex, out var r) && !r.Success).ToList()
                : examplesInGroup;

            if (visibleExamples.Count == 0) continue;

            var categoryExpander = new Expander
            {
                IsExpanded = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = _currentTheme.TextPrimaryBrush,
                Margin = new Thickness(4, 4, 4, 0)
            };

            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Run category button
            var runCategoryButton = new Button
            {
                Content = "▶",
                Background = Brushes.Transparent,
                Foreground = _currentTheme.AccentBrush,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 0, 6, 0),
                FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = $"Run all '{group.Key}' examples"
            };
            var capturedGroup = examplesInGroup;
            runCategoryButton.Click += async (_, _) => await RunExamplesAsync(capturedGroup);
            headerPanel.Children.Add(runCategoryButton);

            headerPanel.Children.Add(new TextBlock
            {
                Text = group.Key,
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = _currentTheme.TextMutedBrush,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Count badge
            var categoryFailed = examplesInGroup.Count(ex => _exampleResults.TryGetValue(ex, out var r) && !r.Success);
            var categoryPassed = examplesInGroup.Count(ex => _exampleResults.TryGetValue(ex, out var r) && r.Success);
            var badgeText = _exampleResults.Any(r => examplesInGroup.Contains(r.Key))
                ? $"{categoryPassed}✓ {categoryFailed}✗"
                : visibleExamples.Count.ToString();
            var badgeForeground = categoryFailed > 0
                ? new SolidColorBrush(Color.FromRgb(249, 80, 80))
                : categoryPassed > 0
                    ? new SolidColorBrush(Color.FromRgb(73, 204, 144))
                    : _currentTheme.TextMutedBrush;

            headerPanel.Children.Add(new Border
            {
                Background = _currentTheme.TagBrush,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = badgeText,
                    Foreground = badgeForeground,
                    FontSize = 10
                }
            });
            categoryExpander.Header = headerPanel;

            var itemsPanel = new StackPanel { Margin = new Thickness(0, 2, 0, 0) };

            foreach (var example in visibleExamples)
            {
                var button = CreateExampleSidebarButton(example);
                button.Click += ExampleButton_Click;
                itemsPanel.Children.Add(button);
                _exampleButtons[example] = button;

                if (example == _lastSelectedExample)
                {
                    _selectedButton = button;
                }
            }

            categoryExpander.Content = itemsPanel;
            ApiButtonsPanel.Children.Add(categoryExpander);
        }

        ApplyResultColors();
    }

    private Button CreateExampleSidebarButton(IExample example)
    {
        var hasResult = _exampleResults.TryGetValue(example, out var result);

        var statusIndicator = hasResult ? "●" : "○";
        SolidColorBrush statusColor;
        if (hasResult)
            statusColor = result!.Success
                ? new SolidColorBrush(Color.FromRgb(73, 204, 144))
                : new SolidColorBrush(Color.FromRgb(249, 80, 80));
        else
            statusColor = _currentTheme.TextMutedBrush;

        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock
        {
            Text = statusIndicator,
            Foreground = statusColor,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        var namePanel = new StackPanel();
        namePanel.Children.Add(new TextBlock
        {
            Text = example.Text,
            VerticalAlignment = VerticalAlignment.Center
        });

        // Show elapsed time if there's a result
        if (hasResult)
        {
            namePanel.Children.Add(new TextBlock
            {
                Text = $"{result!.ElapsedMs}ms",
                FontSize = 10,
                Foreground = _currentTheme.TextMutedBrush
            });
        }

        content.Children.Add(namePanel);

        var isSelected = example == _lastSelectedExample;

        return new Button
        {
            Content = content,
            Tag = example,
            Background = Brushes.Transparent,
            Foreground = isSelected ? _currentTheme.AccentBrush : _currentTheme.TextSecondaryBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 8, 14, 8),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            Cursor = System.Windows.Input.Cursors.Hand
        };
    }

    private Button CreateActionSidebarButton(string text)
    {
        var button = new Button
        {
            Content = text,
            Background = Brushes.Transparent,
            Foreground = _currentTheme.AccentBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        return button;
    }

    private Button CreateSidebarButton(string content, object tag)
    {
        return new Button
        {
            Content = content,
            Tag = tag,
            Background = Brushes.Transparent,
            Foreground = _currentTheme.TextSecondaryBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 10, 14, 10),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            Cursor = System.Windows.Input.Cursors.Hand
        };
    }

    private void SelectButton(Button button)
    {
        if (_selectedButton != null)
            _selectedButton.Foreground = _currentTheme.TextSecondaryBrush;

        _selectedButton = button;
        _selectedButton.Foreground = _currentTheme.AccentBrush;
    }

    private void ApplyResultColors()
    {
        foreach (var (example, button) in _exampleButtons)
        {
            if (example == _lastSelectedExample)
            {
                button.Foreground = _currentTheme.AccentBrush;
                continue;
            }
            button.Foreground = _currentTheme.TextSecondaryBrush;
        }
    }

    private void UpdateExampleButtonStatus(IExample example)
    {
        if (!_exampleButtons.TryGetValue(example, out var button)) return;

        if (button.Content is StackPanel sp && sp.Children[0] is TextBlock indicator)
        {
            if (_exampleResults.TryGetValue(example, out var result))
            {
                indicator.Text = "●";
                indicator.FontSize = 14;
                indicator.Foreground = result.Success
                    ? new SolidColorBrush(Color.FromRgb(73, 204, 144))
                    : new SolidColorBrush(Color.FromRgb(249, 80, 80));

                // Update elapsed time label
                if (sp.Children[1] is StackPanel namePanel && namePanel.Children.Count > 1
                    && namePanel.Children[1] is TextBlock timeLabel)
                {
                    timeLabel.Text = $"{result.ElapsedMs}ms";
                }
                else if (sp.Children[1] is StackPanel np)
                {
                    np.Children.Add(new TextBlock
                    {
                        Text = $"{result.ElapsedMs}ms",
                        FontSize = 10,
                        Foreground = _currentTheme.TextMutedBrush
                    });
                }
            }
            else
            {
                indicator.Text = "○";
                indicator.FontSize = 14;
                indicator.Foreground = _currentTheme.TextMutedBrush;
            }
        }
    }

    // ─── Run Examples ────────────────────────────────────────────────

    private async Task RunExamplesAsync(IEnumerable<IExample> examples)
    {
        var exampleList = examples.ToList();
        ResponseTextBox.Clear();
        ResponseStatusText.Text = $"⏳ Running {exampleList.Count} example(s)...";
        ResponseStatusText.Foreground = _currentTheme.TextSecondaryBrush;

        ApiTitleText.Text = "Running Examples";
        ApiDescriptionText.Text = $"Executing {exampleList.Count} example(s)...";
        EndpointsPanel.Children.Clear();

        int passed = 0;
        int failed = 0;
        var summaryBuilder = new StringBuilder();

        foreach (var example in exampleList)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await example.RunAsync();
                sw.Stop();

                _exampleResults[example] = new ExampleResult(true, result, sw.ElapsedMilliseconds);
                passed++;
                summaryBuilder.AppendLine($"  ✅ {example.Text} ({sw.ElapsedMilliseconds}ms)");
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorOutput = $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
                _exampleResults[example] = new ExampleResult(false, errorOutput, sw.ElapsedMilliseconds);
                failed++;
                summaryBuilder.AppendLine($"  ❌ {example.Text} ({sw.ElapsedMilliseconds}ms) - {ex.GetType().Name}: {ex.Message}");
            }

            UpdateExampleButtonStatus(example);
            ResponseStatusText.Text = $"⏳ Running... ({passed + failed}/{exampleList.Count})";
        }

        // Rebuild sidebar to update all badges and filter button count
        RebuildSidebarForCurrentMode();

        var totalMs = _exampleResults
            .Where(r => exampleList.Contains(r.Key))
            .Sum(r => r.Value.ElapsedMs);

        var statusEmoji = failed == 0 ? "✅" : "⚠️";
        ResponseStatusText.Text = $"{statusEmoji} {passed} passed, {failed} failed — {totalMs}ms total";
        ResponseStatusText.Foreground = failed == 0
            ? new SolidColorBrush(Color.FromRgb(73, 204, 144))
            : new SolidColorBrush(Color.FromRgb(249, 80, 80));

        ApiTitleText.Text = "Run Results";
        ApiDescriptionText.Text = $"{passed} passed, {failed} failed out of {exampleList.Count} examples.";

        var output = new StringBuilder();
        output.AppendLine($"═══ Execution Summary ═══");
        output.AppendLine($"Total: {exampleList.Count}  |  Passed: {passed}  |  Failed: {failed}  |  Time: {totalMs}ms");
        output.AppendLine();
        output.Append(summaryBuilder);

        ResponseTextBox.Text = output.ToString();
        BuildResultsSummaryUi(exampleList);
    }

    private void BuildResultsSummaryUi(List<IExample> examples)
    {
        EndpointsPanel.Children.Clear();
        var monoFont = new FontFamily("Cascadia Code, Consolas, monospace");

        foreach (var example in examples)
        {
            if (!_exampleResults.TryGetValue(example, out var result)) continue;

            var statusColor = result.Success
                ? Color.FromRgb(73, 204, 144)
                : Color.FromRgb(249, 80, 80);

            var card = new Border
            {
                Background = _currentTheme.CardBrush,
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, statusColor.R, statusColor.G, statusColor.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var row = new DockPanel();

            var statusBadge = new TextBlock
            {
                Text = result.Success ? "✅" : "❌",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(statusBadge, Dock.Left);
            row.Children.Add(statusBadge);

            var timeBadge = new Border
            {
                Background = _currentTheme.TagBrush,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            timeBadge.Child = new TextBlock
            {
                Text = $"{result.ElapsedMs}ms",
                FontSize = 10,
                FontFamily = monoFont,
                Foreground = _currentTheme.TextMutedBrush
            };
            DockPanel.SetDock(timeBadge, Dock.Right);
            row.Children.Add(timeBadge);

            var nameBlock = new TextBlock
            {
                Text = example.Text,
                FontWeight = FontWeights.Medium,
                FontSize = 12.5,
                Foreground = _currentTheme.TextPrimaryBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            row.Children.Add(nameBlock);

            card.Child = row;

            var capturedExample = example;
            card.MouseLeftButtonUp += (_, _) =>
            {
                ShowExampleResult(capturedExample);
            };

            EndpointsPanel.Children.Add(card);
        }
    }

    private void ShowExampleResult(IExample example)
    {
        if (!_exampleResults.TryGetValue(example, out var result)) return;

        _lastSelectedExample = example;

        if (_exampleButtons.TryGetValue(example, out var btn))
        {
            SelectButton(btn);
        }

        ApiTitleText.Text = example.Text;
        ApiDescriptionText.Text = result.Success
            ? $"✅ Completed successfully in {result.ElapsedMs}ms"
            : $"❌ Failed after {result.ElapsedMs}ms";

        ResponseStatusText.Text = result.Success
            ? $"✅ {result.ElapsedMs}ms"
            : $"❌ {result.ElapsedMs}ms";
        ResponseStatusText.Foreground = result.Success
            ? new SolidColorBrush(Color.FromRgb(73, 204, 144))
            : new SolidColorBrush(Color.FromRgb(249, 80, 80));

        var output = result.Output;
        try
        {
            var jsonDoc = JsonDocument.Parse(output);
            output = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { }

        ResponseTextBox.Text = output;
    }

    // ─── API Mode ────────────────────────────────────────────────────

    private void ApiButton_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var filePath = (string)button.Tag;

        SelectButton(button);
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

    // ─── Examples Mode ───────────────────────────────────────────────

    private void ExampleButton_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        var example = (IExample)button.Tag;

        SelectButton(button);
        _lastSelectedExample = example;

        if (_exampleResults.TryGetValue(example, out _))
        {
            ShowExampleResult(example);
            ShowExample(example, showResultInResponse: true);
        }
        else
        {
            ShowExample(example, showResultInResponse: false);
        }
    }

    private void ShowExample(IExample example, bool showResultInResponse = false)
    {
        ApiTitleText.Text = example.Text;
        ApiDescriptionText.Text = example.ShortDescription;
        EndpointsPanel.Children.Clear();

        if (!showResultInResponse)
        {
            ResponseTextBox.Clear();
            ResponseStatusText.Text = "";
        }

        var monoFont = new FontFamily("Cascadia Code, Consolas, monospace");

        var card = new Border
        {
            Background = _currentTheme.CardBrush,
            BorderBrush = _currentTheme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var stack = new StackPanel();

        // ─── Dynamic Parameters UI (hidden until toggled) ────────────
        var parameterControls = new List<(ExampleParameterInfo Info, FrameworkElement Control)>();
        var parameters = example.Parameters;

        TextBlock? paramsHeading = null;
        StackPanel? paramsContent = null;

        if (parameters.Count > 0)
        {
            paramsHeading = new TextBlock
            {
                Text = "⚙️ Parameters",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = _currentTheme.TextPrimaryBrush,
                Margin = new Thickness(0, 14, 0, 6),
                Visibility = Visibility.Visible
            };

            paramsContent = new StackPanel { Visibility = Visibility.Visible };

            foreach (var param in parameters)
            {
                var paramBorder = new Border
                {
                    Background = _currentTheme.InputBrush,
                    BorderBrush = _currentTheme.BorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var paramStack = new StackPanel();

                var labelRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                labelRow.Children.Add(new TextBlock
                {
                    Text = param.DisplayName,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    FontFamily = monoFont,
                    Foreground = _currentTheme.TextPrimaryBrush
                });
                if (param.Required)
                {
                    labelRow.Children.Add(new TextBlock
                    {
                        Text = " *",
                        Foreground = _currentTheme.RequiredBrush,
                        FontWeight = FontWeights.Bold,
                        FontSize = 12
                    });
                }
                labelRow.Children.Add(new Border
                {
                    Background = _currentTheme.TagBrush,
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = GetFriendlyTypeName(param.PropertyType),
                        Foreground = _currentTheme.TextMutedBrush,
                        FontSize = 10,
                        FontFamily = monoFont
                    }
                });
                paramStack.Children.Add(labelRow);

                if (!string.IsNullOrEmpty(param.Description))
                {
                    paramStack.Children.Add(new TextBlock
                    {
                        Text = param.Description,
                        Foreground = _currentTheme.TextMutedBrush,
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 6)
                    });
                }

                var currentValue = param.GetValue(example);
                FrameworkElement inputControl = CreateParameterInput(param, currentValue, monoFont);

                paramStack.Children.Add(inputControl);
                paramBorder.Child = paramStack;
                paramsContent.Children.Add(paramBorder);

                parameterControls.Add((param, inputControl));
            }
        }

        // ─── Source Code Panel (hidden until toggled) ────────────────
        var sourceHeading = new TextBlock
        {
            Text = "💻 Source Code",
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Foreground = _currentTheme.TextPrimaryBrush,
            Margin = new Thickness(0, 14, 0, 6),
            Visibility = Visibility.Collapsed
        };

        var sourceCodePanel = new Border
        {
            Background = _currentTheme.InputBrush,
            BorderBrush = _currentTheme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 12, 16, 12),
            Visibility = Visibility.Collapsed
        };

        var sourceCodeTextBox = new TextBox
        {
            Text = example.SourceCode,
            IsReadOnly = true,
            Background = Brushes.Transparent,
            Foreground = _currentTheme.TextPrimaryBrush,
            BorderThickness = new Thickness(0),
            FontFamily = monoFont,
            FontSize = 12,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 300
        };
        sourceCodePanel.Child = sourceCodeTextBox;

        // ─── Action Row: Run | Parameters | Source Code ──────────────
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal };

        var runButton = CreateRunButton();
        actionRow.Children.Add(runButton);

        if (parameters.Count > 0)
        {
            var capturedParamsHeading = paramsHeading!;
            var capturedParamsContent = paramsContent!;
            var paramsToggle = CreateToggleButton($"⚙️ Parameters ({parameters.Count})", _currentTheme.TagBrush);
            paramsToggle.Click += (_, _) =>
            {
                var newVisibility = capturedParamsContent.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                capturedParamsHeading.Visibility = newVisibility;
                capturedParamsContent.Visibility = newVisibility;
            };
            actionRow.Children.Add(paramsToggle);
        }

        var capturedSourceHeading = sourceHeading;
        var capturedSourcePanel = sourceCodePanel;
        var sourceToggle = CreateToggleButton("💻 View Source Code", _currentTheme.TagBrush);
        sourceToggle.Click += (_, _) =>
        {
            var newVisibility = capturedSourcePanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
            capturedSourceHeading.Visibility = newVisibility;
            capturedSourcePanel.Visibility = newVisibility;
        };
        actionRow.Children.Add(sourceToggle);

        stack.Children.Add(actionRow);

        if (paramsHeading != null && paramsContent != null)
        {
            stack.Children.Add(paramsHeading);
            stack.Children.Add(paramsContent);
        }
        stack.Children.Add(sourceHeading);
        stack.Children.Add(sourceCodePanel);

        // ─── Run Button Click Handler ────────────────────────────────
        var capturedExample = example;
        var capturedParamControls = parameterControls;

        runButton.Click += async (_, _) =>
        {
            foreach (var (info, control) in capturedParamControls)
            {
                try
                {
                    var rawValue = GetParameterValue(control);
                    var converted = ConvertParameterValue(rawValue, info.PropertyType);
                    info.SetValue(capturedExample, converted);
                }
                catch (Exception ex)
                {
                    ResponseStatusText.Text = $"❌ Invalid parameter '{info.DisplayName}'";
                    ResponseStatusText.Foreground = new SolidColorBrush(Color.FromRgb(249, 80, 80));
                    ResponseTextBox.Text = $"Could not convert value for '{info.DisplayName}': {ex.Message}";
                    return;
                }
            }

            foreach (var (info, control) in capturedParamControls)
            {
                if (info.Required)
                {
                    var value = info.GetValue(capturedExample);
                    if (value is null || (value is string s && string.IsNullOrWhiteSpace(s)))
                    {
                        ResponseStatusText.Text = $"❌ '{info.DisplayName}' is required";
                        ResponseStatusText.Foreground = new SolidColorBrush(Color.FromRgb(249, 80, 80));
                        ResponseTextBox.Text = $"Parameter '{info.DisplayName}' is required but has no value.";
                        return;
                    }
                }
            }

            runButton.IsEnabled = false;
            ResponseTextBox.Clear();
            ResponseStatusText.Text = "⏳ Running...";
            ResponseStatusText.Foreground = _currentTheme.TextSecondaryBrush;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await capturedExample.RunAsync();
                sw.Stop();

                _exampleResults[capturedExample] = new ExampleResult(true, result, sw.ElapsedMilliseconds);
                UpdateExampleButtonStatus(capturedExample);

                ResponseStatusText.Text = $"✅ Completed — {sw.ElapsedMilliseconds}ms";
                ResponseStatusText.Foreground = new SolidColorBrush(Color.FromRgb(73, 204, 144));

                try
                {
                    var jsonDoc = JsonDocument.Parse(result);
                    result = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
                }
                catch { }

                ResponseTextBox.Text = result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorOutput = $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
                _exampleResults[capturedExample] = new ExampleResult(false, errorOutput, sw.ElapsedMilliseconds);
                UpdateExampleButtonStatus(capturedExample);

                ResponseStatusText.Text = $"❌ Error — {sw.ElapsedMilliseconds}ms";
                ResponseStatusText.Foreground = new SolidColorBrush(Color.FromRgb(249, 80, 80));
                ResponseTextBox.Text = errorOutput;
            }
            finally
            {
                runButton.IsEnabled = true;
            }
        };

        card.Child = stack;
        EndpointsPanel.Children.Add(card);
    }

    // ─── Parameter UI Helpers ────────────────────────────────────────

    private FrameworkElement CreateParameterInput(ExampleParameterInfo param, object? currentValue, FontFamily monoFont)
    {
        if (param.PropertyType == typeof(bool))
        {
            return new CheckBox
            {
                IsChecked = currentValue is true,
                Foreground = _currentTheme.TextPrimaryBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        var displayValue = currentValue switch
        {
            string[] arr => string.Join(", ", arr),
            _ => currentValue?.ToString() ?? ""
        };

        return new TextBox
        {
            Text = displayValue,
            Padding = new Thickness(8, 6, 8, 6),
            Background = _currentTheme.InputFieldBrush,
            Foreground = _currentTheme.TextPrimaryBrush,
            CaretBrush = _currentTheme.TextPrimaryBrush,
            BorderBrush = _currentTheme.BorderBrush,
            BorderThickness = new Thickness(1),
            FontFamily = monoFont,
            FontSize = 12
        };
    }

    private static string GetParameterValue(FrameworkElement control)
    {
        return control switch
        {
            TextBox tb => tb.Text,
            CheckBox cb => cb.IsChecked == true ? "true" : "false",
            _ => ""
        };
    }

    private static object? ConvertParameterValue(string rawValue, Type targetType)
    {
        if (targetType == typeof(string))
            return rawValue;
        if (targetType == typeof(string[]))
            return rawValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (targetType == typeof(bool))
            return bool.Parse(rawValue);
        if (targetType == typeof(int))
            return int.Parse(rawValue);
        if (targetType == typeof(long))
            return long.Parse(rawValue);
        if (targetType == typeof(double))
            return double.Parse(rawValue);
        if (targetType == typeof(float))
            return float.Parse(rawValue);
        if (targetType == typeof(decimal))
            return decimal.Parse(rawValue);
        if (targetType == typeof(int?))
            return string.IsNullOrWhiteSpace(rawValue) ? null : int.Parse(rawValue);
        if (targetType == typeof(double?))
            return string.IsNullOrWhiteSpace(rawValue) ? null : double.Parse(rawValue);
        if (targetType == typeof(bool?))
            return string.IsNullOrWhiteSpace(rawValue) ? null : bool.Parse(rawValue);

        return Convert.ChangeType(rawValue, targetType);
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type == typeof(string)) return "string";
        if (type == typeof(string[])) return "string[]";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(int?)) return "int?";
        if (type == typeof(double?)) return "double?";
        if (type == typeof(bool?)) return "bool?";
        return type.Name;
    }

    // ─── Theme ───────────────────────────────────────────────────────

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        _currentTheme = _currentTheme.IsDark ? AppTheme.Light : AppTheme.Dark;
        ApplyTheme();
        ApplyTabStyles();
        RebuildSidebarForCurrentMode();

        if (_activeMode == SidebarMode.Apis && _lastLoadedFilePath != null)
            LoadApiFromFile(_lastLoadedFilePath);
        else if (_activeMode == SidebarMode.Examples && _lastSelectedExample != null)
            ShowExample(_lastSelectedExample);
    }

    private void ApplyTheme()
    {
        Background = _currentTheme.SurfaceBrush;

        SidebarBorder.Background = _currentTheme.SidebarBrush;
        SidebarBorder.BorderBrush = _currentTheme.BorderBrush;
        SidebarTitle.Foreground = _currentTheme.TextPrimaryBrush;
        SidebarSubtitle.Foreground = _currentTheme.TextSecondaryBrush;
        SidebarHeaderBorder.BorderBrush = _currentTheme.BorderBrush;

        ThemeToggleButton.Content = _currentTheme.IsDark ? "☀️ Light" : "🌙 Dark";
        ThemeToggleButton.Foreground = _currentTheme.TextSecondaryBrush;
        ThemeToggleButton.Background = _currentTheme.TagBrush;

        TitleBarBorder.BorderBrush = _currentTheme.BorderBrush;
        ApiTitleText.Foreground = _currentTheme.TextPrimaryBrush;
        ApiDescriptionText.Foreground = _currentTheme.TextSecondaryBrush;

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

        ColumnSplitter.Background = _currentTheme.BorderBrush;
        RowSplitter.Background = _currentTheme.BorderBrush;

        ContentGrid.Background = _currentTheme.SurfaceBrush;
    }

    private Button CreateRunButton()
    {
        var accentColor = _currentTheme.Accent;
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 12, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var buttonTemplate = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(accentColor));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(20, 8, 20, 8));
        borderFactory.Name = "ButtonBorder";

        var contentFactory = new FrameworkElementFactory(typeof(StackPanel));
        contentFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var iconFactory = new FrameworkElementFactory(typeof(TextBlock));
        iconFactory.SetValue(TextBlock.TextProperty, "▶");
        iconFactory.SetValue(TextBlock.FontSizeProperty, 13.0);
        iconFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        iconFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 8, 0));
        iconFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        contentFactory.AppendChild(iconFactory);

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetValue(TextBlock.TextProperty, "Run Example");
        textFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        textFactory.SetValue(TextBlock.FontSizeProperty, 13.0);
        textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        contentFactory.AppendChild(textFactory);

        borderFactory.AppendChild(contentFactory);
        buttonTemplate.VisualTree = borderFactory;

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        var hoverColor = Color.FromArgb(255,
            (byte)Math.Min(accentColor.R + 20, 255),
            (byte)Math.Min(accentColor.G + 20, 255),
            (byte)Math.Min(accentColor.B + 20, 255));
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverColor), "ButtonBorder"));
        buttonTemplate.Triggers.Add(hoverTrigger);

        var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabledTrigger.Setters.Add(new Setter(Border.BackgroundProperty, _currentTheme.TagBrush, "ButtonBorder"));
        disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.6));
        buttonTemplate.Triggers.Add(disabledTrigger);

        button.Template = buttonTemplate;
        return button;
    }

    private Button CreateToggleButton(string text, SolidColorBrush background)
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(8, 12, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var buttonTemplate = new ControlTemplate(typeof(Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.BackgroundProperty, background);
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(14, 8, 14, 8));
        borderFactory.Name = "ButtonBorder";

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetValue(TextBlock.TextProperty, text);
        textFactory.SetValue(TextBlock.ForegroundProperty, _currentTheme.TextPrimaryBrush);
        textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        textFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
        textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

        borderFactory.AppendChild(textFactory);
        buttonTemplate.VisualTree = borderFactory;

        var hoverBg = _currentTheme.IsDark
            ? new SolidColorBrush(Color.FromRgb(60, 60, 72))
            : new SolidColorBrush(Color.FromRgb(220, 222, 232));

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "ButtonBorder"));
        buttonTemplate.Triggers.Add(hoverTrigger);

        button.Template = buttonTemplate;
        return button;
    }
}
