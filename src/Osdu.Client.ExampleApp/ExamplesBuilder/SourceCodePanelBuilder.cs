using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Osdu.Client.ExampleApp.Examples;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

internal sealed record SourceCodePanelResult(DockPanel Header, Border Content);

internal sealed class SourceCodePanelBuilder(AppTheme theme)
{
    private static readonly FontFamily MonoFont = new("Cascadia Code, Consolas, monospace");

    public SourceCodePanelResult Build(IExample example)
    {
        var sourceTextBox = new TextBox
        {
            Text = example.SourceCode,
            IsReadOnly = true,
            Background = Brushes.Transparent,
            Foreground = theme.TextPrimaryBrush,
            BorderThickness = new Thickness(0),
            FontFamily = MonoFont,
            FontSize = 12,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 350
        };

        var heading = new DockPanel
        {
            Margin = new Thickness(0, 18, 0, 10),
            Visibility = Visibility.Collapsed
        };

        var copyButton = new Button
        {
            Content = "📋 Copy",
            Background = Brushes.Transparent,
            Foreground = theme.TextSecondaryBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center
        };
        copyButton.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(sourceTextBox.Text))
                Clipboard.SetText(sourceTextBox.Text);
        };
        DockPanel.SetDock(copyButton, Dock.Right);
        heading.Children.Add(copyButton);

        var leftPanel = new StackPanel { Orientation = Orientation.Horizontal };
        leftPanel.Children.Add(new TextBlock
        {
            Text = "💻 Source Code",
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Foreground = theme.TextPrimaryBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        var showAllCheckBox = new CheckBox
        {
            Content = "Show All",
            Foreground = theme.TextSecondaryBrush,
            FontSize = 12,
            Margin = new Thickness(12, 0, 0, 0),
            IsChecked = false,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        showAllCheckBox.Checked += (_, _) => sourceTextBox.Text = example.FullSourceCode;
        showAllCheckBox.Unchecked += (_, _) => sourceTextBox.Text = example.SourceCode;
        leftPanel.Children.Add(showAllCheckBox);

        heading.Children.Add(leftPanel);

        var panel = new Border
        {
            Background = theme.InputBrush,
            BorderBrush = theme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18, 14, 18, 14),
            Visibility = Visibility.Collapsed,
            Effect = new DropShadowEffect
            {
                BlurRadius = 4, ShadowDepth = 1,
                Opacity = theme.ShadowOpacity * 0.5, Color = Colors.Black, Direction = 270
            },
            Child = sourceTextBox
        };

        return new SourceCodePanelResult(heading, panel);
    }
}
