using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using System;

namespace CapAvalonia;

internal sealed class TeleprompterWindow : Window
{
    private readonly TextBox _script;
    private readonly TextBlock _readingText;
    private readonly ScrollViewer _readingScroll;
    private readonly Slider _fontSize;
    private readonly Button _startStop;
    private readonly DispatcherTimer _scrollTimer;
    private bool _reading;
    private bool _scrolling;
    private double _scrollSpeed = 18;

    public TeleprompterWindow()
    {
        Title = "Cap Teleprompter";
        Width = 580;
        Height = 660;
        MinWidth = 400;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#111111"));
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        var initialText = AppPreferencesStore.Get("teleprompter.script") ?? string.Empty;
        var heading = new TextBlock
        {
            Text = "Teleprompter",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        var topmost = Action("Always on top");
        topmost.Click += (_, _) =>
        {
            Topmost = !Topmost;
            topmost.Content = Topmost ? "Pinned" : "Always on top";
        };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(18, 14) };
        header.Children.Add(heading);
        Grid.SetColumn(topmost, 1);
        header.Children.Add(topmost);

        _script = new TextBox
        {
            Text = initialText,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top,
            PlaceholderText = "Paste or write your script here…",
            Padding = new Thickness(15),
            FontSize = 17,
            Background = new SolidColorBrush(Color.Parse("#1B1B1B")),
            BorderBrush = new SolidColorBrush(Color.Parse("#303030")),
            Foreground = new SolidColorBrush(Color.Parse("#EEEEEE")),
            CornerRadius = new CornerRadius(10)
        };
        _script.TextChanged += (_, _) =>
        {
            try { AppPreferencesStore.Set("teleprompter.script", _script.Text ?? string.Empty); }
            catch { }
            _readingText!.Text = _script.Text ?? string.Empty;
        };

        _readingText = new TextBlock
        {
            Text = initialText,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 42,
            LineHeight = 58,
            Foreground = Brushes.White,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Margin = new Thickness(26, 90)
        };
        _readingScroll = new ScrollViewer
        {
            Content = _readingText,
            IsVisible = false,
            Background = new SolidColorBrush(Color.Parse("#080808")),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden
        };

        var modeButton = Action("Start reading");
        _startStop = Action("Start scroll");
        _startStop.IsVisible = false;
        _startStop.Click += (_, _) => ToggleScroll();
        var topButton = Action("Top");
        topButton.IsVisible = false;
        topButton.Click += (_, _) =>
        {
            _readingScroll.Offset = new Vector(0, 0);
            if (_scrolling) ToggleScroll();
        };
        _fontSize = new Slider { Minimum = 20, Maximum = 72, Value = 42, Width = 140, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, IsVisible = false };
        _fontSize.ValueChanged += (_, _) => _readingText.FontSize = _fontSize.Value;
        var speed = new Slider { Minimum = 4, Maximum = 70, Value = 18, Width = 120, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, IsVisible = false };
        speed.ValueChanged += (_, _) => _scrollSpeed = speed.Value;
        var speedLabel = new TextBlock { Text = "Speed", FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#999999")), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, IsVisible = false };

        var controls = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, Margin = new Thickness(18, 12), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        controls.Children.Add(speedLabel);
        controls.Children.Add(speed);
        controls.Children.Add(topButton);
        controls.Children.Add(_fontSize);
        controls.Children.Add(_startStop);
        controls.Children.Add(modeButton);

        modeButton.Click += (_, _) =>
        {
            _reading = !_reading;
            var inReadingMode = _reading;
            _startStop.IsVisible = inReadingMode;
            topButton.IsVisible = inReadingMode;
            _fontSize.IsVisible = inReadingMode;
            speed.IsVisible = inReadingMode;
            speedLabel.IsVisible = inReadingMode;
            _script.IsVisible = !inReadingMode;
            _readingScroll.IsVisible = inReadingMode;
            modeButton.Content = inReadingMode ? "Edit script" : "Start reading";
        };

        var body = new Grid();
        body.Children.Add(_script);
        body.Children.Add(_readingScroll);
        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _scrollTimer.Tick += (_, _) =>
        {
            var max = Math.Max(0, _readingScroll.Extent.Height - _readingScroll.Viewport.Height);
            var offset = Math.Min(max, _readingScroll.Offset.Y + (_scrollSpeed * 0.03));
            _readingScroll.Offset = new Vector(_readingScroll.Offset.X, offset);
            if (offset >= max)
                ToggleScroll();
        };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        root.Children.Add(header);
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        Grid.SetRow(controls, 2);
        root.Children.Add(controls);
        Content = root;
        Closed += (_, _) => _scrollTimer.Stop();
    }

    private void ToggleScroll()
    {
        _scrolling = !_scrolling;
        if (_scrolling)
            _scrollTimer.Start();
        else
            _scrollTimer.Stop();
        _startStop.Content = _scrolling ? "Pause scroll" : "Start scroll";
    }

    private static Button Action(string label) => new()
    {
        Content = label,
        Padding = new Thickness(12, 7),
        Background = new SolidColorBrush(Color.Parse("#242424")),
        BorderBrush = new SolidColorBrush(Color.Parse("#383838")),
        BorderThickness = new Thickness(1),
        Foreground = Brushes.White,
        CornerRadius = new CornerRadius(8),
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
    };
}
