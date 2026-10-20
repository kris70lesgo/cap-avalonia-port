using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace CapAvalonia;

internal sealed class VideoEditorWindow : Window
{
    private readonly string _path;
    private readonly Image _preview;
    private readonly Slider _playhead;
    private readonly TextBox _trimStart;
    private readonly TextBox _trimEnd;
    private readonly TextBlock _timeLabel;
    private readonly TextBlock _status;
    private readonly Button _export;
    private readonly ComboBox _exportQuality;
    private Bitmap? _previewBitmap;
    private double _duration;
    private double _start;
    private double _end;
    private int _previewGeneration;

    public VideoEditorWindow(string path)
    {
        _path = path;
        Title = "Cap Studio";
        Width = 1040;
        Height = 720;
        MinWidth = 760;
        MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#111111"));
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        _preview = new Image { Stretch = Stretch.Uniform, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch };
        _playhead = new Slider { Minimum = 0, Maximum = 1, Value = 0, Margin = new Thickness(0, 6, 0, 0) };
        _trimStart = TimeInput("0:00.0");
        _trimEnd = TimeInput("0:00.0");
        _timeLabel = Text("00:00 / 00:00", 12, "#B5B5B5");
        _status = Text("Loading recording…", 12, "#A4A4A4");
        _export = Action("Export clip", primary: true);
        _export.Click += async (_, _) => await ExportAsync();
        _exportQuality = new ComboBox
        {
            Width = 138,
            Height = 34,
            SelectedIndex = 1,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Background = new SolidColorBrush(Color.Parse("#242424")),
            BorderBrush = new SolidColorBrush(Color.Parse("#383838")),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8),
            Items = { new ComboBoxItem { Content = "Smaller file" }, new ComboBoxItem { Content = "Balanced" }, new ComboBoxItem { Content = "Higher quality" } }
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"), ColumnSpacing = 10, Margin = new Thickness(20, 14) };
        var back = Action("Back to recordings");
        back.Click += (_, _) => Close();
        header.Children.Add(back);
        var title = Text(Path.GetFileName(path), 14, "#F0F0F0");
        title.FontWeight = FontWeight.SemiBold;
        title.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        title.Margin = new Thickness(8, 0);
        Grid.SetColumn(title, 1);
        header.Children.Add(title);
        var play = Action("Play preview");
        play.Click += (_, _) =>
        {
            try { MediaLibraryService.OpenVideoAt(_path, _playhead.Value); }
            catch (Exception error) { SetStatus(error.Message); }
        };
        Grid.SetColumn(play, 2);
        header.Children.Add(play);
        Grid.SetColumn(_exportQuality, 3);
        header.Children.Add(_exportQuality);
        Grid.SetColumn(_export, 4);
        header.Children.Add(_export);

        var previewHost = new Border
        {
            Margin = new Thickness(20, 0),
            Background = new SolidColorBrush(Color.Parse("#080808")),
            BorderBrush = new SolidColorBrush(Color.Parse("#292929")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Child = _preview
        };

        var trimStartLabel = Text("IN", 10, "#8F8F8F");
        var trimEndLabel = Text("OUT", 10, "#8F8F8F");
        var trimRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,110,*,Auto,110"), ColumnSpacing = 10, Margin = new Thickness(20, 12, 20, 0) };
        trimRow.Children.Add(trimStartLabel);
        Grid.SetColumn(_trimStart, 1);
        trimRow.Children.Add(_trimStart);
        Grid.SetColumn(trimEndLabel, 3);
        trimRow.Children.Add(trimEndLabel);
        Grid.SetColumn(_trimEnd, 4);
        trimRow.Children.Add(_trimEnd);

        var editDescription = Text("Set an in and out point, preview the clip, then export a trimmed MP4. Your original stays unchanged.", 12, "#A0A0A0");
        editDescription.TextWrapping = TextWrapping.Wrap;
        var controls = new StackPanel { Spacing = 4, Margin = new Thickness(20, 10, 20, 18) };
        var timelineLabels = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,* ,Auto"), Margin = new Thickness(0, 2, 0, 0) };
        timelineLabels.Children.Add(_timeLabel);
        var playheadText = Text("Drag to seek", 11, "#777777");
        playheadText.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
        Grid.SetColumn(playheadText, 2);
        timelineLabels.Children.Add(playheadText);
        controls.Children.Add(timelineLabels);
        controls.Children.Add(_playhead);
        controls.Children.Add(trimRow);
        controls.Children.Add(editDescription);
        controls.Children.Add(_status);

        _playhead.ValueChanged += (_, _) =>
        {
            UpdateTimeLabel();
            _ = RefreshPreviewLaterAsync(++_previewGeneration);
        };
        _trimStart.TextChanged += (_, _) => ApplyTrimText(_trimStart, isStart: true);
        _trimEnd.TextChanged += (_, _) => ApplyTrimText(_trimEnd, isStart: false);

        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        layout.Children.Add(header);
        Grid.SetRow(previewHost, 1);
        layout.Children.Add(previewHost);
        Grid.SetRow(controls, 2);
        layout.Children.Add(controls);

        Content = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#111111")),
            Child = layout
        };

        Closed += (_, _) => _previewBitmap?.Dispose();
        Opened += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _duration = await MediaLibraryService.GetVideoDurationAsync(_path);
            _playhead.Maximum = _duration;
            var savedStart = AppPreferencesStore.Get("trim.start." + _path);
            var savedEnd = AppPreferencesStore.Get("trim.end." + _path);
            _start = double.TryParse(savedStart, CultureInfo.InvariantCulture, out var start) ? Math.Clamp(start, 0, _duration) : 0;
            _end = double.TryParse(savedEnd, CultureInfo.InvariantCulture, out var end) ? Math.Clamp(end, _start, _duration) : _duration;
            _trimStart.Text = FormatTime(_start);
            _trimEnd.Text = FormatTime(_end);
            UpdateTimeLabel();
            SetStatus($"{FormatTime(_duration)} · {MediaLibraryService.FormatSize(new FileInfo(_path).Length)}");
            await RefreshPreviewAsync(0);
        }
        catch (Exception error)
        {
            _export.IsEnabled = false;
            SetStatus(error.Message);
        }
    }

    private void ApplyTrimText(TextBox input, bool isStart)
    {
        if (!TryParseTime(input.Text, out var value) || _duration <= 0)
            return;
        if (isStart)
            _start = Math.Clamp(value, 0, Math.Max(0, _end - 0.1));
        else
            _end = Math.Clamp(value, Math.Min(_duration, _start + 0.1), _duration);
        SaveTrimPoints();
    }

    private void SaveTrimPoints()
    {
        try
        {
            AppPreferencesStore.Set("trim.start." + _path, _start.ToString("0.###", CultureInfo.InvariantCulture));
            AppPreferencesStore.Set("trim.end." + _path, _end.ToString("0.###", CultureInfo.InvariantCulture));
        }
        catch { }
    }

    private async Task RefreshPreviewLaterAsync(int generation)
    {
        await Task.Delay(350);
        if (generation != _previewGeneration || _duration <= 0)
            return;
        await RefreshPreviewAsync(_playhead.Value);
    }

    private async Task RefreshPreviewAsync(double time)
    {
        try
        {
            var thumbnail = await MediaLibraryService.CreateVideoThumbnailAsync(_path, time);
            if (!IsVisible || Math.Abs(time - _playhead.Value) > 0.05)
                return;
            var bytes = await File.ReadAllBytesAsync(thumbnail);
            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            var previous = _previewBitmap;
            _previewBitmap = bitmap;
            _preview.Source = bitmap;
            previous?.Dispose();
        }
        catch (Exception error)
        {
            SetStatus("Preview could not be loaded: " + error.Message);
        }
    }

    private async Task ExportAsync()
    {
        if (_duration <= 0 || _end <= _start)
            return;
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export trimmed recording",
                SuggestedFileName = Path.GetFileNameWithoutExtension(_path) + "_trimmed.mp4",
                DefaultExtension = "mp4",
                FileTypeChoices = new[] { new FilePickerFileType("MP4 video") { Patterns = new[] { "*.mp4" } } }
            });
            if (file is null)
                return;

            _export.IsEnabled = false;
            SetStatus("Exporting clip…");
            var quality = (_exportQuality.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Balanced";
            await MediaLibraryService.ExportTrimmedVideoAsync(_path, file.Path.LocalPath, _start, _end, quality);
            SetStatus("Export saved: " + Path.GetFileName(file.Path.LocalPath));
        }
        catch (Exception error)
        {
            SetStatus("Export failed: " + error.Message);
        }
        finally
        {
            _export.IsEnabled = true;
        }
    }

    private void UpdateTimeLabel() => _timeLabel.Text = $"{FormatTime(_playhead.Value)} / {FormatTime(_duration)}";

    private void SetStatus(string message) => _status.Text = message;

    private static TextBox TimeInput(string initial) => new()
    {
        Text = initial,
        MinHeight = 32,
        Padding = new Thickness(8, 4),
        Background = new SolidColorBrush(Color.Parse("#242424")),
        BorderBrush = new SolidColorBrush(Color.Parse("#383838")),
        Foreground = new SolidColorBrush(Color.Parse("#F0F0F0")),
        CornerRadius = new CornerRadius(7),
        HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
    };

    private static Button Action(string label, bool primary = false) => new()
    {
        Content = label,
        Padding = new Thickness(14, 8),
        Background = new SolidColorBrush(Color.Parse(primary ? "#2677EA" : "#242424")),
        BorderBrush = new SolidColorBrush(Color.Parse(primary ? "#3D89FA" : "#383838")),
        BorderThickness = new Thickness(1),
        Foreground = Brushes.White,
        CornerRadius = new CornerRadius(8),
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
    };

    private static TextBlock Text(string value, double size, string color) => new()
    {
        Text = value,
        FontSize = size,
        Foreground = new SolidColorBrush(Color.Parse(color)),
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
    };

    private static string FormatTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
            seconds = 0;
        var whole = (int)Math.Floor(seconds);
        return $"{whole / 60:00}:{whole % 60:00}.{(int)((seconds - whole) * 10)}";
    }

    private static bool TryParseTime(string? input, out double seconds)
    {
        if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
            return seconds >= 0;
        var parts = input?.Split(':');
        if (parts is { Length: 2 } &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var remainder))
        {
            seconds = (minutes * 60) + remainder;
            return seconds >= 0;
        }
        seconds = 0;
        return false;
    }
}
