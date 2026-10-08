using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CapAvalonia;

internal sealed class ScreenPickerWindow : Window
{
    public ScreenPickerWindow(Window owner, int selectedOrdinal, Action<int, PixelRect> selected)
    {
        Title = "Choose a display";
        Width = 370;
        Height = 330;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#171717"));
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        var items = new StackPanel { Spacing = 8, Margin = new Thickness(18) };
        var displays = owner.Screens.All;
        for (var index = 0; index < displays.Count; index++)
        {
            var screen = displays[index];
            var screenOrdinal = index;
            var title = string.IsNullOrWhiteSpace(screen.DisplayName) ? $"Display {screenOrdinal + 1}" : screen.DisplayName;
            if (screen.IsPrimary)
                title += " · Primary";
            var width = screen.Bounds.Width;
            var height = screen.Bounds.Height;
            var button = Action($"{title}\n{width} × {height}", screenOrdinal == selectedOrdinal);
            button.HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            button.Click += (_, _) =>
            {
                selected(screenOrdinal, screen.Bounds);
                Close();
            };
            items.Children.Add(button);
        }
        Content = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = "Choose a display to record or capture.", Foreground = Brushes.White, Margin = new Thickness(18, 16, 18, 0), FontSize = 13 },
                new ScrollViewer { Content = items, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }
            }
        };
    }

    private static Button Action(string label, bool selected) => new()
    {
        Content = label,
        Padding = new Thickness(12, 10),
        Background = new SolidColorBrush(Color.Parse(selected ? "#173253" : "#242424")),
        BorderBrush = new SolidColorBrush(Color.Parse(selected ? "#3885FF" : "#383838")),
        BorderThickness = new Thickness(1),
        Foreground = Brushes.White,
        CornerRadius = new CornerRadius(8)
    };
}

internal sealed class CaptureDevicePickerWindow : Window
{
    public CaptureDevicePickerWindow(Window owner, string kind, int? selectedIndex, Action<CaptureDevice?> selected)
    {
        Title = $"Choose {kind.ToLowerInvariant()}";
        Width = 400;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#171717"));
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        var list = new StackPanel { Spacing = 8, Margin = new Thickness(18) };
        var header = new TextBlock { Text = $"Choose {kind.ToLowerInvariant()}", FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White, Margin = new Thickness(18, 16, 18, 0) };
        var status = new TextBlock { Text = "Finding connected devices…", FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#999999")), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(18, 4, 18, 0) };
        Content = new StackPanel
        {
            Children =
            {
                header,
                status,
                new ScrollViewer { Content = list, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto }
            }
        };
        Opened += async (_, _) =>
        {
            try
            {
                var availableDevices = await MediaLibraryService.ListMacCaptureDevicesAsync();
                var devices = kind == "System Audio"
                    ? availableDevices.Where(device => device.Kind == "Microphone" && IsLikelyLoopbackDevice(device.Name)).ToArray()
                    : availableDevices.Where(device => device.Kind == kind).ToArray();
                list.Children.Add(DeviceButton("Off", selectedIndex is null, () => { selected(null); Close(); }));
                foreach (var device in devices)
                {
                    var entry = device;
                    list.Children.Add(DeviceButton(entry.Name, entry.Index == selectedIndex, () => { selected(entry); Close(); }));
                }
                status.Text = devices.Length == 0
                    ? kind == "System Audio"
                        ? "No loopback devices were detected. macOS needs an audio loopback device, such as BlackHole, for system sound capture."
                        : $"No {kind.ToLowerInvariant()} devices were detected. Connect a device, then reopen this list."
                    : kind == "System Audio"
                        ? "Select an installed loopback device that receives system output audio."
                        : "These devices are detected by FFmpeg on this Mac.";
            }
            catch (Exception error)
            {
                status.Text = error.Message;
            }
        };
    }

    private static Button DeviceButton(string label, bool selected, Action click)
    {
        var button = new Button
        {
            Content = label,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Padding = new Thickness(12, 10),
            Background = new SolidColorBrush(Color.Parse(selected ? "#173253" : "#242424")),
            BorderBrush = new SolidColorBrush(Color.Parse(selected ? "#3885FF" : "#383838")),
            BorderThickness = new Thickness(1),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8)
        };
        button.Click += (_, _) => click();
        return button;
    }

    private static bool IsLikelyLoopbackDevice(string name)
    {
        var normalized = name.ToLowerInvariant();
        return new[] { "blackhole", "soundflower", "loopback", "virtual", "vb-cable", "ishowu", "audio capture", "aggregate", "multi-output" }
            .Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }
}

internal sealed class AreaSelectionWindow : Window
{
    private readonly TaskCompletionSource<CaptureArea?> _selection = new();
    private readonly int _screenOrdinal;
    private readonly Screen _screen;
    private readonly SelectionSurface _surface = new();

