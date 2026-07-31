using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Osdu.Client.ExampleApp.Examples;

namespace Osdu.Client.ExampleApp.ExamplesBuilder;

internal sealed record SourceCodePanelResult(TextBlock Header, Border Content);

internal sealed class SourceCodePanelBuilder(AppTheme theme)
{
    private static readonly FontFamily MonoFont = new("Cascadia Code, Consolas, monospace");

    public SourceCodePanelResult Build(IExample example)
    {
        var heading = new TextBlock
        {
            Text = "💻 Source Code",
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Foreground = theme.TextPrimaryBrush,
            Margin = new Thickness(0, 18, 0, 10),
            Visibility = Visibility.Collapsed
        };

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
            }
        };

        panel.Child = new TextBox
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

        return new SourceCodePanelResult(heading, panel);
    }
}
