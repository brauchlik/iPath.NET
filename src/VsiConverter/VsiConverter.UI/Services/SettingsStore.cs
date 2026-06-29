using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace VsiConverter.UI.Services;

public class AppSettings
{
    public int CompressionQuality { get; set; } = 90;
    public string? BfconvertPath { get; set; }
    public string? VipsPath { get; set; }
    public string? JavaPath { get; set; }
}

public static class SettingsStore
{
    private static string GetFilePath()
    {
        var baseDir = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "VsiConverter")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VsiConverter");
        Directory.CreateDirectory(baseDir);
        return Path.Combine(baseDir, "settings.json");
    }

    public static AppSettings Load()
    {
        try
        {
            var path = GetFilePath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SettingsStore.Load failed: {ex.Message}");
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var path = GetFilePath();
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SettingsStore.Save failed: {ex.Message}");
        }
    }
}