    private AreaSelectionWindow(Window owner, int screenOrdinal, string instructionText)
    {
        _screenOrdinal = screenOrdinal;
        var screens = owner.Screens.All;
        _screen = screenOrdinal >= 0 && screenOrdinal < screens.Count
            ? screens[screenOrdinal]
            : owner.Screens.Primary ?? screens[0];
        var scale = _screen.Scaling;
        Width = _screen.Bounds.Width / scale;
        Height = _screen.Bounds.Height / scale;
        Position = new PixelPoint(_screen.Bounds.X, _screen.Bounds.Y);
        Title = "Select capture area";
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowDecorations = WindowDecorations.None;
        CanResize = false;
        Topmost = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        var instruction = new TextBlock
        {
            Text = instructionText,
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold,
            FontSize = 15,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(12)
        };
        var cancel = Button("Cancel");
        cancel.Click += (_, _) => Finish(null);
        var use = Button("Use area");
        use.Click += (_, _) =>
        {
            var rect = _surface.Selection;
            if (rect.Width < 4 || rect.Height < 4)
                return;
            var factor = _screen.Scaling;
            var x = Math.Max(0, (int)Math.Floor(rect.X * factor));
            var y = Math.Max(0, (int)Math.Floor(rect.Y * factor));
            var width = Math.Max(2, (int)Math.Floor(rect.Width * factor) & ~1);
            var height = Math.Max(2, (int)Math.Floor(rect.Height * factor) & ~1);
            Finish(new CaptureArea(_screenOrdinal, x, y, width, height, _screen.Bounds.X, _screen.Bounds.Y));
        };
        var actionRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Spacing = 10,
            Margin = new Thickness(12)
        };
        actionRow.Children.Add(cancel);
        actionRow.Children.Add(use);
        var overlay = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        overlay.Background = new SolidColorBrush(Color.FromArgb(95, 0, 0, 0));
        overlay.Children.Add(_surface);
        Grid.SetRowSpan(_surface, 3);
        Grid.SetRow(instruction, 0);
        overlay.Children.Add(instruction);
        Grid.SetRow(actionRow, 2);
        overlay.Children.Add(actionRow);
        Content = overlay;
        Closed += (_, _) => _selection.TrySetResult(null);
        use.IsEnabled = false;
        _surface.SelectionChanged += rect => use.IsEnabled = rect.Width >= 4 && rect.Height >= 4;
    }

    public static Task<CaptureArea?> SelectAsync(Window owner, int screenOrdinal, string instruction = "Drag to select an area")
    {
        var picker = new AreaSelectionWindow(owner, screenOrdinal, instruction);
        picker.Show(owner);
        picker.Activate();
        return picker._selection.Task;
    }

    private void Finish(CaptureArea? area)
    {
        _selection.TrySetResult(area);
        Close();
    }

    private static Button Button(string text) => new()
    {
        Content = text,
        Padding = new Thickness(20, 10),
        Background = new SolidColorBrush(Color.Parse(text == "Use area" ? "#2677EA" : "#242424")),
        BorderBrush = new SolidColorBrush(Color.Parse("#FFFFFF55")),
        BorderThickness = new Thickness(1),
        Foreground = Brushes.White,
        CornerRadius = new CornerRadius(9)
    };

    private sealed class SelectionSurface : Control
    {
        private Point _start;
        private Point _end;
        private bool _selecting;
        public Rect Selection
        {
            get
            {
                var left = Math.Clamp(Math.Min(_start.X, _end.X), 0, Bounds.Width);
                var top = Math.Clamp(Math.Min(_start.Y, _end.Y), 0, Bounds.Height);
                var right = Math.Clamp(Math.Max(_start.X, _end.X), 0, Bounds.Width);
                var bottom = Math.Clamp(Math.Max(_start.Y, _end.Y), 0, Bounds.Height);
                return new Rect(left, top, right - left, bottom - top);
            }
        }
        public event Action<Rect>? SelectionChanged;

        public SelectionSurface()
        {
            PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
                _start = _end = e.GetPosition(this);
                _selecting = true;
                e.Pointer.Capture(this);
                InvalidateVisual();
            };
            PointerMoved += (_, e) =>
            {
                if (!_selecting || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
                _end = e.GetPosition(this);
                InvalidateVisual();
                SelectionChanged?.Invoke(Selection);
            };
            PointerReleased += (_, e) =>
            {
                if (!_selecting) return;
                _selecting = false;
                _end = e.GetPosition(this);
                e.Pointer.Capture(null);
                InvalidateVisual();
                SelectionChanged?.Invoke(Selection);
            };
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (Selection.Width < 1 || Selection.Height < 1) return;
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(48, 56, 133, 255)), null, Selection);
            context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#6BA5FF")), 2), Selection);
        }
    }
}
