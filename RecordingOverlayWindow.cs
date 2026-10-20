using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.Diagnostics;

namespace CapAvalonia;

internal sealed class RecordingOverlayWindow : Window
{
    private readonly TextBlock _elapsed;
    private readonly TextBlock _status;
    private readonly Button _pauseButton;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _elapsedClock = Stopwatch.StartNew();

    public RecordingOverlayWindow(Window owner, Action stop, Func<bool> togglePause, Action<object?, KeyEventArgs> handleShortcut)
    {
        Title = "Cap Recording";
        Width = 280;
        Height = 58;
        MinWidth = 280;
        MinHeight = 58;
        MaxWidth = 280;
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
        _status = new TextBlock { Text = "Recording", FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#AAAAAA")), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        _pauseButton = new Button
        {
            Content = "Pause",
            Padding = new Thickness(9, 6),
            Background = new SolidColorBrush(Color.Parse("#292929")),
            BorderBrush = new SolidColorBrush(Color.Parse("#454545")),
            BorderThickness = new Thickness(1),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            IsEnabled = OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()
        };
        ToolTip.SetTip(_pauseButton, _pauseButton.IsEnabled ? "Pause or resume recording" : "Pause is not supported on this platform.");
        _pauseButton.Click += (_, _) =>
        {
            try { SetPaused(togglePause()); }
            catch (Exception error)
            {
                _status.Text = "Pause failed";
                ToolTip.SetTip(_pauseButton, error.Message);
            }
        };
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
        left.Children.Add(_status);
        left.Children.Add(_elapsed);
        var actions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
        actions.Children.Add(_pauseButton);
        actions.Children.Add(stopButton);
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(12, 7) };
        content.Children.Add(left);
        Grid.SetColumn(actions, 1);
        content.Children.Add(actions);
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
            var elapsed = _elapsedClock.Elapsed;
            _elapsed.Text = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
        };
        Opened += (_, _) =>
        {
            MacWindowCaptureProtection.ExcludeFromCapture(this);
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    public void SetPaused(bool paused)
    {
        if (paused) _elapsedClock.Stop(); else _elapsedClock.Start();
        _status.Text = paused ? "Paused" : "Recording";
        _status.Foreground = new SolidColorBrush(Color.Parse(paused ? "#F5B942" : "#AAAAAA"));
        _pauseButton.Content = paused ? "Resume" : "Pause";
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Button && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
