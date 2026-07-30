using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Osdu.Client.ExampleApp;

/// <summary>
/// Dynamically generates WPF UI elements from an OpenAPI specification.
/// </summary>
public class ApiUiBuilder
{
    private readonly TextBox _responseTextBox;
    private readonly HttpClient _httpClient;

    public ApiUiBuilder(TextBox responseTextBox, HttpClient httpClient)
    {
        _responseTextBox = responseTextBox;
        _httpClient = httpClient;
    }

    public void BuildEndpointsUi(JsonElement root, StackPanel container)
    {
        if (!root.TryGetProperty("paths", out var paths))
            return;

        var schemas = root.TryGetProperty("components", out var components)
            && components.TryGetProperty("schemas", out var s) ? s : default;

        // Extract base path from servers
        var basePath = "";
        if (root.TryGetProperty("servers", out var servers))
        {
            foreach (var server in servers.EnumerateArray())
            {
                if (server.TryGetProperty("url", out var url))
                {
                    basePath = url.GetString()?.TrimEnd('/') ?? "";
                    break;
                }
            }
        }

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var method in path.Value.EnumerateObject())
            {
                var endpointPanel = CreateEndpointPanel(basePath, path.Name, method.Name, method.Value, schemas);
                container.Children.Add(endpointPanel);
            }
        }
    }

    private Border CreateEndpointPanel(string basePath, string path, string httpMethod, JsonElement operation, JsonElement schemas)
    {
        var methodColor = httpMethod.ToUpperInvariant() switch
        {
            "GET" => Color.FromRgb(97, 175, 254),
            "POST" => Color.FromRgb(73, 204, 144),
            "PUT" => Color.FromRgb(252, 161, 48),
            "DELETE" => Color.FromRgb(249, 62, 62),
            "PATCH" => Color.FromRgb(80, 227, 194),
            _ => Color.FromRgb(128, 128, 128)
        };

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(methodColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(12)
        };

        var mainStack = new StackPanel();

        // Header: METHOD + PATH
        var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        headerStack.Children.Add(new Border
        {
            Background = new SolidColorBrush(methodColor),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock
            {
                Text = httpMethod.ToUpperInvariant(),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12
            }
        });
        headerStack.Children.Add(new TextBlock
        {
            Text = path,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        mainStack.Children.Add(headerStack);

        // Summary
        if (operation.TryGetProperty("summary", out var summary))
        {
            mainStack.Children.Add(new TextBlock
            {
                Text = summary.GetString(),
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });
        }

        // Expander for details
        var expander = new Expander { Header = "Parameters & Body", IsExpanded = false };
        var detailsStack = new StackPanel();

        // Query parameters (skip header params)
        var inputControls = new Dictionary<string, (FrameworkElement Control, string Location)>();

        if (operation.TryGetProperty("parameters", out var parameters))
        {
            foreach (var param in parameters.EnumerateArray())
            {
                var inValue = param.GetProperty("in").GetString();
                if (inValue == "header") continue; // skip headers

                var paramName = param.GetProperty("name").GetString()!;
                var required = param.TryGetProperty("required", out var req) && req.GetBoolean();
                var description = param.TryGetProperty("description", out var desc) ? desc.GetString() : "";
                var paramType = "string";
                if (param.TryGetProperty("schema", out var paramSchema) && paramSchema.TryGetProperty("type", out var pt))
                    paramType = pt.GetString()!;

                var control = CreateParameterControl(paramName, paramType, required, description, inValue!);
                detailsStack.Children.Add(control.Panel);
                inputControls[paramName] = (control.Input, inValue!);
            }
        }

        // Request body
        TextBox? bodyTextBox = null;
        if (operation.TryGetProperty("requestBody", out var requestBody))
        {
            var bodySchema = GetRequestBodySchema(requestBody, schemas);
            var bodyLabel = new TextBlock
            {
                Text = "Request Body (JSON):",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 4)
            };
            detailsStack.Children.Add(bodyLabel);

            bodyTextBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                MinHeight = 120,
                MaxHeight = 300,
                TextWrapping = TextWrapping.NoWrap,
                Text = bodySchema
            };
            detailsStack.Children.Add(bodyTextBox);
        }

        expander.Content = detailsStack;
        mainStack.Children.Add(expander);

        // Execute button
        var executeButton = new Button
        {
            Content = "Execute",
            Padding = new Thickness(16, 6, 16, 6),
            Margin = new Thickness(0, 8, 0, 0),
            Background = new SolidColorBrush(methodColor),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var capturedBasePath = basePath;
        var capturedPath = path;
        var capturedMethod = httpMethod;
        var capturedInputs = inputControls;
        var capturedBody = bodyTextBox;

        executeButton.Click += async (_, _) =>
        {
            await ExecuteRequestAsync(executeButton, capturedBasePath, capturedPath, capturedMethod, capturedInputs, capturedBody);
        };

        mainStack.Children.Add(executeButton);

        border.Child = mainStack;
        return border;
    }

    private async Task ExecuteRequestAsync(
        Button executeButton,
        string basePath,
        string path,
        string httpMethod,
        Dictionary<string, (FrameworkElement Control, string Location)> inputControls,
        TextBox? bodyTextBox)
    {
        executeButton.IsEnabled = false;
        _responseTextBox.Text = "Sending request...";

        try
        {
            var queryParams = new List<string>();
            var pathResolved = path;

            foreach (var kvp in inputControls)
            {
                var value = GetControlValue(kvp.Value.Control);
                if (string.IsNullOrEmpty(value)) continue;

                if (kvp.Value.Location == "path")
                    pathResolved = pathResolved.Replace($"{{{kvp.Key}}}", Uri.EscapeDataString(value));
                else // query
                    queryParams.Add($"{kvp.Key}={Uri.EscapeDataString(value)}");
            }

            var relativePath = $"{basePath}{pathResolved}";
            if (queryParams.Count > 0)
                relativePath = $"{relativePath}?{string.Join("&", queryParams)}";

            using var request = new HttpRequestMessage(
                new HttpMethod(httpMethod.ToUpperInvariant()),
                relativePath);

            if (bodyTextBox != null && !string.IsNullOrWhiteSpace(bodyTextBox.Text))
            {
                request.Content = new StringContent(bodyTextBox.Text, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request);

            var responseBody = await response.Content.ReadAsStringAsync();

            var output = new StringBuilder();
            output.AppendLine($"--- Response ---");
            output.AppendLine($"Status: {(int)response.StatusCode} {response.ReasonPhrase}");
            output.AppendLine();

            // Try to pretty-print JSON
            try
            {
                var jsonDoc = JsonDocument.Parse(responseBody);
                responseBody = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                // Not JSON, use raw response
            }

            output.AppendLine(responseBody);
            _responseTextBox.Text = output.ToString();
        }
        catch (Exception ex)
        {
            _responseTextBox.Text = $"--- Error ---\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
        }
        finally
        {
            executeButton.IsEnabled = true;
        }
    }

    private (StackPanel Panel, FrameworkElement Input) CreateParameterControl(
        string name, string type, bool required, string? description, string location)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };

        var label = new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 2)
        };
        label.Inlines.Add(new System.Windows.Documents.Run(name) { FontWeight = FontWeights.SemiBold });
        label.Inlines.Add(new System.Windows.Documents.Run($"  ({location}, {type})") { Foreground = Brushes.Gray, FontSize = 11 });
        if (required)
            label.Inlines.Add(new System.Windows.Documents.Run(" *") { Foreground = Brushes.Red });

        panel.Children.Add(label);

        if (!string.IsNullOrEmpty(description))
        {
            panel.Children.Add(new TextBlock
            {
                Text = description,
                Foreground = Brushes.Gray,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 2)
            });
        }

        FrameworkElement input;
        if (type == "boolean")
        {
            input = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
        }
        else
        {
            input = new TextBox
            {
                MinWidth = 300,
                Padding = new Thickness(4, 2, 4, 2)
            };
        }

        panel.Children.Add(input);
        return (panel, input);
    }

    private string GetControlValue(FrameworkElement control)
    {
        return control switch
        {
            TextBox tb => tb.Text,
            CheckBox cb => cb.IsChecked == true ? "true" : "false",
            _ => ""
        };
    }

    private string GetRequestBodySchema(JsonElement requestBody, JsonElement schemas)
    {
        try
        {
            if (requestBody.TryGetProperty("content", out var content)
                && content.TryGetProperty("application/json", out var jsonContent)
                && jsonContent.TryGetProperty("schema", out var schema))
            {
                var resolved = ResolveSchema(schema, schemas);
                return JsonSerializer.Serialize(resolved, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch { }

        return "{\n  \n}";
    }

    private Dictionary<string, object?> ResolveSchema(JsonElement schema, JsonElement schemas, int depth = 0)
    {
        if (depth > 5) return new Dictionary<string, object?> { ["..."] = "max depth" };

        // Handle $ref
        if (schema.TryGetProperty("$ref", out var refElement))
        {
            var refPath = refElement.GetString()!;
            var schemaName = refPath.Split('/').Last();
            if (schemas.ValueKind == JsonValueKind.Object && schemas.TryGetProperty(schemaName, out var refSchema))
            {
                return ResolveSchema(refSchema, schemas, depth + 1);
            }
            return new Dictionary<string, object?> { ["$ref"] = refPath };
        }

        var result = new Dictionary<string, object?>();

        if (schema.TryGetProperty("properties", out var properties))
        {
            foreach (var prop in properties.EnumerateObject())
            {
                result[prop.Name] = ResolvePropertyExample(prop.Value, schemas, depth + 1);
            }
        }

        return result;
    }

    private object? ResolvePropertyExample(JsonElement prop, JsonElement schemas, int depth)
    {
        if (depth > 5) return "...";

        if (prop.TryGetProperty("$ref", out var refEl))
        {
            var refPath = refEl.GetString()!;
            var schemaName = refPath.Split('/').Last();
            if (schemas.ValueKind == JsonValueKind.Object && schemas.TryGetProperty(schemaName, out var refSchema))
            {
                return ResolveSchema(refSchema, schemas, depth);
            }
            return $"<{schemaName}>";
        }

        // anyOf – take first
        if (prop.TryGetProperty("anyOf", out var anyOf))
        {
            foreach (var item in anyOf.EnumerateArray())
            {
                return ResolvePropertyExample(item, schemas, depth);
            }
        }

        if (prop.TryGetProperty("example", out var example))
        {
            return example.ValueKind switch
            {
                JsonValueKind.String => example.GetString(),
                JsonValueKind.Number => example.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => example.ToString()
            };
        }

        var type = prop.TryGetProperty("type", out var t) ? t.GetString() : "string";

        return type switch
        {
            "string" => "",
            "integer" => 0,
            "number" => 0.0,
            "boolean" => false,
            "array" => new List<object?>(),
            "object" => ResolveSchema(prop, schemas, depth),
            _ => null
        };
    }
}
