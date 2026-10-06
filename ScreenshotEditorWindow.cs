using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CapAvalonia;

internal sealed class ScreenshotEditorWindow : Window
{
    private readonly string _path;
    private readonly Bitmap _bitmap;
    private readonly Image _image;
    private readonly InkLayer _ink;
    private readonly TextBox _noteInput;
    private readonly TextBlock _noteOverlay;
    private readonly Grid _imageCanvas;
    private readonly TextBlock _status;

    public ScreenshotEditorWindow(string path)
    {
        _path = path;
        Title = "Screenshot Editor";
        Width = 1100;
        Height = 760;
        MinWidth = 820;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#111111"));
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        _bitmap = new Bitmap(path);
        _image = new Image { Source = _bitmap, Stretch = Stretch.Fill };
        _ink = new InkLayer();
        _noteOverlay = new TextBlock
        {
            Text = string.Empty,
            FontSize = 36,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.Parse("#CC111111")),
            Padding = new Thickness(18, 10),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(32),
            IsVisible = false,
            MaxWidth = Math.Max(120, _bitmap.PixelSize.Width - 64),
            TextWrapping = TextWrapping.Wrap
        };
        _imageCanvas = new Grid
        {
            Width = _bitmap.PixelSize.Width,
            Height = _bitmap.PixelSize.Height,
            Children = { _image, _ink, _noteOverlay }
        };
        _ink.TextPositionRequested += SetNotePosition;

        var title = new TextBlock
        {
            Text = Path.GetFileName(path),
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#F0F0F0")),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,170,108,104,Auto,Auto,Auto"),
            ColumnSpacing = 8,
            Margin = new Thickness(18, 12)
        };
        var close = Button("Done");
        close.Click += (_, _) => Close();
        header.Children.Add(close);
        Grid.SetColumn(title, 1);
        header.Children.Add(title);

