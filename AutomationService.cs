using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CapAvalonia;

internal static class AutomationService
{
    public static async Task<string?> ApplyAfterSaveAsync(string sourcePath, bool screenshot)
    {
        var prefix = screenshot ? "automation.screenshots" : "automation.recordings";
        if (AppPreferencesStore.Get(prefix + ".enabled") != "true")
            return null;

        var destinationDirectory = AppPreferencesStore.Get(prefix + ".folder");
        if (string.IsNullOrWhiteSpace(destinationDirectory))
            return "Choose a destination folder for the enabled save automation.";
        Directory.CreateDirectory(destinationDirectory);
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));
        if (string.Equals(source, Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            return null;
        destination = GetUniquePath(destination);
        await using var input = File.OpenRead(source);
        await using var output = File.Create(destination);
        await input.CopyToAsync(output);
        return destination;
    }

    public static async Task<string?> ChooseFolderAsync(Window owner, string title)
    {
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return folders.Count == 0 ? null : folders[0].Path.LocalPath;
    }

    private static string GetUniquePath(string destination)
    {
        if (!File.Exists(destination))
            return destination;
        var folder = Path.GetDirectoryName(destination)!;
        var stem = Path.GetFileNameWithoutExtension(destination);
        var extension = Path.GetExtension(destination);
        var suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var candidate = Path.Combine(folder, $"{stem}_{suffix}{extension}");
        for (var duplicate = 1; File.Exists(candidate); duplicate++)
            candidate = Path.Combine(folder, $"{stem}_{suffix}_{duplicate}{extension}");
        return candidate;
    }
}
