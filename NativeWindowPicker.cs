using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CapAvalonia;

internal sealed record CaptureWindowTarget(long Handle, string Title);

internal static class NativeWindowCapture
{
    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr state);

    public static IReadOnlyList<CaptureWindowTarget> ListWindows()
    {
        if (OperatingSystem.IsLinux())
            return ListX11Windows();
        if (!OperatingSystem.IsWindows())
            return Array.Empty<CaptureWindowTarget>();

        var windows = new List<CaptureWindowTarget>();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || IsIconic(handle))
                return true;
            GetWindowThreadProcessId(handle, out var processId);
            if (processId == Environment.ProcessId)
                return true;
            var title = new StringBuilder(1024);
            if (GetWindowText(handle, title, title.Capacity) <= 0)
                return true;
            if (!GetWindowRect(handle, out var rect) || rect.Right - rect.Left < 80 || rect.Bottom - rect.Top < 60)
                return true;
            windows.Add(new CaptureWindowTarget(handle.ToInt64(), title.ToString().Trim()));
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public static bool IsAvailable(CaptureWindowTarget? target) => target is not null &&
        (OperatingSystem.IsWindows() ? IsWindow(new IntPtr(target.Handle)) :
            OperatingSystem.IsLinux() && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")));

    private static IReadOnlyList<CaptureWindowTarget> ListX11Windows()
    {
        var executable = FindExecutable("xwininfo");
        if (executable is null)
            return Array.Empty<CaptureWindowTarget>();
        try
        {
            using var process = Process.Start(new ProcessStartInfo(executable, "-root -tree")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process is null)
                return Array.Empty<CaptureWindowTarget>();
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(3000))
            {
                process.Kill(entireProcessTree: true);
                return Array.Empty<CaptureWindowTarget>();
            }
            var targets = new List<CaptureWindowTarget>();
            foreach (var line in output.Split('\n'))
            {
                var match = Regex.Match(line, @"^\s{2,}(0x[0-9a-fA-F]+)\s+""(.*?)""\s+\(");
                if (!match.Success || !long.TryParse(match.Groups[1].Value.AsSpan(2), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out var id))
                    continue;
                var title = match.Groups[2].Value.Trim();
                if (title.Length > 0 && !title.StartsWith("<", StringComparison.Ordinal))
                    targets.Add(new CaptureWindowTarget(id, title));
            }
            return targets.DistinctBy(target => target.Handle).ToArray();
        }
        catch
        {
            return Array.Empty<CaptureWindowTarget>();
        }
    }

    private static string? FindExecutable(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, name);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr state);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int maximumCount);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr handle, out WindowRect rect);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

internal sealed class CaptureWindowPickerWindow : Window
{
    private readonly TaskCompletionSource<CaptureWindowTarget?> _selection = new();

    private CaptureWindowPickerWindow()
    {
        Title = "Choose a window";
        Width = 480;
        Height = 520;
        MinWidth = 380;
        MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#171717"));
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        var items = new StackPanel { Spacing = 7, Margin = new Thickness(18) };
        var windows = NativeWindowCapture.ListWindows();
        foreach (var target in windows)
        {
            var selectedTarget = target;
            var button = new Button
            {
                Content = target.Title,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Padding = new Thickness(12, 10),
                Background = new SolidColorBrush(Color.Parse("#242424")),
                BorderBrush = new SolidColorBrush(Color.Parse("#383838")),
                BorderThickness = new Thickness(1),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(8)
            };
            button.Click += (_, _) => Finish(selectedTarget);
            items.Children.Add(button);
        }

        var body = new StackPanel { Spacing = 10, Margin = new Thickness(18) };
        body.Children.Add(new TextBlock
        {
            Text = windows.Count == 0
                ? OperatingSystem.IsLinux() ? "No windows found. Install xwininfo and use an X11/XWayland desktop to select a window." : "No open windows are available to capture."
                : "Choose an open window to capture, even when it is behind other windows.",
            Foreground = new SolidColorBrush(Color.Parse("#B0B0B0")),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
        body.Children.Add(new ScrollViewer { Content = items, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        var cancel = new Button
        {
            Content = "Cancel",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Padding = new Thickness(14, 7),
            Background = new SolidColorBrush(Color.Parse("#242424")),
            BorderBrush = new SolidColorBrush(Color.Parse("#383838")),
            BorderThickness = new Thickness(1),
            Foreground = Brushes.White,
            CornerRadius = new CornerRadius(8)
        };
        cancel.Click += (_, _) => Finish(null);
        body.Children.Add(cancel);
        Content = body;
        Closed += (_, _) => _selection.TrySetResult(null);
    }

    public static async Task<CaptureWindowTarget?> SelectAsync(Window owner)
    {
        var picker = new CaptureWindowPickerWindow();
        picker.Show(owner);
        return await picker._selection.Task;
    }

    private void Finish(CaptureWindowTarget? target)
    {
        _selection.TrySetResult(target);
        Close();
    }
}