        _noteInput = new TextBox
        {
            PlaceholderText = "Add text",
            MinHeight = 32,
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse("#242424")),
            BorderBrush = new SolidColorBrush(Color.Parse("#383838")),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(7)
        };
        _noteInput.TextChanged += (_, _) =>
        {
            _noteOverlay.Text = _noteInput.Text;
            _noteOverlay.IsVisible = !string.IsNullOrWhiteSpace(_noteInput.Text);
        };
        Grid.SetColumn(_noteInput, 2);
        header.Children.Add(_noteInput);

        var toolPicker = Picker(new[] { "Pen", "Arrow", "Rectangle", "Highlight", "Text" }, width: 108);
        toolPicker.SelectionChanged += (_, _) =>
        {
            _ink.Tool = GetSelectedText(toolPicker) switch
            {
                "Arrow" => AnnotationTool.Arrow,
                "Rectangle" => AnnotationTool.Rectangle,
                "Highlight" => AnnotationTool.Highlight,
                "Text" => AnnotationTool.Text,
                _ => AnnotationTool.Pen
            };
            if (_status is null)
                return;
            _status.Text = _ink.Tool == AnnotationTool.Text
                ? "Enter text above, then click the image to place it."
                : "Draw on the image to add marks. Undo the last mark or save a new PNG.";
        };
        Grid.SetColumn(toolPicker, 3);
        header.Children.Add(toolPicker);

        var colorPicker = Picker(new[] { "Red", "Yellow", "Blue", "White" }, width: 104);
        colorPicker.SelectionChanged += (_, _) => _ink.Color = GetSelectedText(colorPicker) switch
        {
            "Yellow" => Color.Parse("#FFFFD54F"),
            "Blue" => Color.Parse("#FF4C9AFF"),
            "White" => Colors.White,
            _ => Color.Parse("#FFF04444")
        };
        Grid.SetColumn(colorPicker, 4);
        header.Children.Add(colorPicker);

        var undo = Button("Undo");
        undo.Click += (_, _) => _ink.Undo();
        Grid.SetColumn(undo, 5);
        header.Children.Add(undo);
        var clear = Button("Clear");
        clear.Click += (_, _) =>
        {
            _ink.Clear();
            _noteInput.Clear();
            _noteOverlay.Margin = new Thickness(32);
        };
        ToolTip.SetTip(clear, "Clear all marks and text");
        Grid.SetColumn(clear, 6);
        header.Children.Add(clear);
        var save = Button("Save as PNG", primary: true);
        save.Click += async (_, _) => await SaveAsync();
        Grid.SetColumn(save, 7);
        header.Children.Add(save);

        var viewport = new Border
        {
            Margin = new Thickness(18, 0),
            Background = new SolidColorBrush(Color.Parse("#080808")),
            BorderBrush = new SolidColorBrush(Color.Parse("#292929")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Child = new Viewbox { Stretch = Stretch.Uniform, Child = _imageCanvas }
        };
        _status = new TextBlock
        {
            Text = "Choose a tool, draw on the image, or enter text and click to place it.",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#A1A1A1")),
            Margin = new Thickness(18, 10, 18, 14)
        };

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        root.Children.Add(header);
        Grid.SetRow(viewport, 1);
        root.Children.Add(viewport);
        Grid.SetRow(_status, 2);
        root.Children.Add(_status);
        Content = root;
        Closed += (_, _) => _bitmap.Dispose();
    }

    private void SetNotePosition(Point point)
    {
        var left = Math.Clamp(point.X, 0, Math.Max(0, _imageCanvas.Width - 80));
        var top = Math.Clamp(point.Y, 0, Math.Max(0, _imageCanvas.Height - 48));
        _noteOverlay.Margin = new Thickness(left, top, 0, 0);
        if (!string.IsNullOrWhiteSpace(_noteInput.Text))
            _noteOverlay.IsVisible = true;
        _status.Text = string.IsNullOrWhiteSpace(_noteInput.Text)
            ? "Enter text above, then click the image to place it."
            : "Text placed. Save as PNG to export your annotations.";
    }

    private async Task SaveAsync()
    {
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save annotated screenshot",
                SuggestedFileName = Path.GetFileNameWithoutExtension(_path) + "_annotated.png",
                DefaultExtension = "png",
                FileTypeChoices = new[] { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } }
            });
            if (file is null)
                return;

            var size = new PixelSize((int)_imageCanvas.Width, (int)_imageCanvas.Height);
            using var output = new RenderTargetBitmap(size, new Vector(96, 96));
            _imageCanvas.Measure(new Size(size.Width, size.Height));
            _imageCanvas.Arrange(new Rect(0, 0, size.Width, size.Height));
            output.Render(_imageCanvas);
            output.Save(file.Path.LocalPath, PngBitmapEncoderOptions.Default);
            _status.Text = "Saved annotated image: " + Path.GetFileName(file.Path.LocalPath);
        }
        catch (Exception error)
        {
            _status.Text = "Could not save image: " + error.Message;
        }
    }

    private static ComboBox Picker(IEnumerable<string> values, double width)
    {
        var picker = new ComboBox
        {
            Width = width,
            MinHeight = 32,
            SelectedIndex = 0,
            Background = new SolidColorBrush(Color.Parse("#242424")),
            BorderBrush = new SolidColorBrush(Color.Parse("#383838")),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(7),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        foreach (var value in values)
            picker.Items.Add(new ComboBoxItem { Content = value });
        return picker;
    }

    private static string? GetSelectedText(ComboBox picker) => picker.SelectedItem switch
    {
        ComboBoxItem item => item.Content?.ToString(),
        _ => picker.SelectedItem?.ToString()
    };

    private static Button Button(string label, bool primary = false) => new()
    {
        Content = label,
        Padding = new Thickness(10, 7),
        Background = new SolidColorBrush(Color.Parse(primary ? "#2677EA" : "#242424")),
        BorderBrush = new SolidColorBrush(Color.Parse(primary ? "#3D89FA" : "#383838")),
        BorderThickness = new Thickness(1),
        Foreground = Brushes.White,
        CornerRadius = new CornerRadius(8),
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
    };

    private enum AnnotationTool
    {
        Pen,
        Arrow,
        Rectangle,
        Highlight,
        Text
    }

    private sealed record Mark(AnnotationTool Tool, Color Color, double Width, List<Point> Points);

    private sealed class InkLayer : Control
    {
        private readonly List<Mark> _marks = new();
        private Mark? _current;

        public AnnotationTool Tool { get; set; } = AnnotationTool.Pen;
        public Color Color { get; set; } = Color.Parse("#FFF04444");
        public event Action<Point>? TextPositionRequested;

        public InkLayer()
        {
            PointerPressed += OnPressed;
            PointerMoved += OnMoved;
            PointerReleased += OnReleased;
            PointerCaptureLost += (_, _) => _current = null;
            Cursor = new Cursor(StandardCursorType.Cross);
        }

        public void Undo()
        {
            if (_marks.Count == 0)
                return;
            _marks.RemoveAt(_marks.Count - 1);
            InvalidateVisual();
        }

        public void Clear()
        {
            _marks.Clear();
            _current = null;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            foreach (var mark in _marks)
                DrawMark(context, mark);
            if (_current is not null)
                DrawMark(context, _current);
        }

        private void DrawMark(DrawingContext context, Mark mark)
        {
            if (mark.Points.Count == 0)
                return;

            var color = mark.Tool == AnnotationTool.Highlight
                ? Color.FromArgb(92, mark.Color.R, mark.Color.G, mark.Color.B)
                : mark.Color;
            var brush = new SolidColorBrush(color);
            var pen = new Pen(brush, Math.Max(mark.Width, Bounds.Width / 720), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            var first = mark.Points[0];
            var last = mark.Points[^1];

            if (mark.Tool is AnnotationTool.Pen or AnnotationTool.Highlight)
            {
                for (var index = 1; index < mark.Points.Count; index++)
                    context.DrawLine(pen, mark.Points[index - 1], mark.Points[index]);
                if (mark.Points.Count == 1)
                    context.DrawEllipse(brush, null, first, pen.Thickness / 2, pen.Thickness / 2);
            }
            else if (mark.Tool == AnnotationTool.Rectangle)
            {
                var rect = new Rect(first, last).Normalize();
                context.DrawRectangle(null, pen, rect);
            }
            else if (mark.Tool == AnnotationTool.Arrow)
            {
                context.DrawLine(pen, first, last);
                var dx = first.X - last.X;
                var dy = first.Y - last.Y;
                if (Math.Sqrt(dx * dx + dy * dy) > 0.01)
                {
                    var angle = Math.Atan2(dy, dx);
                    var headLength = Math.Max(14, pen.Thickness * 4);
                    var left = last + new Vector(Math.Cos(angle + 0.48) * headLength, Math.Sin(angle + 0.48) * headLength);
                    var right = last + new Vector(Math.Cos(angle - 0.48) * headLength, Math.Sin(angle - 0.48) * headLength);
                    context.DrawLine(pen, last, left);
                    context.DrawLine(pen, last, right);
                }
            }
        }

        private void OnPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            var point = e.GetPosition(this);
            if (Tool == AnnotationTool.Text)
            {
                TextPositionRequested?.Invoke(point);
                e.Handled = true;
                return;
            }

            _current = new Mark(Tool, Color, Math.Max(4, Bounds.Width / 360), new List<Point> { point });
            _marks.Add(_current);
            e.Pointer.Capture(this);
            InvalidateVisual();
        }

        private void OnMoved(object? sender, PointerEventArgs e)
        {
            if (_current is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                return;
            _current.Points.Add(e.GetPosition(this));
            InvalidateVisual();
        }

        private void OnReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_current is null)
                return;
            _current.Points.Add(e.GetPosition(this));
            _current = null;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
    }
}
