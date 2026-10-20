using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CapAvalonia;

internal sealed record MediaLibraryItem(string Path, string Name, long Size, DateTime Modified, string Mode);
internal sealed record CaptureDevice(int Index, string Name, string Kind, string? DeviceId = null);
internal sealed record CaptureArea(int ScreenOrdinal, int X, int Y, int Width, int Height, int ScreenOriginX = 0, int ScreenOriginY = 0);

internal static class MediaLibraryService
{
    private static readonly SemaphoreSlim ThumbnailSemaphore = new(2, 2);
    private static readonly string AppDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CapAvalonia");

    public static string DefaultRecordingsDirectory => Path.Combine(AppDataDirectory, "Recordings");
    public static string RecordingsDirectory
    {
        get
        {
            var configured = AppPreferencesStore.Get("recordings.directory");
            return string.IsNullOrWhiteSpace(configured) ? DefaultRecordingsDirectory : configured;
        }
    }
    public static string ScreenshotsDirectory => Path.Combine(AppDataDirectory, "Screenshots");
    public static bool IsFfmpegAvailable
    {
        get
        {
            try { _ = FindFfmpeg(); return true; }
            catch { return false; }
        }
    }

    public static IReadOnlySet<string> GetAvailableVideoEncoders()
    {
        try
        {
            var startInfo = new ProcessStartInfo(FindFfmpeg())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-encoders");
            using var process = Process.Start(startInfo);
            if (process is null)
                return new HashSet<string>(StringComparer.Ordinal);

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var output = outputTask.GetAwaiter().GetResult() + Environment.NewLine + errorTask.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
                return new HashSet<string>(StringComparer.Ordinal);

            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length >= 2 && parts[0].Length == 6)
                .Select(parts => parts[1])
                .ToHashSet(StringComparer.Ordinal);
        }
        catch
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    public static IReadOnlyList<MediaLibraryItem> GetItems(bool screenshots)
    {
        var directory = screenshots ? ScreenshotsDirectory : RecordingsDirectory;
        Directory.CreateDirectory(directory);

        var extensions = screenshots
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".bmp" }
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".mov", ".mkv", ".webm", ".m4v" };

        return Directory.EnumerateFiles(directory)
            .Select(path => new FileInfo(path))
            .Where(file => extensions.Contains(file.Extension))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new MediaLibraryItem(file.FullName, file.Name, file.Length, file.LastWriteTime,
                screenshots ? "Screenshot" : AppPreferencesStore.Get("recording.mode." + file.FullName) ?? "Studio"))
            .ToArray();
    }

    public static async Task<IReadOnlyList<string>> ImportAsync(Window owner, bool screenshots)
    {
        var imageType = new FilePickerFileType("Images")
        {
            Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp" }
        };
        var videoType = new FilePickerFileType("Videos")
        {
            Patterns = new[] { "*.mp4", "*.mov", "*.mkv", "*.webm", "*.m4v" }
        };

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = screenshots ? "Import screenshots" : "Import recordings",
            AllowMultiple = true,
            FileTypeFilter = new[] { screenshots ? imageType : videoType }
        });

        var destinationDirectory = screenshots ? ScreenshotsDirectory : RecordingsDirectory;
        Directory.CreateDirectory(destinationDirectory);
        var imported = new List<string>();

        foreach (var file in files)
        {
            var destination = UniqueDestination(destinationDirectory, file.Name);
            await using var source = await file.OpenReadAsync();
            await using var destinationStream = File.Create(destination);
            await source.CopyToAsync(destinationStream);
            imported.Add(destination);
        }

        return imported;
    }

    public static async Task<string> CaptureScreenshotAsync(CaptureArea? area = null, int screenOrdinal = 0,
        CaptureWindowTarget? windowTarget = null)
    {
        MacScreenCaptureAccess.EnsureAvailable();
        Directory.CreateDirectory(ScreenshotsDirectory);
        var path = Path.Combine(ScreenshotsDirectory, $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");

        if (OperatingSystem.IsMacOS() && area is null && screenOrdinal == 0)
        {
            await RunAndCheckAsync("/usr/sbin/screencapture", new[] { "-x", path }, TimeSpan.FromSeconds(30));
            EnsureCaptureFile(path, "screenshot");
            return path;
        }

        var ffmpeg = FindFfmpeg();
        var startInfo = await BuildCaptureStartInfoAsync(ffmpeg, path, screenshot: true, screenOrdinal: screenOrdinal, area: area, windowTarget: windowTarget);
        await RunAndCheckAsync(startInfo, TimeSpan.FromSeconds(30));
        EnsureCaptureFile(path, "screenshot");
        return path;
    }

    public static async Task<ActiveRecordingSession> StartRecordingAsync(int frameRate = 30, string quality = "Balanced",
        int? microphoneIndex = null, int? systemAudioIndex = null, int? cameraIndex = null, bool cameraOnly = false,
        int screenOrdinal = 0, CaptureArea? area = null, string? nameTemplate = null, string? recordingMode = null,
        string? targetKind = null, CaptureWindowTarget? windowTarget = null)
    {
        if (!cameraOnly)
            MacScreenCaptureAccess.EnsureAvailable();
        Directory.CreateDirectory(RecordingsDirectory);
        var baseName = BuildRecordingName(nameTemplate, recordingMode ?? "Studio", cameraOnly, area, targetKind);
        var path = UniqueDestination(RecordingsDirectory, baseName + ".mp4");
        var startInfo = await BuildCaptureStartInfoAsync(FindFfmpeg(), path, screenshot: false, frameRate, quality,
            microphoneIndex, systemAudioIndex, cameraIndex, cameraOnly, screenOrdinal, area, windowTarget);
        startInfo.ArgumentList.Insert(3, "-progress");
        startInfo.ArgumentList.Insert(4, "pipe:1");
        startInfo.ArgumentList.Insert(5, "-nostats");
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var errors = new StringBuilder();
        var firstFrameCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                errors.AppendLine(args.Data);
        };
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null && args.Data.StartsWith("frame=", StringComparison.Ordinal) &&
                int.TryParse(args.Data.AsSpan("frame=".Length), out var frame) && frame > 0)
                firstFrameCaptured.TrySetResult();
        };

        if (!process.Start())
            throw new InvalidOperationException("FFmpeg did not start.");

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        var exited = process.WaitForExitAsync();
        var startupTimeout = Task.Delay(TimeSpan.FromSeconds(15));
        var startupResult = await Task.WhenAny(firstFrameCaptured.Task, exited, startupTimeout);
        if (startupResult != firstFrameCaptured.Task)
        {
            if (startupResult == startupTimeout && !process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            }
            await exited;
            process.WaitForExit(); // Drain redirected FFmpeg output before inspecting the diagnostics.
            var exitedBeforeFirstFrame = startupResult == exited;
            var detail = UsefulError(errors.ToString(), exitedBeforeFirstFrame
                ? "FFmpeg stopped before it captured a video frame."
                : "FFmpeg did not capture a video frame within 15 seconds. Check Screen Recording permission and the selected capture devices.");
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            process.Dispose();
            throw new InvalidOperationException(detail);
        }

        return new ActiveRecordingSession(process, path, errors);
    }

    private static string BuildRecordingName(string? template, string mode, bool cameraOnly, CaptureArea? area, string? targetKind)
    {
        var now = DateTime.Now;
        targetKind = cameraOnly ? "Camera" : targetKind ?? (area is not null ? "Area" : "Display");
        var name = string.IsNullOrWhiteSpace(template)
            ? "{target_name} ({target_kind}) {date} {time}"
            : template;
        name = name.Replace("{target_name}", cameraOnly ? "Camera Recording" : "Screen Recording", StringComparison.OrdinalIgnoreCase)
            .Replace("{target_kind}", targetKind, StringComparison.OrdinalIgnoreCase)
            .Replace("{recording_mode}", mode, StringComparison.OrdinalIgnoreCase)
            .Replace("{mode}", mode, StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", now.ToString("HH-mm"), StringComparison.OrdinalIgnoreCase);
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(name.Select(character => invalid.Contains(character) ? '-' : character).ToArray()).Trim(' ', '.');
        return string.IsNullOrWhiteSpace(cleaned) ? $"Recording_{now:yyyyMMdd_HHmmss_fff}" : cleaned;
    }

    public static void OpenFile(string path)
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public static string RenameMedia(string path, string newBaseName)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("This media file no longer exists.", fullPath);
        var cleanedName = newBaseName.Trim();
        if (string.IsNullOrWhiteSpace(cleanedName) || cleanedName is "." or ".." ||
            cleanedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || cleanedName.Contains('/') || cleanedName.Contains('\\'))
            throw new ArgumentException("Enter a valid file name.", nameof(newBaseName));

        var directory = Path.GetDirectoryName(fullPath)!;
        var extension = Path.GetExtension(fullPath);
        var destination = Path.Combine(directory, cleanedName + extension);
        if (string.Equals(fullPath, destination, OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            return fullPath;
        if (File.Exists(destination))
            throw new IOException("A file with that name already exists in this library.");

        var mode = AppPreferencesStore.Get("recording.mode." + fullPath);
        var trimStart = AppPreferencesStore.Get("trim.start." + fullPath);
        var trimEnd = AppPreferencesStore.Get("trim.end." + fullPath);
        File.Move(fullPath, destination);
        try
        {
            if (mode is not null)
            {
                AppPreferencesStore.Set("recording.mode." + destination, mode);
                AppPreferencesStore.Remove("recording.mode." + fullPath);
            }
            if (trimStart is not null)
            {
                AppPreferencesStore.Set("trim.start." + destination, trimStart);
                AppPreferencesStore.Remove("trim.start." + fullPath);
            }
            if (trimEnd is not null)
            {
                AppPreferencesStore.Set("trim.end." + destination, trimEnd);
                AppPreferencesStore.Remove("trim.end." + fullPath);
            }
        }
        catch { /* The media file rename succeeds independently of optional metadata persistence. */ }
        return destination;
    }

    public static void OpenDirectory(string path)
    {
        var directory = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }

    public static async Task CopyToClipboardAsync(TopLevel owner, string path, bool image)
    {
        var clipboard = owner.Clipboard
            ?? throw new InvalidOperationException("The system clipboard is not available.");

        if (image)
        {
            using var bitmap = new Bitmap(path);
            await clipboard.SetBitmapAsync(bitmap);
        }
        else
        {
            var file = await owner.StorageProvider.TryGetFileFromPathAsync(path);
            if (file is null)
                throw new FileNotFoundException("This media file could not be opened.", path);
            await clipboard.SetFileAsync(file);
        }

        await clipboard.FlushAsync();
    }

    public static async Task MoveToTrashAsync(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("This media file no longer exists.", fullPath);

        if (OperatingSystem.IsWindows())
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(fullPath,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            await Task.Run(() => MacFileTrash.Move(fullPath));
            return;
        }

        var gio = FindOnPath("gio")
            ?? throw new FileNotFoundException("Install gio to move files to the system Trash.");
        await RunAndCheckAsync(gio, new[] { "trash", fullPath }, TimeSpan.FromMinutes(2));
    }

    public static string FormatSize(long size)
    {
        var units = new[] { "B", "KB", "MB", "GB" };
        var value = (double)size;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.#} {units[unit]}";
    }

    public static async Task<double> GetVideoDurationAsync(string path)
    {
        var startInfo = new ProcessStartInfo(FindFfprobe())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        foreach (var argument in new[] { "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", path })
            startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not inspect this video.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0 || !double.TryParse(output.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var duration) || duration <= 0)
            throw new InvalidOperationException(UsefulError(error, "Could not read the video duration."));
        return duration;
    }

    public static async Task<IReadOnlyList<CaptureDevice>> ListMacCaptureDevicesAsync()
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("Camera and microphone selection is currently supported on macOS.");
        var output = await ReadMacDeviceListAsync(FindFfmpeg());
        var devices = new List<CaptureDevice>();
        var section = "";
        foreach (var line in output.Split('\n'))
        {
            if (line.Contains("AVFoundation video devices:", StringComparison.OrdinalIgnoreCase))
            {
                section = "video";
                continue;
            }
            if (line.Contains("AVFoundation audio devices:", StringComparison.OrdinalIgnoreCase))
            {
                section = "audio";
                continue;
            }
            var match = Regex.Match(line, @"\[(\d+)\]\s+(.+?)\s*$");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var index))
                continue;
            var name = match.Groups[2].Value.Trim();
            if (section == "audio")
                devices.Add(new CaptureDevice(index, name, "Microphone"));
            else if (section == "video")
                devices.Add(new CaptureDevice(index, name, name.Contains("Capture screen", StringComparison.OrdinalIgnoreCase) ? "Screen" : "Camera"));
        }
        return devices;
    }

    public static Task<IReadOnlyList<CaptureDevice>> ListCaptureDevicesAsync() => OperatingSystem.IsMacOS()
        ? ListMacCaptureDevicesAsync()
        : OperatingSystem.IsWindows()
            ? ListWindowsCaptureDevicesAsync()
            : OperatingSystem.IsLinux()
                ? ListLinuxCaptureDevicesAsync()
                : Task.FromException<IReadOnlyList<CaptureDevice>>(new PlatformNotSupportedException("Capture device discovery is not supported on this platform."));

    private static async Task<IReadOnlyList<CaptureDevice>> ListWindowsCaptureDevicesAsync()
    {
        var startInfo = new ProcessStartInfo(FindFfmpeg())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        foreach (var argument in new[] { "-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy" })
            startInfo.ArgumentList.Add(argument);
        var output = await ReadProcessDiagnosticsAsync(startInfo, TimeSpan.FromSeconds(15));
        var devices = new List<CaptureDevice>();
        var section = "";
        foreach (var line in output.Split('\n'))
        {
            if (line.Contains("DirectShow video devices", StringComparison.OrdinalIgnoreCase))
            {
                section = "video";
                continue;
            }
            if (line.Contains("DirectShow audio devices", StringComparison.OrdinalIgnoreCase))
            {
                section = "audio";
                continue;
            }
            if (section.Length == 0 || line.Contains("Alternative name", StringComparison.OrdinalIgnoreCase))
                continue;
            var match = Regex.Match(line, "\\\"(?<name>.+?)\\\"\\s*$");
            if (!match.Success)
                continue;
            var name = match.Groups["name"].Value.Trim();
            var kind = section == "video" ? "Camera" : IsLikelySystemAudioSource(name) ? "System Audio" : "Microphone";
            devices.Add(new CaptureDevice(devices.Count(device => device.Kind == kind), name, kind, name));
        }
        return devices;
    }

    private static async Task<IReadOnlyList<CaptureDevice>> ListLinuxCaptureDevicesAsync()
    {
        var devices = new List<CaptureDevice>();
        var videoDirectory = "/dev";
        if (Directory.Exists(videoDirectory))
        {
            foreach (var path in Directory.GetFiles(videoDirectory, "video*").OrderBy(value => value, StringComparer.Ordinal))
            {
                var sysfsName = Path.Combine("/sys/class/video4linux", Path.GetFileName(path), "name");
                var name = File.Exists(sysfsName) ? (await File.ReadAllTextAsync(sysfsName)).Trim() : Path.GetFileName(path);
                devices.Add(new CaptureDevice(devices.Count(device => device.Kind == "Camera"), name, "Camera", path));
            }
        }

        var pactlPath = FindOnPath("pactl");
        if (pactlPath is not null)
        {
            try
            {
                var pactl = new ProcessStartInfo(pactlPath)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                pactl.ArgumentList.Add("list");
                pactl.ArgumentList.Add("short");
                pactl.ArgumentList.Add("sources");
                var listedSources = await ReadProcessDiagnosticsAsync(pactl, TimeSpan.FromSeconds(15));
                foreach (var line in listedSources.Split('\n'))
                {
                    var columns = Regex.Split(line.Trim(), @"\s+");
                    if (columns.Length < 2 || !int.TryParse(columns[0], out _))
                        continue;
                    var id = columns[1];
                    var kind = IsLikelySystemAudioSource(id) ? "System Audio" : "Microphone";
                    devices.Add(new CaptureDevice(devices.Count(device => device.Kind == kind), id, kind, id));
                }
                if (devices.Any(device => device.Kind is "Microphone" or "System Audio"))
                    return devices;
            }
            catch
            {
                // Fall back to FFmpeg's own source listing when PulseAudio is unavailable.
            }
        }

        var pulse = new ProcessStartInfo(FindFfmpeg())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        foreach (var argument in new[] { "-hide_banner", "-sources", "pulse" })
            pulse.ArgumentList.Add(argument);
        var sources = await ReadProcessDiagnosticsAsync(pulse, TimeSpan.FromSeconds(15));
        foreach (var line in sources.Split('\n'))
        {
            var match = Regex.Match(line, @"^\s*(?<id>[^\s\[\]]+)(?:\s+\[(?<name>.+?)\])?\s*$");
            if (!match.Success || match.Groups["id"].Value.Contains(':'))
                continue;
            var id = match.Groups["id"].Value;
            var name = match.Groups["name"].Success ? match.Groups["name"].Value.Trim() : id;
            var kind = IsLikelySystemAudioSource(id + " " + name) ? "System Audio" : "Microphone";
            devices.Add(new CaptureDevice(devices.Count(device => device.Kind == kind), name, kind, id));
        }
        return devices;
    }

    private static bool IsLikelySystemAudioSource(string name)
    {
        var normalized = name.ToLowerInvariant();
        return new[] { "monitor", "loopback", "stereo mix", "what u hear", "vb-cable", "virtual audio" }
            .Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }

    private static async Task<string> ReadProcessDiagnosticsAsync(ProcessStartInfo startInfo, TimeSpan timeout)
    {
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not inspect capture devices.");
        var errorTask = process.StandardError.ReadToEndAsync();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var exitTask = process.WaitForExitAsync();
        if (await Task.WhenAny(exitTask, Task.Delay(timeout)) != exitTask)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("Capture device discovery timed out.");
        }
        await exitTask;
        return (await errorTask) + Environment.NewLine + (await outputTask);
    }

    public static async Task<string> CreateVideoThumbnailAsync(string path, double seconds)
    {
        var directory = Path.Combine(AppDataDirectory, "PreviewCache");
        Directory.CreateDirectory(directory);
        var previewTime = Math.Max(0, seconds);
        var source = new FileInfo(path);
        var cacheIdentity = $"{source.FullName}|{source.Length}|{source.LastWriteTimeUtc.Ticks}|{previewTime:0.###}";
        var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheIdentity)))[..20];
        var thumbnail = Path.Combine(directory, cacheKey + ".jpg");
        if (File.Exists(thumbnail))
            return thumbnail;
        var startInfo = new ProcessStartInfo(FindFfmpeg())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-y", "-ss", previewTime.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), "-i", path, "-frames:v", "1", "-vf", "scale=1280:-2", thumbnail })
            startInfo.ArgumentList.Add(argument);
        await ThumbnailSemaphore.WaitAsync();
        try
        {
            await RunAndCheckAsync(startInfo, TimeSpan.FromSeconds(45));
        }
        finally
        {
            ThumbnailSemaphore.Release();
        }
        return thumbnail;
    }

    public static async Task ExportTrimmedVideoAsync(string inputPath, string outputPath, double start, double end, string quality = "Balanced")
    {
        if (end <= start)
            throw new ArgumentOutOfRangeException(nameof(end), "The end time must be after the start time.");

        var startInfo = new ProcessStartInfo(FindFfmpeg())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        var duration = end - start;
        var arguments = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-y", "-ss",
            start.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), "-i", inputPath,
            "-t", duration.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "-c:v", OperatingSystem.IsMacOS() ? "h264_videotoolbox" : "libx264"
        };
        var normalizedQuality = quality switch
        {
            "Smaller file" => "Smaller files",
            "Higher quality" => "Maximum detail",
            _ => quality
        };
        if (OperatingSystem.IsMacOS())
            arguments.AddRange(new[] { "-b:v", normalizedQuality switch { "Smaller files" => "3M", "Maximum detail" => "10M", _ => "6M" } });
        else
            arguments.AddRange(new[] { "-preset", "ultrafast", "-crf", normalizedQuality switch { "Smaller files" => "28", "Maximum detail" => "18", _ => "23" } });
        arguments.AddRange(new[] { "-c:a", "aac", "-movflags", "+faststart", outputPath });
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        await RunAndCheckAsync(startInfo, TimeSpan.FromHours(2));
        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            throw new InvalidOperationException("The export finished without producing a video file.");
    }

    public static void OpenVideoAt(string path, double seconds = 0)
    {
        var ffplay = FindFfplay();
        Process.Start(new ProcessStartInfo(ffplay)
        {
            UseShellExecute = false,
            ArgumentList = { "-ss", Math.Max(0, seconds).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), "-i", path }
        });
    }

    private static async Task<ProcessStartInfo> BuildCaptureStartInfoAsync(string ffmpeg, string outputPath, bool screenshot,
        int frameRate = 30, string quality = "Balanced", int? microphoneIndex = null, int? systemAudioIndex = null,
        int? cameraIndex = null, bool cameraOnly = false, int screenOrdinal = 0, CaptureArea? area = null,
        CaptureWindowTarget? windowTarget = null)
    {
        var startInfo = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-y");
        var capturePointer = !screenshot && AppPreferencesStore.Get("general.Show pointer in screen recordings") != "false";

        if (OperatingSystem.IsMacOS())
        {
            var devices = await ListMacCaptureDevicesAsync();
            var screens = devices.Where(device => device.Kind == "Screen").OrderBy(device => device.Index).ToArray();
            if (cameraOnly)
            {
                if (cameraIndex is null)
                    cameraIndex = devices.FirstOrDefault(device => device.Kind == "Camera")?.Index;
                if (cameraIndex is null)
                    throw new InvalidOperationException("Choose a camera before starting Camera Only recording.");
                AddAvFoundationInput(startInfo, $"{cameraIndex}:none", Math.Min(frameRate, 30), captureCursor: false);
            }
            else
            {
                if (area is not null)
                    screenOrdinal = area.ScreenOrdinal;
                if (screenOrdinal < 0 || screenOrdinal >= screens.Length)
                    screenOrdinal = 0;
                var screenIndex = screens.Length > 0 ? screens[screenOrdinal].Index : await FindMacScreenIndexAsync(ffmpeg);
                AddAvFoundationInput(startInfo, $"{screenIndex}:none", screenshot ? 1 : frameRate, captureCursor: capturePointer);
            }

            if (!screenshot && !cameraOnly && cameraIndex is not null)
                AddAvFoundationInput(startInfo, $"{cameraIndex}:none", Math.Min(frameRate, 30), captureCursor: false);

            var audioInputIndices = new List<int>();
            var videoInputCount = cameraOnly || cameraIndex is null || screenshot ? 1 : 2;
            if (!screenshot && microphoneIndex is not null)
            {
                audioInputIndices.Add(videoInputCount++);
                AddAvFoundationInput(startInfo, $":{microphoneIndex}", frameRate, captureCursor: false);
            }
            if (!screenshot && systemAudioIndex is not null)
            {
                audioInputIndices.Add(videoInputCount);
                AddAvFoundationInput(startInfo, $":{systemAudioIndex}", frameRate, captureCursor: false);
            }

            var cameraOverlay = !screenshot && !cameraOnly && cameraIndex is not null;
            var filterGraph = new List<string>();
            if (cameraOverlay)
            {
                if (area is not null)
                    filterGraph.Add($"[0:v]crop={area.Width}:{area.Height}:{area.X}:{area.Y}[screenSource]");
                else
                    filterGraph.Add("[0:v]null[screenSource]");

                filterGraph.Add("[1:v]crop='min(iw,ih)':'min(iw,ih)'[cameraSquare]");
                filterGraph.Add("[cameraSquare][screenSource]scale2ref=w='min(480,rw*0.18)':h='min(480,rw*0.18)'[cameraSized][screen]");
                filterGraph.Add("[cameraSized]format=rgba," +
                    "geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':a='clip(min(W,H)/2+0.5-sqrt(pow(X-W/2,2)+pow(Y-H/2,2)),0,1)*255'[camera]");
                filterGraph.Add("[screen][camera]overlay=main_w-overlay_w-28:main_h-overlay_h-28:format=auto[video]");
                startInfo.ArgumentList.Add("-map");
                startInfo.ArgumentList.Add("[video]");
            }
            else if (area is not null && audioInputIndices.Count > 1)
            {
                filterGraph.Add($"[0:v]crop={area.Width}:{area.Height}:{area.X}:{area.Y}[video]");
                startInfo.ArgumentList.Add("-map");
                startInfo.ArgumentList.Add("[video]");
            }
            else if (area is not null)
            {
                startInfo.ArgumentList.Add("-vf");
                startInfo.ArgumentList.Add($"crop={area.Width}:{area.Height}:{area.X}:{area.Y}");
            }

            if (audioInputIndices.Count == 1)
            {
                if (!cameraOverlay)
                {
                    startInfo.ArgumentList.Add("-map");
                    startInfo.ArgumentList.Add("0:v:0");
                }
                startInfo.ArgumentList.Add("-map");
                startInfo.ArgumentList.Add($"{audioInputIndices[0]}:a:0?");
                startInfo.ArgumentList.Add("-c:a");
                startInfo.ArgumentList.Add("aac");
                if (microphoneIndex is not null && AppPreferencesStore.Get("general.Studio Sound on new recordings") != "false")
                {
                    startInfo.ArgumentList.Add("-filter:a:0");
                    startInfo.ArgumentList.Add("afftdn");
                }
            }
            else if (audioInputIndices.Count > 1)
            {
                if (!cameraOverlay && area is null)
                {
                    startInfo.ArgumentList.Add("-map");
                    startInfo.ArgumentList.Add("0:v:0");
                }

                var audioLabels = new List<string>();
                for (var index = 0; index < audioInputIndices.Count; index++)
                {
                    var inputIndex = audioInputIndices[index];
                    var label = $"audio{index}";
                    var isMicrophone = microphoneIndex is not null && index == 0;
                    if (isMicrophone && AppPreferencesStore.Get("general.Studio Sound on new recordings") != "false")
                        filterGraph.Add($"[{inputIndex}:a:0]afftdn[{label}]");
                    else
                        label = $"{inputIndex}:a:0";
                    audioLabels.Add($"[{label}]");
                }
                filterGraph.Add($"{string.Concat(audioLabels)}amix=inputs={audioInputIndices.Count}:duration=longest:dropout_transition=2[audio]");
                startInfo.ArgumentList.Add("-map");
                startInfo.ArgumentList.Add("[audio]");
                startInfo.ArgumentList.Add("-c:a");
                startInfo.ArgumentList.Add("aac");
            }
            else if (cameraOverlay)
            {
                // The filtered webcam composition is already explicitly mapped above.
            }

            if (filterGraph.Count > 0)
            {
                startInfo.ArgumentList.Add("-filter_complex");
                startInfo.ArgumentList.Add(string.Join(';', filterGraph));
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            if (!cameraOnly)
            {
                startInfo.ArgumentList.Add("-f");
                startInfo.ArgumentList.Add("gdigrab");
                startInfo.ArgumentList.Add("-draw_mouse");
                startInfo.ArgumentList.Add(capturePointer ? "1" : "0");
                startInfo.ArgumentList.Add("-framerate");
                startInfo.ArgumentList.Add(screenshot ? "1" : Math.Clamp(frameRate, 1, 120).ToString());
                if (windowTarget is not null)
                {
                    if (!NativeWindowCapture.IsAvailable(windowTarget))
                        throw new InvalidOperationException("The selected window has closed. Choose an open window and try again.");
                }
                else if (area is not null)
                {
                    startInfo.ArgumentList.Add("-offset_x");
                    startInfo.ArgumentList.Add((area.X + area.ScreenOriginX).ToString());
                    startInfo.ArgumentList.Add("-offset_y");
                    startInfo.ArgumentList.Add((area.Y + area.ScreenOriginY).ToString());
                    startInfo.ArgumentList.Add("-video_size");
                    startInfo.ArgumentList.Add($"{area.Width}x{area.Height}");
                }
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(windowTarget is null ? "desktop" : $"hwnd=0x{windowTarget.Handle:X}");
            }
        }
        else
        {
            if (!cameraOnly)
            {
                var display = Environment.GetEnvironmentVariable("DISPLAY");
                if (string.IsNullOrWhiteSpace(display))
                    throw new InvalidOperationException("No X11 display was found. Linux screen recording currently requires an X11 or XWayland display.");

                var primary = windowTarget is not null
                    ? (0, 0, 0, 0)
                    : area is null ? GetX11PrimaryMode() : (area.Width, area.Height, 0, 0);
                startInfo.ArgumentList.Add("-f");
                startInfo.ArgumentList.Add("x11grab");
                startInfo.ArgumentList.Add("-draw_mouse");
                startInfo.ArgumentList.Add(capturePointer ? "1" : "0");
                startInfo.ArgumentList.Add("-framerate");
                startInfo.ArgumentList.Add(screenshot ? "1" : Math.Clamp(frameRate, 1, 120).ToString());
                if (windowTarget is not null)
                {
                    startInfo.ArgumentList.Add("-window_id");
                    startInfo.ArgumentList.Add($"0x{windowTarget.Handle:X}");
                }
                else
                {
                    startInfo.ArgumentList.Add("-video_size");
                    startInfo.ArgumentList.Add($"{primary.Item1}x{primary.Item2}");
                    var offsetX = area is null ? primary.Item3 : area.X + area.ScreenOriginX;
                    var offsetY = area is null ? primary.Item4 : area.Y + area.ScreenOriginY;
                    display += $"+{offsetX},{offsetY}";
                }
                startInfo.ArgumentList.Add("-i");
                startInfo.ArgumentList.Add(display);
            }
        }

        if (!OperatingSystem.IsMacOS() && !screenshot)
            await AddNonMacCaptureDevicesAsync(startInfo, frameRate, microphoneIndex, systemAudioIndex, cameraIndex, cameraOnly);

        if (screenshot)
        {
            startInfo.ArgumentList.Add("-frames:v");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-update");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add(outputPath);
        }
        else
        {
            if (OperatingSystem.IsMacOS())
            {
                startInfo.ArgumentList.Add("-c:v");
                startInfo.ArgumentList.Add("h264_videotoolbox");
                startInfo.ArgumentList.Add("-b:v");
                startInfo.ArgumentList.Add(quality switch { "Smaller files" => "3M", "Maximum detail" => "10M", _ => "6M" });
            }
            else
            {
                startInfo.ArgumentList.Add("-c:v");
                startInfo.ArgumentList.Add("libx264");
                startInfo.ArgumentList.Add("-preset");
                startInfo.ArgumentList.Add("ultrafast");
                startInfo.ArgumentList.Add("-crf");
                startInfo.ArgumentList.Add(quality switch { "Smaller files" => "28", "Maximum detail" => "18", _ => "23" });
            }

            startInfo.ArgumentList.Add("-pix_fmt");
            startInfo.ArgumentList.Add("yuv420p");
            startInfo.ArgumentList.Add("-movflags");
            startInfo.ArgumentList.Add(AppPreferencesStore.Get("general.Crash-recoverable recording") == "false"
                ? "+faststart"
                : "+frag_keyframe+empty_moov+default_base_moof");
            startInfo.ArgumentList.Add(outputPath);
        }

        return startInfo;
    }

    private static async Task AddNonMacCaptureDevicesAsync(ProcessStartInfo startInfo, int frameRate,
        int? microphoneIndex, int? systemAudioIndex, int? cameraIndex, bool cameraOnly)
    {
        if (!cameraOnly && cameraIndex is null && microphoneIndex is null && systemAudioIndex is null)
        {
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("0:v:0");
            return;
        }

        var devices = await ListCaptureDevicesAsync();
        CaptureDevice? Resolve(int? index, string kind) => index is null
            ? null
            : devices.FirstOrDefault(device => device.Index == index && device.Kind == kind);

        var camera = Resolve(cameraIndex, "Camera");
        if (cameraOnly && camera is null)
            camera = devices.FirstOrDefault(device => device.Kind == "Camera");
        if (cameraOnly && camera is null)
            throw new InvalidOperationException("No camera was found. Connect a camera and select it before starting Camera Only recording.");
        if (cameraIndex is not null && camera is null)
            throw new InvalidOperationException("The selected camera is no longer available. Choose another camera.");

        var screenVideoIndex = cameraOnly ? (int?)null : 0;
        var nextInputIndex = cameraOnly ? 0 : 1;
        int? cameraInputIndex = null;
        if (camera is not null)
        {
            cameraInputIndex = nextInputIndex++;
            AddCameraInput(startInfo, camera, frameRate);
        }
        var microphone = Resolve(microphoneIndex, "Microphone");
        var systemAudio = Resolve(systemAudioIndex, "System Audio");
        if (microphoneIndex is not null && microphone is null)
            throw new InvalidOperationException("The selected microphone is no longer available. Choose another microphone.");
        if (systemAudioIndex is not null && systemAudio is null)
            throw new InvalidOperationException("The selected system-audio source is no longer available. Choose another source.");

        var audioInputs = new List<(int Index, bool IsMicrophone)>();
        if (microphone is not null)
        {
            audioInputs.Add((nextInputIndex++, true));
            AddAudioInput(startInfo, microphone);
        }
        if (systemAudio is not null)
        {
            audioInputs.Add((nextInputIndex, false));
            AddAudioInput(startInfo, systemAudio);
        }

        var filterGraph = new List<string>();
        var cameraOverlay = !cameraOnly && screenVideoIndex is not null && cameraInputIndex is not null;
        if (cameraOverlay)
        {
            filterGraph.Add($"[{cameraInputIndex}:v]crop='min(iw,ih)':'min(iw,ih)'[cameraSquare]");
            filterGraph.Add($"[cameraSquare][{screenVideoIndex}:v]scale2ref=w='min(480,rw*0.18)':h='min(480,rw*0.18)'[cameraSized][screen]");
            filterGraph.Add("[cameraSized]format=rgba," +
                "geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':a='clip(min(W,H)/2+0.5-sqrt(pow(X-W/2,2)+pow(Y-H/2,2)),0,1)*255'[camera]");
            filterGraph.Add("[screen][camera]overlay=main_w-overlay_w-28:main_h-overlay_h-28:format=auto[video]");
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("[video]");
        }
        else
        {
            var videoInput = cameraOnly ? cameraInputIndex : screenVideoIndex;
            if (videoInput is null)
                throw new InvalidOperationException("No video input is available for this recording.");
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add($"{videoInput}:v:0");
        }

        if (audioInputs.Count == 1)
        {
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add($"{audioInputs[0].Index}:a:0?");
            startInfo.ArgumentList.Add("-c:a");
            startInfo.ArgumentList.Add("aac");
            if (audioInputs[0].IsMicrophone && AppPreferencesStore.Get("general.Studio Sound on new recordings") != "false")
            {
                startInfo.ArgumentList.Add("-filter:a:0");
                startInfo.ArgumentList.Add("afftdn");
            }
        }
        else if (audioInputs.Count > 1)
        {
            var audioLabels = new List<string>();
            foreach (var (inputIndex, isMicrophone) in audioInputs)
            {
                var label = $"audio{audioLabels.Count}";
                if (isMicrophone && AppPreferencesStore.Get("general.Studio Sound on new recordings") != "false")
                    filterGraph.Add($"[{inputIndex}:a:0]afftdn[{label}]");
                else
                    label = $"{inputIndex}:a:0";
                audioLabels.Add($"[{label}]");
            }
            filterGraph.Add($"{string.Concat(audioLabels)}amix=inputs={audioInputs.Count}:duration=longest:dropout_transition=2[audio]");
            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("[audio]");
            startInfo.ArgumentList.Add("-c:a");
            startInfo.ArgumentList.Add("aac");
        }

        if (filterGraph.Count > 0)
        {
            startInfo.ArgumentList.Add("-filter_complex");
            startInfo.ArgumentList.Add(string.Join(';', filterGraph));
        }
    }

    private static void AddCameraInput(ProcessStartInfo startInfo, CaptureDevice camera, int frameRate)
    {
        var id = camera.DeviceId ?? camera.Name;
        if (OperatingSystem.IsWindows())
        {
            AddInputOption(startInfo, "-f", "dshow");
            AddInputOption(startInfo, "-rtbufsize", "256M");
            AddInputOption(startInfo, "-framerate", Math.Clamp(frameRate, 1, 30).ToString());
            AddInputOption(startInfo, "-i", "video=" + id);
        }
        else
        {
            AddInputOption(startInfo, "-f", "v4l2");
            AddInputOption(startInfo, "-framerate", Math.Clamp(frameRate, 1, 30).ToString());
            AddInputOption(startInfo, "-i", id);
        }
    }

    private static void AddAudioInput(ProcessStartInfo startInfo, CaptureDevice audio)
    {
        var id = audio.DeviceId ?? audio.Name;
        if (OperatingSystem.IsWindows())
        {
            AddInputOption(startInfo, "-f", "dshow");
            AddInputOption(startInfo, "-i", "audio=" + id);
        }
        else
        {
            AddInputOption(startInfo, "-f", "pulse");
            AddInputOption(startInfo, "-i", id);
        }
    }

    private static void AddInputOption(ProcessStartInfo startInfo, string option, string value)
    {
        startInfo.ArgumentList.Add(option);
        startInfo.ArgumentList.Add(value);
    }

    private static void AddAvFoundationInput(ProcessStartInfo startInfo, string device, int frameRate,
        bool captureCursor, string? videoSize = null)
    {
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("avfoundation");
        if (!device.StartsWith(':'))
        {
            startInfo.ArgumentList.Add("-framerate");
            startInfo.ArgumentList.Add(Math.Clamp(frameRate, 1, 120).ToString());
        }
        if (captureCursor)
        {
            startInfo.ArgumentList.Add("-capture_cursor");
            startInfo.ArgumentList.Add("1");
        }
        if (!string.IsNullOrWhiteSpace(videoSize))
        {
            startInfo.ArgumentList.Add("-video_size");
            startInfo.ArgumentList.Add(videoSize);
        }
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(device);
    }

    private static async Task<int> FindMacScreenIndexAsync(string ffmpeg)
    {
        var devices = await ListMacCaptureDevicesAsync();
        var screen = devices.FirstOrDefault(device => device.Kind == "Screen");
        if (screen is not null)
            return screen.Index;

        throw new InvalidOperationException("FFmpeg could not find a macOS display capture device.");
    }

    private static async Task<string> ReadMacDeviceListAsync(string ffmpeg)
    {
        var startInfo = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("avfoundation");
        startInfo.ArgumentList.Add("-list_devices");
        startInfo.ArgumentList.Add("true");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(string.Empty);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not inspect macOS capture devices.");
        var errorTask = process.StandardError.ReadToEndAsync();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (await errorTask) + Environment.NewLine + (await outputTask);
    }

    private static string FindFfmpeg()
    {
        var executable = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, executable),
            Path.Combine(AppContext.BaseDirectory, "Helpers", executable)
        };
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        candidates.AddRange(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, executable)));
        if (OperatingSystem.IsMacOS())
        {
            candidates.Add("/opt/homebrew/bin/ffmpeg");
            candidates.Add("/usr/local/bin/ffmpeg");
        }

        var match = candidates.FirstOrDefault(File.Exists);
        return match ?? throw new FileNotFoundException("FFmpeg is required for screen capture. Install FFmpeg and restart Cap Avalonia.");
    }

    private static string FindFfprobe() => FindCompanion("ffprobe");
    private static string FindFfplay() => FindCompanion("ffplay");

    private static string FindCompanion(string name)
    {
        var executable = OperatingSystem.IsWindows() ? name + ".exe" : name;
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, executable),
            Path.Combine(AppContext.BaseDirectory, "Helpers", executable)
        };
        candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, executable)));
        if (OperatingSystem.IsMacOS())
        {
            candidates.Add(Path.Combine("/opt/homebrew/bin", executable));
            candidates.Add(Path.Combine("/usr/local/bin", executable));
        }
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException($"{name} is required for this feature. Install FFmpeg and restart Cap Avalonia.");
    }

    private static string? FindOnPath(string name) => (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Select(directory => Path.Combine(directory, name))
        .FirstOrDefault(File.Exists);

    private static (int Width, int Height, int X, int Y) GetX11PrimaryMode()
    {
        var path = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, "xrandr"))
            .FirstOrDefault(File.Exists);
        if (path is null)
            throw new FileNotFoundException("xrandr is required to detect the X11 display size.");
        var startInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("--current");
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not inspect the X11 display size.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var output = outputTask.GetAwaiter().GetResult();
        _ = errorTask.GetAwaiter().GetResult();
        var lines = output.Split('\n').Where(line => line.Contains(" connected", StringComparison.Ordinal)).ToArray();
        var selected = lines.FirstOrDefault(line => line.Contains(" primary", StringComparison.Ordinal)) ?? lines.FirstOrDefault();
        var match = selected is null ? Match.Empty : Regex.Match(selected, @"(?<width>\d+)x(?<height>\d+)(?<x>[+-]\d+)(?<y>[+-]\d+)");
        if (match.Success && int.TryParse(match.Groups["width"].Value, out var width) &&
            int.TryParse(match.Groups["height"].Value, out var height) &&
            int.TryParse(match.Groups["x"].Value, out var x) &&
            int.TryParse(match.Groups["y"].Value, out var y))
            return (width, height, x, y);
        throw new InvalidOperationException("Could not determine the active X11 display resolution.");
    }

    private static async Task RunAndCheckAsync(string executable, IEnumerable<string> arguments, TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        await RunAndCheckAsync(startInfo, timeout);
    }

    private static async Task RunAndCheckAsync(ProcessStartInfo startInfo, TimeSpan timeout)
    {
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The capture process did not start.");
        var errorTask = process.StandardError.ReadToEndAsync();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var exitTask = process.WaitForExitAsync();
        if (await Task.WhenAny(exitTask, Task.Delay(timeout)) != exitTask)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Screen capture did not finish in time.");
        }

        await exitTask;
        var error = (await errorTask) + Environment.NewLine + (await outputTask);
        if (process.ExitCode != 0)
            throw new InvalidOperationException(UsefulError(error, "Screen capture failed."));
    }

    private static string UniqueDestination(string directory, string name)
    {
        var safeName = Path.GetFileName(name);
        var destination = Path.Combine(directory, safeName);
        if (!File.Exists(destination))
            return destination;

        var stem = Path.GetFileNameWithoutExtension(safeName);
        var extension = Path.GetExtension(safeName);
        var suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var candidate = Path.Combine(directory, $"{stem}_{suffix}{extension}");
        for (var duplicate = 1; File.Exists(candidate); duplicate++)
            candidate = Path.Combine(directory, $"{stem}_{suffix}_{duplicate}{extension}");
        return candidate;
    }

    private static void EnsureCaptureFile(string path, string mediaType)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            throw new InvalidOperationException($"The capture command finished without producing a {mediaType} file.");
    }

    private static string UsefulError(string output, string fallback)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        var errorLine = lines.LastOrDefault(value =>
            value.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("cannot", StringComparison.OrdinalIgnoreCase));
        var usefulLine = errorLine ?? lines.LastOrDefault(value =>
            !value.StartsWith("[in#", StringComparison.Ordinal) &&
            !value.StartsWith("[out#", StringComparison.Ordinal) &&
            !value.Contains("NSKVONotifying_AVCaptureScreenInput", StringComparison.Ordinal) &&
            !Regex.IsMatch(value, @"^\[[^]]+\]\s+[a-z0-9_]+$", RegexOptions.IgnoreCase));
        return usefulLine ?? fallback;
    }
}

