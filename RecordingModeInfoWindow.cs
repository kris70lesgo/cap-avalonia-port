using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace CapAvalonia;

internal sealed class RecordingModeInfoWindow : Window
{
    public RecordingModeInfoWindow()
    {
        Title = "Recording modes";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        MinHeight = 280;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#111111");
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        var content = new StackPanel { Spacing = 16, Margin = new Thickness(22) };
        content.Children.Add(Label("Choose how to capture", 19, "#F1F1F1", FontWeight.SemiBold));
        content.Children.Add(Label("All recordings and screenshots are saved on this Mac.", 12, "#A1A1A1"));
        content.Children.Add(Mode("Instant", "Fast local recording. The video appears in your Recordings library when you stop."));
        content.Children.Add(Mode("Studio", "Local recording with your selected camera and audio inputs. The trim editor opens when you stop."));
        content.Children.Add(Mode("Screenshot", "Save the selected display or capture area as an image in your Screenshots library."));

        var close = new Button
        {
            Content = "Close",
            Width = 82,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = Brush("#2A2A2A"),
            BorderBrush = Brush("#3A3A3A"),
            BorderThickness = new Thickness(1),
            Foreground = Brush("#F1F1F1"),
            CornerRadius = new CornerRadius(16)
        };
        close.Click += (_, _) => Close();
        content.Children.Add(close);
        Content = new Border
        {
            Padding = new Thickness(2),
            Background = Brush("#111111"),
            Child = content
        };
    }

    private static Control Mode(string title, string description)
    {
        var content = new StackPanel { Spacing = 4 };
        content.Children.Add(Label(title, 13, "#F1F1F1", FontWeight.SemiBold));
        content.Children.Add(Label(description, 12, "#A1A1A1", wrap: true));
        return new Border
        {
            Padding = new Thickness(12, 10),
            Background = Brush("#1C1C1C"),
            BorderBrush = Brush("#2A2A2A"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Child = content
        };
    }

    private static TextBlock Label(string text, double size, string color, FontWeight? weight = null, bool wrap = false) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = Brush(color),
        FontWeight = weight ?? FontWeight.Normal,
        TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap
    };

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
