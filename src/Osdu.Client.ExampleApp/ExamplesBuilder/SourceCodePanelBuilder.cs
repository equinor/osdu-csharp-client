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
    private const double DefaultFontSize = 12;
    private const double MinFontSize = 8;
    private const double MaxFontSize = 24;
    private const double FontSizeStep = 2;

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
            FontSize = DefaultFontSize,
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

        // Right-aligned actions panel
        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(actionsPanel, Dock.Right);

        // Font size label
        var fontSizeLabel = new TextBlock
        {
            Text = $"{DefaultFontSize:0}px",
            Foreground = theme.TextMutedBrush,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };

        var decreaseButton = CreateHeaderButton("A−");
        var increaseButton = CreateHeaderButton("A+");

        decreaseButton.Click += (_, _) =>
        {
            if (sourceTextBox.FontSize - FontSizeStep >= MinFontSize)
            {
                sourceTextBox.FontSize -= FontSizeStep;
                fontSizeLabel.Text = $"{sourceTextBox.FontSize:0}px";
            }
        };

        increaseButton.Click += (_, _) =>
        {
            if (sourceTextBox.FontSize + FontSizeStep <= MaxFontSize)
            {
                sourceTextBox.FontSize += FontSizeStep;
                fontSizeLabel.Text = $"{sourceTextBox.FontSize:0}px";
            }
        };

        var resetFontButton = CreateHeaderButton("↺");
        resetFontButton.ToolTip = "Reset font size";
        resetFontButton.Click += (_, _) =>
        {
            sourceTextBox.FontSize = DefaultFontSize;
            fontSizeLabel.Text = $"{DefaultFontSize:0}px";
        };

        actionsPanel.Children.Add(decreaseButton);
        actionsPanel.Children.Add(fontSizeLabel);
        actionsPanel.Children.Add(increaseButton);
        actionsPanel.Children.Add(resetFontButton);

        var copyButton = CreateHeaderButton("📋 Copy");
        copyButton.Margin = new Thickness(8, 0, 0, 0);
        copyButton.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(sourceTextBox.Text))
                Clipboard.SetText(sourceTextBox.Text);
        };
        actionsPanel.Children.Add(copyButton);

        heading.Children.Add(actionsPanel);

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

    private Button CreateHeaderButton(string text) => new()
    {
        Content = text,
        Background = Brushes.Transparent,
        Foreground = theme.TextSecondaryBrush,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(6, 4, 6, 4),
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        Cursor = System.Windows.Input.Cursors.Hand,
        VerticalAlignment = VerticalAlignment.Center
    };
}
