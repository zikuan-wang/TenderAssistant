using System.IO;
using System.Text.Json;

namespace TenderAssistant.Client.Services;

public static class ClientAppSettingsService
{
    private static readonly object Gate = new();
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TenderAssistant");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "client-settings.json");

    private static ClientAppSettings? _settings;

    public static string FileLibraryCachePath
    {
        get
        {
            var value = Current.FileLibraryCachePath;
            return string.IsNullOrWhiteSpace(value) ? DefaultFileLibraryCachePath : value;
        }
        set
        {
            Current.FileLibraryCachePath = string.IsNullOrWhiteSpace(value)
                ? DefaultFileLibraryCachePath
                : value.Trim();
            Save();
        }
    }

    public static string SourceFileLibraryPath
    {
        get => Current.SourceFileLibraryPath;
        set
        {
            Current.SourceFileLibraryPath = string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
            Save();
        }
    }

    private static string DefaultFileLibraryCachePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TenderAssistant",
        "FileLibrary");

    private static ClientAppSettings Current
    {
        get
        {
            lock (Gate)
            {
                return _settings ??= Load();
            }
        }
    }

    private static ClientAppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<ClientAppSettings>(File.ReadAllText(SettingsPath));
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
            // Fall back to defaults when the settings file is corrupt or inaccessible.
        }

        return new ClientAppSettings();
    }

    private static void Save()
    {
        lock (Gate)
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private sealed class ClientAppSettings
    {
        public string FileLibraryCachePath { get; set; } = DefaultFileLibraryCachePath;

        public string SourceFileLibraryPath { get; set; } = string.Empty;

        public string? Theme { get; set; }
    }
}
