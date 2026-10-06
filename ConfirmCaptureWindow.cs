using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Threading.Tasks;

namespace CapAvalonia;

internal sealed class ConfirmCaptureWindow : Window
{
    private readonly TaskCompletionSource<bool> _result = new();

    public ConfirmCaptureWindow()
    {
        Title = "Record without a microphone?";
        Width = 390;
        Height = 220;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        Background = new SolidColorBrush(Color.Parse("#191919"));
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        var heading = new TextBlock
        {
            Text = "No microphone is turned on",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        };
        var explanation = new TextBlock
        {
            Text = "This recording will not contain your voice. You can select a microphone before continuing.",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#B0B0B0"))
        };
        var cancel = Button("Go back");
        cancel.Click += (_, _) => Finish(false);
        var continueButton = Button("Record without mic", primary: true);
        continueButton.Click += (_, _) => Finish(true);
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8 };
        buttons.Children.Add(cancel);
        buttons.Children.Add(continueButton);
        var content = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        content.Children.Add(heading);
        content.Children.Add(explanation);
        content.Children.Add(buttons);
        Content = content;
        Closed += (_, _) => _result.TrySetResult(false);
    }

    public Task<bool> ShowAndWaitAsync(Window owner)
    {
        Show(owner);
        Activate();
        return _result.Task;
    }

    private void Finish(bool result)
    {
        _result.TrySetResult(result);
        Close();
    }

    private static Button Button(string text, bool primary = false) => new()
    {
        Content = text,
        Padding = new Thickness(12, 8),
        Background = new SolidColorBrush(Color.Parse(primary ? "#2677EA" : "#242424")),
        BorderBrush = new SolidColorBrush(Color.Parse(primary ? "#3D89FA" : "#383838")),
        BorderThickness = new Thickness(1),
        Foreground = Brushes.White,
        CornerRadius = new CornerRadius(8)
    };
}
