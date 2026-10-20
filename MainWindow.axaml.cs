using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CapAvalonia;

public partial class MainWindow : Window
{
    private SettingsWindow? _settingsWindow;
    private ActiveRecordingSession? _recordingSession;
    private string _selectedMode = "Studio";
    private bool _captureBusy;
    private int _statusGeneration;
    private TeleprompterWindow? _teleprompterWindow;
    private NotificationsWindow? _notificationsWindow;
    private RecordingOverlayWindow? _recordingOverlayWindow;
    private int _screenOrdinal;
    private string _captureTargetKind = "Display";
    private CaptureArea? _captureArea;
    private CaptureWindowTarget? _windowTarget;
    private int? _cameraDeviceIndex;
    private int? _microphoneDeviceIndex;
    private int? _systemAudioDeviceIndex;
    private bool _cameraEnabled;
    private bool _microphoneEnabled;
    private bool _systemAudioEnabled;
    private bool _cameraOnly;
    private bool _restoreRecorderAfterRecording;
    private bool _countdownInProgress;
    private bool _countdownCancelled;
    private MacGlobalHotkeyService? _globalHotkeys;

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => MacTitlebarButtonOffset.Apply(this);
        var savedMode = AppPreferencesStore.Get("recorder.mode");
        _selectedMode = savedMode is "Instant" or "Studio" or "Screenshot" ? savedMode : "Studio";
        _screenOrdinal = int.TryParse(AppPreferencesStore.Get("capture.display.ordinal"), out var displayOrdinal) ? Math.Max(0, displayOrdinal) : 0;
        _captureTargetKind = AppPreferencesStore.Get("capture.target.kind") ?? "Display";
        _cameraDeviceIndex = int.TryParse(AppPreferencesStore.Get("capture.camera.index"), out var cameraIndex) ? cameraIndex : null;
        _microphoneDeviceIndex = int.TryParse(AppPreferencesStore.Get("capture.microphone.index"), out var microphoneIndex) ? microphoneIndex : null;
        _systemAudioDeviceIndex = int.TryParse(AppPreferencesStore.Get("capture.systemaudio.index"), out var systemAudioIndex) ? systemAudioIndex : null;
        _cameraEnabled = AppPreferencesStore.Get("capture.camera.enabled") == "true" && _cameraDeviceIndex is not null;
        _microphoneEnabled = AppPreferencesStore.Get("capture.microphone.enabled") == "true" && _microphoneDeviceIndex is not null;
        _systemAudioEnabled = AppPreferencesStore.Get("capture.systemaudio.enabled") == "true" && _systemAudioDeviceIndex is not null;
        _captureArea = ReadCaptureArea();
        if (_captureArea is not null)
            _screenOrdinal = _captureArea.ScreenOrdinal;
        ToolTip.SetTip(WindowTargetButton, OperatingSystem.IsMacOS()
            ? "Select a window's screen area"
            : "Choose an open window to capture");
        UpdateDeviceControls();
        ApplyModeVisuals();
        RefreshActivityIndicator(initializeReadState: true);
        AddHandler(KeyDownEvent, OnKeyboardShortcut, RoutingStrategies.Tunnel);
        if (OperatingSystem.IsMacOS())
        {
            try
            {
                _globalHotkeys = new MacGlobalHotkeyService(action =>
                    Dispatcher.UIThread.Post(() => _ = RunGlobalShortcutAsync(action)));
            }
            catch (Exception error)
            {
                ShowStatus("System-wide shortcuts could not be enabled: " + error.Message);
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _recordingOverlayWindow?.Close();
        _recordingSession?.StopForShutdown();
        _recordingSession = null;
        _globalHotkeys?.Dispose();
        base.OnClosed(e);
    }

    private void OnModeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string mode)
            return;

        if (_recordingSession is not null)
        {
            ShowStatus("Stop the current recording before changing mode.");
            return;
        }