internal sealed class ActiveRecordingSession
{
    private readonly Process _process;
    private readonly StringBuilder _errors;
    private bool _stopped;
    private bool _paused;

    public ActiveRecordingSession(Process process, string outputPath, StringBuilder errors)
    {
        _process = process;
        OutputPath = outputPath;
        _errors = errors;
    }

    public string OutputPath { get; }
    public bool HasExited => _process.HasExited;

    public bool TogglePaused()
    {
        if (_stopped || _process.HasExited)
            throw new InvalidOperationException("The recording is no longer active.");
        SetPaused(!_paused);
        _paused = !_paused;
        return _paused;
    }

    private void SetPaused(bool paused)
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Pause and resume shortcuts currently require macOS or Linux.");

        var signal = OperatingSystem.IsMacOS()
            ? (paused ? 17 : 19) // SIGSTOP / SIGCONT
            : (paused ? 19 : 18);
        var result = OperatingSystem.IsMacOS()
            ? UnixProcessSignals.KillMac(_process.Id, signal)
            : UnixProcessSignals.KillLinux(_process.Id, signal);
        if (result != 0)
            throw new InvalidOperationException("Could not pause or resume FFmpeg (error " + Marshal.GetLastPInvokeError() + ").");
    }

    public void StopForShutdown()
    {
        if (_stopped)
            return;
        try
        {
            if (!_process.HasExited)
            {
                if (_paused)
                {
                    SetPaused(false);
                    _paused = false;
                }
                _stopped = true;
                RequestGracefulStop();
                if (!_process.WaitForExit(8000))
                    _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            try
            {
                if (!_process.HasExited)
                    _process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        }
        finally
        {
            _stopped = true;
            _process.Dispose();
        }
    }

    public async Task<string> StopAsync()
    {
        if (_stopped)
            return OutputPath;

        var gracefulSignalSent = false;
        if (!_process.HasExited)
        {
            if (_paused)
            {
                SetPaused(false);
                _paused = false;
            }
            _stopped = true;
            gracefulSignalSent = RequestGracefulStop();
            var exitTask = _process.WaitForExitAsync();
            if (await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(12))) != exitTask)
            {
                try { _process.Kill(entireProcessTree: true); } catch { }
                await _process.WaitForExitAsync();
                _process.WaitForExit();
                _process.Dispose();
                throw new TimeoutException($"FFmpeg did not stop recording in time. A partial file may remain at {OutputPath}.");
            }
            await exitTask;
        }

        _stopped = true;
        await _process.WaitForExitAsync();
        _process.WaitForExit(); // Complete asynchronous stderr delivery before reading process errors.
        var code = _process.ExitCode;
        _process.Dispose();

        if (code != 0 && !(gracefulSignalSent && code == 255))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(_errors.ToString())
                ? "The recording stopped with an error."
                : _errors.ToString().Trim());
        if (!File.Exists(OutputPath) || new FileInfo(OutputPath).Length == 0)
            throw new InvalidOperationException("The recording finished without producing a video file.");
        try
        {
            var duration = await MediaLibraryService.GetVideoDurationAsync(OutputPath);
            if (duration < 0.05)
                throw new InvalidOperationException("The recorded video is too short to play.");
        }
        catch (Exception error)
        {
            throw new InvalidOperationException("The recording finished, but its MP4 could not be read: " + error.Message, error);
        }
        return OutputPath;
    }

    private bool RequestGracefulStop()
    {
        if (OperatingSystem.IsMacOS() && UnixProcessSignals.KillMac(_process.Id, 2) == 0)
            return true;
        if (OperatingSystem.IsLinux() && UnixProcessSignals.KillLinux(_process.Id, 2) == 0)
            return true;
        _process.StandardInput.WriteLine("q");
        return false;
    }
}

internal static class UnixProcessSignals
{
    [DllImport("libSystem.B.dylib", EntryPoint = "kill", SetLastError = true)]
    internal static extern int KillMac(int processId, int signal);

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    internal static extern int KillLinux(int processId, int signal);
}
