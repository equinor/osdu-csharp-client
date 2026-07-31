using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Osdu.Client.ExampleApp.Examples;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

/// <summary>
/// Builds the main example run card with parameters, source code, and run button.
/// </summary>
internal sealed class ExampleCardBuilder(AppTheme theme)
{
    private static readonly FontFamily MonoFont = new("Cascadia Code, Consolas, monospace");

    public event Func<IExample, System.Threading.Tasks.Task>? RunRequested;
    public event Action<string>? ValidationFailed;

    public Border Build(IExample example)
    {
        var card = new Border
        {
            Background = theme.CardBrush,
            BorderBrush = theme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(24),
            Margin = new Thickness(0, 0, 0, 16),
            Effect = new DropShadowEffect
            {
                BlurRadius = 12, ShadowDepth = 3,
                Opacity = theme.ShadowOpacity, Color = Colors.Black, Direction = 270
            }
        };

        var stack = new StackPanel();

        // Parameters
        var paramBuilder = new ParameterPanelBuilder(theme);
        var paramResult = paramBuilder.Build(example);

        // Source code
        var sourceBuilder = new SourceCodePanelBuilder(theme);
        var sourceResult = sourceBuilder.Build(example);

        // Action row
        var actionRow = BuildActionRow(example, paramResult, sourceResult);
        stack.Children.Add(actionRow);

        if (paramResult != null)
        {
            stack.Children.Add(paramResult.Header);
            stack.Children.Add(paramResult.Content);
        }
        stack.Children.Add(sourceResult.Header);
        stack.Children.Add(sourceResult.Content);

        card.Child = stack;
        return card;
    }

    private StackPanel BuildActionRow(IExample example, ParameterPanelResult? paramResult, SourceCodePanelResult sourceResult)
    {
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal };

        var runButton = new RunButtonBuilder(theme).Build();
        actionRow.Children.Add(runButton);

        if (paramResult != null)
        {
            var toggle = new ToggleButtonBuilder(theme).Build($"⚙️ Parameters ({example.Parameters.Count})");
            var capturedHeader = paramResult.Header;
            var capturedContent = paramResult.Content;
            toggle.Click += (_, _) =>
            {
                var v = capturedContent.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                capturedHeader.Visibility = v;
                capturedContent.Visibility = v;
            };
            actionRow.Children.Add(toggle);
        }

        var sourceToggle = new ToggleButtonBuilder(theme).Build("💻 View Source Code");
        var capturedSrcHeader = sourceResult.Header;
        var capturedSrcContent = sourceResult.Content;
        sourceToggle.Click += (_, _) =>
        {
            var v = capturedSrcContent.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            capturedSrcHeader.Visibility = v;
            capturedSrcContent.Visibility = v;
        };
        actionRow.Children.Add(sourceToggle);

        // Wire run button
        var capturedExample = example;
        var capturedParams = paramResult;
        runButton.Click += async (_, _) =>
        {
            if (capturedParams != null)
            {
                var error = capturedParams.ApplyValues(capturedExample);
                if (error != null)
                {
                    ValidationFailed?.Invoke(error);
                    return;
                }
            }
            if (RunRequested != null)
                await RunRequested(capturedExample);
        };

        return actionRow;
    }
}
