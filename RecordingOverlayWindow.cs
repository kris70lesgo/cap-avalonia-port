using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using System;

namespace CapAvalonia;

internal sealed class RecordingOverlayWindow : Window
{
    private readonly TextBlock _elapsed;
    private readonly DispatcherTimer _timer;
    private readonly DateTime _startedAt = DateTime.Now;

    public RecordingOverlayWindow(Window owner, Action stop, Action<object?, KeyEventArgs> handleShortcut)
    {
        Title = "Cap Recording";
        Width = 240;
        Height = 58;
        MinWidth = 240;
        MinHeight = 58;
        MaxWidth = 240;
        MaxHeight = 58;
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        Topmost = true;
        ShowInTaskbar = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        var dot = new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(Color.Parse("#F04444")), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        _elapsed = new TextBlock { Text = "00:00", FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        var status = new TextBlock { Text = "Recording", FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        var stopButton = new Button
        {
            Content = "Stop",
            Padding = new Thickness(12, 6),
            Background = new SolidColorBrush(Color.Parse("#3A1E1E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#633232")),
            BorderThickness = new Thickness(1),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        stopButton.Click += (_, _) => stop();
        KeyDown += (sender, args) => handleShortcut(sender, args);
        var left = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 7, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        left.Children.Add(dot);
        left.Children.Add(status);
        left.Children.Add(_elapsed);
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(12, 7) };
        content.Children.Add(left);
        Grid.SetColumn(stopButton, 1);
        content.Children.Add(stopButton);
        Content = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#E51B1B1B")),
            BorderBrush = new SolidColorBrush(Color.Parse("#454545")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(13),
            Child = content
        };

        var screen = owner.Screens.ScreenFromWindow(owner) ?? owner.Screens.Primary;
        if (screen is not null)
        {
            var scale = screen.Scaling;
            Position = new PixelPoint(
                screen.Bounds.X + screen.Bounds.Width - (int)(Width * scale) - (int)(20 * scale),
                screen.Bounds.Y + screen.Bounds.Height - (int)(Height * scale) - (int)(24 * scale));
        }
        PointerPressed += OnPointerPressed;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            var elapsed = DateTime.Now - _startedAt;
            _elapsed.Text = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
        };
        Opened += (_, _) =>
        {
            MacWindowCaptureProtection.ExcludeFromCapture(this);
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Button && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