        _selectedMode = mode;
        try
        {
            AppPreferencesStore.Set("recorder.mode", _selectedMode);
        }
        catch
        {
            ShowStatus("Could not save the selected mode.");
        }
        ApplyModeVisuals();
    }

    private async void OnCaptureButtonClick(object? sender, RoutedEventArgs e) => await OnCaptureButtonClickAsync();

    private async Task OnCaptureButtonClickAsync()
    {
        if (_captureBusy)
            return;

        _captureBusy = true;
        RecordButton.IsEnabled = false;
        try
        {
            if (_selectedMode == "Screenshot")
            {
                if ((OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) && _captureTargetKind == "Window" && !NativeWindowCapture.IsAvailable(_windowTarget))
                {
                    ShowStatus("Choose an open window before taking a window screenshot.");
                    return;
                }
                var path = await TakeScreenshotAsync();
                var backup = await ApplySaveAutomationAsync(path, screenshot: true);
                ShowStatus($"Screenshot saved: {Path.GetFileName(path)}{backup}");
                RefreshActivityIndicator();
                _settingsWindow?.RefreshCurrentPage();
                return;
            }

            if (_recordingSession is null)
            {
                if (!_cameraOnly && _captureTargetKind == "Window" && !NativeWindowCapture.IsAvailable(_windowTarget))
                {
                    ShowStatus("Choose an open window before recording.");
                    return;
                }
                if (!await EnsureSelectedDevicesAvailableAsync())
                    return;

                var microphoneWillRecord = _microphoneEnabled && _microphoneDeviceIndex is not null;
                if (!microphoneWillRecord && AppPreferencesStore.Get("general.Confirm before recording without a microphone") != "false")
                {
                    var confirmation = new ConfirmCaptureWindow();
                    if (!await confirmation.ShowAndWaitAsync(this))
                        return;
                }
                if (!await RunCountdownAsync())
                    return;
                var fpsValue = AppPreferencesStore.Get("general.Max capture framerate") ?? "60 FPS";
                var frameRate = int.TryParse(new string(fpsValue.Where(char.IsDigit).ToArray()), out var parsedFps) ? parsedFps : 60;
                var quality = AppPreferencesStore.Get("quality.studio") ?? "Balanced";
                PrepareRecorderWindowForCapture();
                _recordingSession = await MediaLibraryService.StartRecordingAsync(frameRate, quality,
                    _microphoneEnabled ? _microphoneDeviceIndex : null,
                    _systemAudioEnabled ? _systemAudioDeviceIndex : null,
                    _cameraEnabled ? _cameraDeviceIndex : null,
                    _cameraOnly, _screenOrdinal, _captureArea,
                    AppPreferencesStore.Get("general.project-name"), _selectedMode, _captureTargetKind, _windowTarget);
                RecordGlyph.Fill = new SolidColorBrush(Color.Parse("#F04444"));
                ToolTip.SetTip(RecordButton, "Stop recording");
                ShowStatus(_selectedMode == "Instant"
                    ? "Recording locally. Instant uploads are not configured."
                    : "Recording has started.");
                ShowRecordingOverlayIfNeeded();
                return;
            }

            var session = _recordingSession;
            var pathWhenStopped = await session.StopAsync();
            _recordingSession = null;
            try { AppPreferencesStore.Set("recording.mode." + pathWhenStopped, _selectedMode); } catch { }
            RecordGlyph.Fill = new SolidColorBrush(Color.Parse("#F2F2F2"));
            ToolTip.SetTip(RecordButton, "Start recording");
            var backupPath = await ApplySaveAutomationAsync(pathWhenStopped, screenshot: false);
            ShowStatus($"Recording saved: {Path.GetFileName(pathWhenStopped)}{backupPath}");
            RefreshActivityIndicator();
            _settingsWindow?.RefreshCurrentPage();
            _recordingOverlayWindow?.Close();
            _recordingOverlayWindow = null;
            RestoreRecorderWindow();
            Activate();
            if (_selectedMode == "Studio")
            {
                if (AppPreferencesStore.Get("general.After a Studio recording") == "Open in video player")
                {
                    try { MediaLibraryService.OpenVideoAt(pathWhenStopped); }
                    catch (Exception error) { ShowStatus(error.Message); }
                }
                else
                    new VideoEditorWindow(pathWhenStopped).Show(this);
            }
        }
        catch (Exception error)
        {
            _recordingSession = null;
            RecordGlyph.Fill = new SolidColorBrush(Color.Parse("#F2F2F2"));
            _recordingOverlayWindow?.Close();
            _recordingOverlayWindow = null;
            RestoreRecorderWindow();
            ToolTip.SetTip(RecordButton, _selectedMode == "Screenshot" ? "Take screenshot" : "Start recording");
            ShowStatus(error.Message);
        }
        finally
        {
            RecordButton.IsEnabled = true;
            _captureBusy = false;
        }
    }

    private void OnOpenSettings(object? sender, RoutedEventArgs e)
    {
        OpenSettings("General");
    }

    private void OnOpenHelp(object? sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://cap.so") { UseShellExecute = true });
        }
        catch (Exception error)
        {
            ShowStatus("Could not open Cap help: " + error.Message);
        }
    }

    private void OnRecordingModeInfoClick(object? sender, RoutedEventArgs e)
    {
        new RecordingModeInfoWindow().ShowDialog(this);
    }

    private void OnOpenScreenshots(object? sender, RoutedEventArgs e)
    {
        OpenSettings("Screenshots");
    }

    private void OnOpenRecordings(object? sender, RoutedEventArgs e)
    {
        OpenSettings("Recordings");
    }

    private void OnOpenTeleprompter(object? sender, RoutedEventArgs e)
    {
        if (_teleprompterWindow is { IsVisible: true })
        {
            _teleprompterWindow.Activate();
            return;
        }
        _teleprompterWindow = new TeleprompterWindow();
        _teleprompterWindow.Closed += (_, _) => _teleprompterWindow = null;
        _teleprompterWindow.Show(this);
    }

    private void OnOpenNotifications(object? sender, RoutedEventArgs e)
    {
        try { AppPreferencesStore.Set("activity.lastViewedUtc", DateTime.UtcNow.ToString("O")); } catch { }
        ActivityDot.IsVisible = false;
        if (_notificationsWindow is { IsVisible: true })
        {
            _notificationsWindow.Activate();
            return;
        }
        _notificationsWindow = new NotificationsWindow(this);
        _notificationsWindow.Closed += (_, _) => _notificationsWindow = null;
        _notificationsWindow.Show(this);
    }

    private void OnDisplayTargetClick(object? sender, RoutedEventArgs e)
    {
        _ = SelectDisplayAsync();
    }

    private Task<bool> SelectDisplayAsync()
    {
        if (_recordingSession is not null)
        {
            ShowStatus("Stop the current recording before changing displays.");
            return Task.FromResult(false);
        }

        var completion = new TaskCompletionSource<bool>();
        var picker = new ScreenPickerWindow(this, _screenOrdinal, (ordinal, bounds) =>
        {
            _screenOrdinal = ordinal;
            _captureTargetKind = "Display";
            _cameraOnly = false;
            _windowTarget = null;
            ToolTip.SetTip(WindowTargetButton, OperatingSystem.IsMacOS()
                ? "Select a window's screen area"
                : "Choose an open window to capture");
            _captureArea = OperatingSystem.IsMacOS()
                ? null
                : new CaptureArea(ordinal, 0, 0, bounds.Width, bounds.Height, bounds.X, bounds.Y);
            try
            {
                AppPreferencesStore.Set("capture.display.ordinal", ordinal.ToString());
                AppPreferencesStore.Set("capture.target.kind", _captureTargetKind);
                AppPreferencesStore.Set("capture.area", _captureArea is null
                    ? string.Empty
                    : $"{_captureArea.ScreenOrdinal},{_captureArea.X},{_captureArea.Y},{_captureArea.Width},{_captureArea.Height},{_captureArea.ScreenOriginX},{_captureArea.ScreenOriginY}");
            }
            catch { }
            ShowStatus($"Display {ordinal + 1} selected.");
            completion.TrySetResult(true);
        });
        picker.Closed += (_, _) => completion.TrySetResult(false);
        picker.Show(this);
        return completion.Task;
    }

    private async void OnWindowTargetClick(object? sender, RoutedEventArgs e) => _ = await SelectWindowAsync();

    private async void OnAreaTargetClick(object? sender, RoutedEventArgs e) =>
        _ = await SelectAreaAsync("Drag to select the area to capture", "Capture area selected.", "Area");

    private async Task<bool> SelectWindowAsync()
    {
        if (_recordingSession is not null)
        {
            ShowStatus("Stop the current recording before changing the capture target.");
            return false;
        }

        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            return await SelectAreaAsync("Drag around the window to capture its screen area", "Window area selected.", "Window");

        var target = await CaptureWindowPickerWindow.SelectAsync(this);
        if (target is null)
            return false;
        _windowTarget = target;
        _captureArea = null;
        _captureTargetKind = "Window";
        _cameraOnly = false;
        ToolTip.SetTip(WindowTargetButton, $"Window selected: {target.Title}");
        try
        {
            AppPreferencesStore.Set("capture.target.kind", "Window");
            AppPreferencesStore.Set("capture.area", string.Empty);
        }
        catch { }
        ShowStatus($"Window selected: {target.Title}");
        return true;
    }

    private async Task<bool> SelectAreaAsync(string instruction, string resultMessage, string targetKind)
    {
        if (_recordingSession is not null) { ShowStatus("Stop the current recording before changing the capture area."); return false; }
        var area = await AreaSelectionWindow.SelectAsync(this, _screenOrdinal, instruction);
        if (area is null)
            return false;
        _captureArea = area;
        _windowTarget = null;
        if (targetKind == "Window")
            ToolTip.SetTip(WindowTargetButton, "Select a window's screen area");
        _screenOrdinal = area.ScreenOrdinal;
        _captureTargetKind = targetKind;
        try
        {
            AppPreferencesStore.Set("capture.display.ordinal", _screenOrdinal.ToString());
            AppPreferencesStore.Set("capture.target.kind", _captureTargetKind);
            AppPreferencesStore.Set("capture.area", $"{area.ScreenOrdinal},{area.X},{area.Y},{area.Width},{area.Height},{area.ScreenOriginX},{area.ScreenOriginY}");
        }
        catch { }
        ShowStatus(resultMessage);
        return true;
    }

    private void OnCameraOnlyClick(object? sender, RoutedEventArgs e)
    {
        if (_cameraOnly)
        {
            _cameraOnly = false;
            ShowStatus(_captureTargetKind switch
            {
                "Window" => "Window capture is selected.",
                "Area" => "Area capture is selected.",
                _ => "Display capture is selected."
            });
            return;
        }

        if (_cameraDeviceIndex is null)
        {
            ChooseDevice("Camera", device =>
            {
                if (device is null)
                {
                    ShowStatus("Choose a camera to start Camera Only capture.");
                    return;
                }
                _cameraOnly = true;
                SetCameraEnabled(true);
                ShowStatus("Camera Only capture is selected.");
            });
            return;
        }

        _cameraOnly = true;
        SetCameraEnabled(true);
        ShowStatus("Camera Only capture is selected.");
    }

    private void OnCameraAudioClick(object? sender, RoutedEventArgs e)
    {
        if (_cameraDeviceIndex is null)
        {
            OnChooseCamera(sender, e);
            return;
        }
        SetCameraEnabled(!_cameraEnabled);
        ShowStatus(_cameraEnabled ? "Camera will be included in the recording." : "Camera is off.");
    }

    private void OnMicrophoneClick(object? sender, RoutedEventArgs e)
    {
        if (_microphoneDeviceIndex is null)
        {
            OnChooseMicrophone(sender, e);
            return;
        }
        SetMicrophoneEnabled(!_microphoneEnabled);
        ShowStatus(_microphoneEnabled ? "Microphone will be included in the recording." : "Microphone is off.");
    }

    private void OnChooseCamera(object? sender, RoutedEventArgs e) => ChooseDevice("Camera");
    private void OnChooseMicrophone(object? sender, RoutedEventArgs e) => ChooseDevice("Microphone");
    private void OnChooseSystemAudio(object? sender, RoutedEventArgs e) => ChooseDevice("System Audio");

    private void ChooseDevice(string kind, Action<CaptureDevice?>? afterSelected = null)
    {
        var existingIndex = kind switch
        {
            "Camera" => _cameraDeviceIndex,
            "Microphone" => _microphoneDeviceIndex,
            _ => _systemAudioDeviceIndex
        };
        new CaptureDevicePickerWindow(this, kind, existingIndex, device =>
        {
            if (kind == "Camera")
            {
                _cameraDeviceIndex = device?.Index;
                try { AppPreferencesStore.Set("capture.camera.index", device?.Index.ToString() ?? string.Empty); } catch { }
                try { AppPreferencesStore.Set("capture.camera.name", device?.Name ?? string.Empty); } catch { }
                SetCameraEnabled(device is not null);
                if (device is null)
                    _cameraOnly = false;
            }
            else if (kind == "Microphone")
            {
                _microphoneDeviceIndex = device?.Index;
                try { AppPreferencesStore.Set("capture.microphone.index", device?.Index.ToString() ?? string.Empty); } catch { }
                try { AppPreferencesStore.Set("capture.microphone.name", device?.Name ?? string.Empty); } catch { }
                SetMicrophoneEnabled(device is not null);
            }
            else
            {
                _systemAudioDeviceIndex = device?.Index;
                try { AppPreferencesStore.Set("capture.systemaudio.index", device?.Index.ToString() ?? string.Empty); } catch { }
                try { AppPreferencesStore.Set("capture.systemaudio.name", device?.Name ?? string.Empty); } catch { }
                SetSystemAudioEnabled(device is not null);
            }
            UpdateDeviceControls();
            afterSelected?.Invoke(device);
        }).Show(this);
    }

    private async Task<bool> EnsureSelectedDevicesAvailableAsync()
    {
        if (!_cameraEnabled && !_microphoneEnabled && !_systemAudioEnabled)
            return true;

        IReadOnlyList<CaptureDevice> available;
        try
        {
            available = await MediaLibraryService.ListCaptureDevicesAsync();
        }
        catch (Exception error)
        {
            ShowStatus("Could not check the selected capture devices: " + error.Message);
            return false;
        }

        if (_cameraEnabled && !IsStillAvailable(available, _cameraDeviceIndex, "Camera", "capture.camera.name"))
        {
            _cameraDeviceIndex = null;
            _cameraOnly = false;
            try { AppPreferencesStore.Set("capture.camera.index", string.Empty); } catch { }
            try { AppPreferencesStore.Set("capture.camera.name", string.Empty); } catch { }
            SetCameraEnabled(false);
            ShowStatus("The selected camera is no longer available. Choose a connected camera.");
            ChooseDevice("Camera");
            return false;
        }

        if (_microphoneEnabled && !IsStillAvailable(available, _microphoneDeviceIndex, "Microphone", "capture.microphone.name"))
        {
            _microphoneDeviceIndex = null;
            try { AppPreferencesStore.Set("capture.microphone.index", string.Empty); } catch { }
            try { AppPreferencesStore.Set("capture.microphone.name", string.Empty); } catch { }
            SetMicrophoneEnabled(false);
            ShowStatus("The selected microphone is no longer available. Choose a connected microphone.");
            ChooseDevice("Microphone");
            return false;
        }

        var systemAudioKind = OperatingSystem.IsMacOS() ? "Microphone" : "System Audio";
        if (_systemAudioEnabled && !IsStillAvailable(available, _systemAudioDeviceIndex, systemAudioKind, "capture.systemaudio.name"))
        {
            _systemAudioDeviceIndex = null;
            try { AppPreferencesStore.Set("capture.systemaudio.index", string.Empty); } catch { }
            try { AppPreferencesStore.Set("capture.systemaudio.name", string.Empty); } catch { }
            SetSystemAudioEnabled(false);
            ShowStatus("The selected loopback device is no longer available. Choose another device.");
            ChooseDevice("System Audio");
            return false;
        }

        return true;
    }

    private static bool IsStillAvailable(IReadOnlyList<CaptureDevice> devices, int? index, string kind, string nameKey)
    {
        if (index is null)
            return false;

        var expectedName = AppPreferencesStore.Get(nameKey);
        return devices.Any(device => device.Index == index && device.Kind == kind &&
            (string.IsNullOrWhiteSpace(expectedName) || string.Equals(device.Name, expectedName, StringComparison.Ordinal)));
    }

    private void SetCameraEnabled(bool enabled)
    {
        _cameraEnabled = enabled && _cameraDeviceIndex is not null;
        try { AppPreferencesStore.Set("capture.camera.enabled", _cameraEnabled.ToString().ToLowerInvariant()); } catch { }
        UpdateDeviceControls();
    }

    private void SetMicrophoneEnabled(bool enabled)
    {
        _microphoneEnabled = enabled && _microphoneDeviceIndex is not null;
        try { AppPreferencesStore.Set("capture.microphone.enabled", _microphoneEnabled.ToString().ToLowerInvariant()); } catch { }
        UpdateDeviceControls();
    }

    private void SetSystemAudioEnabled(bool enabled)
    {
        _systemAudioEnabled = enabled && _systemAudioDeviceIndex is not null;
        try { AppPreferencesStore.Set("capture.systemaudio.enabled", _systemAudioEnabled.ToString().ToLowerInvariant()); } catch { }
        UpdateDeviceControls();
    }

    private void UpdateDeviceControls()
    {
        var cameraName = AppPreferencesStore.Get("capture.camera.name");
        var microphoneName = AppPreferencesStore.Get("capture.microphone.name");
        var systemAudioName = AppPreferencesStore.Get("capture.systemaudio.name");
        CameraDeviceButton.Content = string.IsNullOrWhiteSpace(cameraName) ? "No Camera" : cameraName;
        MicrophoneDeviceButton.Content = string.IsNullOrWhiteSpace(microphoneName) ? "No Microphone" : microphoneName;
        SystemAudioDeviceButton.Content = _systemAudioEnabled ? "Record System Audio" : "No System Audio";
        ToolTip.SetTip(CameraDeviceButton, cameraName ?? "Choose a camera");
        ToolTip.SetTip(MicrophoneDeviceButton, microphoneName ?? "Choose a microphone");
        ToolTip.SetTip(SystemAudioDeviceButton, systemAudioName ?? "Choose a system audio loopback device");
        CameraAudioButton.Content = _cameraEnabled ? "On" : "Off";
        MicrophoneAudioButton.Content = _microphoneEnabled ? "On" : "Off";
        SystemAudioStatusButton.Content = _systemAudioEnabled ? "On" : "Off";
    }

    private static CaptureArea? ReadCaptureArea()
    {
        var saved = AppPreferencesStore.Get("capture.area");
        var parts = saved?.Split(',');
        if (parts is { Length: 5 or 7 } && parts.All(part => int.TryParse(part, out _)))
        {
            var values = parts.Select(int.Parse).ToArray();
            if (values[3] >= 2 && values[4] >= 2)
                return new CaptureArea(values[0], values[1], values[2], values[3], values[4],
                    values.Length == 7 ? values[5] : 0, values.Length == 7 ? values[6] : 0);
        }
        return null;
    }

    private void OnSystemAudioClick(object? sender, RoutedEventArgs e)
    {
        if (_systemAudioDeviceIndex is null)
        {
            OnChooseSystemAudio(sender, e);
            return;
        }
        SetSystemAudioEnabled(!_systemAudioEnabled);
        ShowStatus(_systemAudioEnabled
            ? "The selected audio loopback will be included in the recording."
            : "System audio is off.");
    }

    private void OpenSettings(string section)
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.NavigateTo(section);
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show(this);
        _settingsWindow.NavigateTo(section);
    }

    private void ShowStatus(string message)
    {
        CaptureStatus.Text = message;
        CaptureStatusToast.IsVisible = true;
        var generation = ++_statusGeneration;
        _ = HideStatusLaterAsync(generation);
    }

    private void RefreshActivityIndicator(bool initializeReadState = false)
    {
        var savedReadTime = AppPreferencesStore.Get("activity.lastViewedUtc");
        if (string.IsNullOrWhiteSpace(savedReadTime) && initializeReadState)
        {
            savedReadTime = DateTime.UtcNow.ToString("O");
            try { AppPreferencesStore.Set("activity.lastViewedUtc", savedReadTime); } catch { }
        }

        var readTime = DateTime.TryParse(savedReadTime, null,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : DateTime.MinValue;
        try
        {
            ActivityDot.IsVisible = MediaLibraryService.GetItems(screenshots: false)
                .Concat(MediaLibraryService.GetItems(screenshots: true))
                .Any(item => item.Modified.ToUniversalTime() > readTime);
        }
        catch
        {
            ActivityDot.IsVisible = false;
        }
    }

    internal void MarkActivityUnread() => ActivityDot.IsVisible = true;

    private async Task<bool> RunCountdownAsync()
    {
        var value = AppPreferencesStore.Get("general.Countdown") ?? "3 seconds";
        var seconds = value switch
        {
            "3 seconds" => 3,
            "5 seconds" => 5,
            _ => 0
        };
        if (seconds == 0)
            return true;
        _countdownCancelled = false;
        _countdownInProgress = true;
        try
        {
            for (var remaining = seconds; remaining > 0; remaining--)
            {
                if (_countdownCancelled)
                    return false;
                ShowStatus($"Recording starts in {remaining}… Press Escape to cancel.");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
            return !_countdownCancelled;
        }
        finally
        {
            _countdownInProgress = false;
        }
    }

    private static async Task<string> ApplySaveAutomationAsync(string path, bool screenshot)
    {
        try
        {
            var result = await AutomationService.ApplyAfterSaveAsync(path, screenshot);
            if (result is null)
                return string.Empty;
            return result.Contains("folder", StringComparison.OrdinalIgnoreCase) && !File.Exists(result)
                ? " · " + result
                : " · Copy saved to " + result;
        }
        catch (Exception error)
        {
            return " · Backup failed: " + error.Message;
        }
    }

    private void PrepareRecorderWindowForCapture()
    {
        var preference = AppPreferencesStore.Get("general.Main window when recording starts") ?? "Close";
        if (preference == "Keep open")
        {
            _restoreRecorderAfterRecording = false;
            return;
        }
        _restoreRecorderAfterRecording = true;
        _settingsWindow?.Hide();
        _teleprompterWindow?.Hide();
        _notificationsWindow?.Hide();
        if (preference == "Minimize")
            WindowState = WindowState.Minimized;
        else
            Hide();
    }

    private void ShowRecordingOverlayIfNeeded()
    {
        if (!_restoreRecorderAfterRecording)
            return;
        _recordingOverlayWindow?.Close();
        _recordingOverlayWindow = new RecordingOverlayWindow(this,
            () => _ = OnCaptureButtonClickAsync(),
            ToggleRecordingPause,
            OnKeyboardShortcut);
        _recordingOverlayWindow.Closed += (_, _) => _recordingOverlayWindow = null;
        _recordingOverlayWindow.Show();
    }

    private bool ToggleRecordingPause()
    {
        if (_recordingSession is null)
            throw new InvalidOperationException("There is no recording to pause.");
        var paused = _recordingSession.TogglePaused();
        _recordingOverlayWindow?.SetPaused(paused);
        return paused;
    }

    private void RestoreRecorderWindow()
    {
        if (!_restoreRecorderAfterRecording)
            return;
        _restoreRecorderAfterRecording = false;
        WindowState = WindowState.Normal;
        Show();
    }

    private async Task<string> TakeScreenshotAsync()
    {
        var wasMainVisible = IsVisible;
        var wasSettingsVisible = _settingsWindow?.IsVisible == true;
        var wasTeleprompterVisible = _teleprompterWindow?.IsVisible == true;
        var wasNotificationsVisible = _notificationsWindow?.IsVisible == true;
        var wasOverlayVisible = _recordingOverlayWindow?.IsVisible == true;
        if (wasSettingsVisible) _settingsWindow!.Hide();
        if (wasTeleprompterVisible) _teleprompterWindow!.Hide();
        if (wasNotificationsVisible) _notificationsWindow!.Hide();
        if (wasOverlayVisible) _recordingOverlayWindow!.Hide();
        if (wasMainVisible) Hide();
        await Task.Delay(250);
        try
        {
            return await MediaLibraryService.CaptureScreenshotAsync(_captureArea, _screenOrdinal, _windowTarget);
        }
        finally
        {
            if (wasMainVisible) Show();
            if (wasSettingsVisible) _settingsWindow?.Show(this);
            if (wasTeleprompterVisible) _teleprompterWindow?.Show(this);
            if (wasNotificationsVisible) _notificationsWindow?.Show(this);
            if (wasOverlayVisible) _recordingOverlayWindow?.Show();
            if (wasMainVisible)
                Activate();
            else
                _recordingOverlayWindow?.Activate();
        }
    }

    private async void OnKeyboardShortcut(object? sender, KeyEventArgs e)
    {
        if (_countdownInProgress && e.Key == Key.Escape)
        {
            _countdownCancelled = true;
            e.Handled = true;
            ShowStatus("Recording countdown cancelled.");
            return;
        }
        if (MatchesShortcut(e, "recording.picker", "Primary+Shift+P"))
        {
            e.Handled = true;
            OpenSettings("Recordings");
        }
        else if (MatchesShortcut(e, "recording.stop", "Primary+Alt+X"))
        {
            e.Handled = true;
            if (_recordingSession is not null)
                await OnCaptureButtonClickAsync();
        }
        else if (MatchesShortcut(e, "recording.pause", "Primary+Alt+P"))
        {
            e.Handled = true;
            try
            {
                if (_recordingSession is null)
                    ShowStatus("There is no recording to pause.");
                else
                    ShowStatus(ToggleRecordingPause() ? "Recording paused." : "Recording resumed.");
            }
            catch (Exception error)
            {
                ShowStatus(error.Message);
            }
        }
        else if (MatchesShortcut(e, "recording.restart", "Primary+Alt+R"))
        {
            e.Handled = true;
            await RestartRecordingAsync();
        }
        else if (MatchesShortcut(e, "recording.cycle", "Primary+Alt+M"))
        {
            e.Handled = true;
            if (_recordingSession is not null)
                ShowStatus("Stop the current recording before changing mode.");
            else
            {
                _selectedMode = _selectedMode switch { "Instant" => "Studio", "Studio" => "Screenshot", _ => "Instant" };
                try { AppPreferencesStore.Set("recorder.mode", _selectedMode); } catch { }
                ApplyModeVisuals();
            }
        }
        else if (MatchesShortcut(e, "screenshot.display", "Primary+Shift+S"))
        {
            e.Handled = true;
            await TakeShortcutScreenshotAsync(display: true);
        }
        else if (MatchesShortcut(e, "screenshot.window", "Primary+Shift+W"))
        {
            e.Handled = true;
            if (await SelectWindowAsync())
                await TakeShortcutScreenshotAsync(display: false);
        }
        else if (MatchesShortcut(e, "screenshot.area", "Primary+Shift+A"))
        {
            e.Handled = true;
            if (await SelectAreaAsync("Drag to select the area to capture", "Capture area selected.", "Area"))
                await TakeShortcutScreenshotAsync(display: false);
        }
        else if (MatchesShortcut(e, "recording.display", "Primary+Alt+D"))
        {
            e.Handled = true;
            if (await SelectDisplayAsync())
                await StartRecordingFromShortcutAsync();
        }
        else if (MatchesShortcut(e, "recording.window", "Primary+Alt+W"))
        {
            e.Handled = true;
            if (await SelectWindowAsync())
                await StartRecordingFromShortcutAsync();
        }
        else if (MatchesShortcut(e, "recording.area", "Primary+Alt+A"))
        {
            e.Handled = true;
            if (await SelectAreaAsync("Drag to select the area to capture", "Capture area selected.", "Area"))
                await StartRecordingFromShortcutAsync();
        }
        else if (MatchesShortcut(e, "record", "Primary+R"))
        {
            e.Handled = true;
            await OnCaptureButtonClickAsync();
        }
        else if (MatchesShortcut(e, "screenshot", "Primary+Shift+S"))
        {
            e.Handled = true;
            await TakeShortcutScreenshotAsync(display: false);
        }
        else if (MatchesShortcut(e, "mode.instant", "Primary+D1") ||
                 MatchesShortcut(e, "mode.studio", "Primary+D2") ||
                 MatchesShortcut(e, "mode.screenshot", "Primary+D3"))
        {
            e.Handled = true;
            if (_recordingSession is not null)
            {
                ShowStatus("Stop the current recording before changing mode.");
                return;
            }
            _selectedMode = MatchesShortcut(e, "mode.instant", "Primary+D1") ? "Instant" :
                MatchesShortcut(e, "mode.studio", "Primary+D2") ? "Studio" : "Screenshot";
            try { AppPreferencesStore.Set("recorder.mode", _selectedMode); } catch { }
            ApplyModeVisuals();
        }
    }

    private async Task RunGlobalShortcutAsync(string action)
    {
        switch (action)
        {
            case "recording.picker":
                OpenSettings("Recordings");
                break;
            case "recording.stop":
                if (_recordingSession is not null)
                    await OnCaptureButtonClickAsync();
                break;
            case "recording.pause":
                try
                {
                    if (_recordingSession is null)
                        ShowStatus("There is no recording to pause.");
                    else
                        ShowStatus(ToggleRecordingPause() ? "Recording paused." : "Recording resumed.");
                }
                catch (Exception error)
                {
                    ShowStatus(error.Message);
                }
                break;
            case "recording.restart":
                await RestartRecordingAsync();
                break;
            case "recording.cycle":
                if (_recordingSession is not null)
                    ShowStatus("Stop the current recording before changing mode.");
                else
                {
                    _selectedMode = _selectedMode switch { "Instant" => "Studio", "Studio" => "Screenshot", _ => "Instant" };
                    try { AppPreferencesStore.Set("recorder.mode", _selectedMode); } catch { }
                    ApplyModeVisuals();
                }
                break;
            case "screenshot.display":
                await TakeShortcutScreenshotAsync(display: true);
                break;
            case "screenshot.window":
                if (await SelectWindowAsync())
                    await TakeShortcutScreenshotAsync(display: false);
                break;
            case "screenshot.area":
                if (await SelectAreaAsync("Drag to select the area to capture", "Capture area selected.", "Area"))
                    await TakeShortcutScreenshotAsync(display: false);
                break;
            case "recording.display":
                if (await SelectDisplayAsync())
                    await StartRecordingFromShortcutAsync();
                break;
            case "recording.window":
                if (await SelectWindowAsync())
                    await StartRecordingFromShortcutAsync();
                break;
            case "recording.area":
                if (await SelectAreaAsync("Drag to select the area to capture", "Capture area selected.", "Area"))
                    await StartRecordingFromShortcutAsync();
                break;
        }
    }

    private async Task TakeShortcutScreenshotAsync(bool display)
    {
        if (!display && (OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) && _captureTargetKind == "Window" && !NativeWindowCapture.IsAvailable(_windowTarget))
        {
            ShowStatus("Choose an open window before taking a window screenshot.");
            return;
        }
        var previousArea = _captureArea;
        var previousWindow = _windowTarget;
        if (display)
        {
            _captureArea = null;
            _windowTarget = null;
        }
        try
        {
            var path = await TakeScreenshotAsync();
            var backup = await ApplySaveAutomationAsync(path, screenshot: true);
            ShowStatus($"Screenshot saved: {Path.GetFileName(path)}{backup}");
            RefreshActivityIndicator();
            _settingsWindow?.RefreshCurrentPage();
        }
        catch (Exception error)
        {
            ShowStatus(error.Message);
        }
        finally
        {
            if (display)
            {
                _captureArea = previousArea;
                _windowTarget = previousWindow;
            }
        }
    }

    private async Task StartRecordingFromShortcutAsync()
    {
        if (_recordingSession is not null)
        {
            ShowStatus("Stop the current recording before starting another.");
            return;
        }

        _selectedMode = "Studio";
        try { AppPreferencesStore.Set("recorder.mode", _selectedMode); } catch { }
        ApplyModeVisuals();
        await OnCaptureButtonClickAsync();
    }

    private async Task RestartRecordingAsync()
    {
        if (_recordingSession is null)
        {
            ShowStatus("There is no recording to restart.");
            return;
        }

        try
        {
            var oldSession = _recordingSession;
            var previousPath = await oldSession.StopAsync();
            _recordingSession = null;
            try { AppPreferencesStore.Set("recording.mode." + previousPath, _selectedMode); } catch { }
            RecordGlyph.Fill = new SolidColorBrush(Color.Parse("#F2F2F2"));
            ToolTip.SetTip(RecordButton, "Start recording");
            _recordingOverlayWindow?.Close();
            _recordingOverlayWindow = null;
            RestoreRecorderWindow();
            ShowStatus("Previous recording saved. Starting a new recording…");
            await OnCaptureButtonClickAsync();
        }
        catch (Exception error)
        {
            _recordingSession = null;
            _recordingOverlayWindow?.Close();
            _recordingOverlayWindow = null;
            RestoreRecorderWindow();
            ShowStatus("Could not restart recording: " + error.Message);
        }
    }

    private static bool MatchesShortcut(KeyEventArgs e, string name, string fallback)
    {
        var configured = AppPreferencesStore.Get("shortcut." + name);
        if (string.IsNullOrWhiteSpace(configured))
            return false;
        var parts = configured.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        var keyName = parts[^1];
        if (!Enum.TryParse<Key>(keyName, ignoreCase: true, out var expectedKey))
            return false;
        var keyMatches = e.Key == expectedKey ||
            expectedKey == Key.D1 && e.Key == Key.NumPad1 ||
            expectedKey == Key.D2 && e.Key == Key.NumPad2 ||
            expectedKey == Key.D3 && e.Key == Key.NumPad3;
        if (!keyMatches)
            return false;

        var expected = KeyModifiers.None;
        foreach (var part in parts.Take(parts.Length - 1))
        {
            expected |= part switch
            {
                "Primary" => OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control,
                "Control" => KeyModifiers.Control,
                "Meta" => KeyModifiers.Meta,
                "Shift" => KeyModifiers.Shift,
                "Alt" => KeyModifiers.Alt,
                _ => KeyModifiers.None
            };
        }

        const KeyModifiers relevant = KeyModifiers.Control | KeyModifiers.Meta | KeyModifiers.Shift | KeyModifiers.Alt;
        return (e.KeyModifiers & relevant) == expected;
    }

    private async Task HideStatusLaterAsync(int generation)
    {
        await Task.Delay(TimeSpan.FromSeconds(6));
        if (generation == _statusGeneration)
            CaptureStatusToast.IsVisible = false;
    }

    private void ApplyModeVisuals()
    {
        UpdateModeButton(InstantModeButton, InstantModeFrame, _selectedMode == "Instant");
        UpdateModeButton(StudioModeButton, StudioModeFrame, _selectedMode == "Studio");
        UpdateModeButton(ScreenshotModeButton, ScreenshotModeFrame, _selectedMode == "Screenshot");
        ToolTip.SetTip(RecordButton, _selectedMode == "Screenshot" ? "Take screenshot" : "Start recording");
    }

    private static void UpdateModeButton(Button button, Border frame, bool selected)
    {
        if (selected)
        {
            if (!button.Classes.Contains("selected"))
                button.Classes.Add("selected");
            frame.Background = new SolidColorBrush(Color.Parse("#292929"));
            frame.BorderBrush = new SolidColorBrush(Color.Parse("#3B82F6"));
            frame.BorderThickness = new Thickness(2.5);
        }
        else
        {
            button.Classes.Remove("selected");
            frame.Background = new SolidColorBrush(Color.Parse("#363636"));
            frame.BorderBrush = Brushes.Transparent;
            frame.BorderThickness = new Thickness(0);
        }
    }

    private void OnDragRegionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }
}
