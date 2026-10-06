using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CapAvalonia;

internal static class AppPreferencesStore
{
    private static readonly object Gate = new();
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CapAvalonia",
        "settings.json");
    private static Dictionary<string, string> _values = Load();

    public static string? Get(string key)
    {
        lock (Gate)
            return _values.TryGetValue(key, out var value) ? value : null;
    }

    public static void Set(string key, string value)
    {
        lock (Gate)
        {
            _values[key] = value;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
    }

    public static void Remove(string key)
    {
        lock (Gate)
        {
            if (!_values.Remove(key))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporaryPath = FilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
    }

    private static Dictionary<string, string> Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath)) ?? new()
                : new();
        }
        catch
        {
            return new();
        }
    }
}
