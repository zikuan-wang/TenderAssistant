using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace TenderAssistant.Client.Services;

public enum ClientApplicationTheme
{
    Light,
    Dark,
    Pink
}

public static class AppThemeService
{
    private static readonly object SyncRoot = new();
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TenderAssistant");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "client-settings.json");

    public static ClientApplicationTheme CurrentTheme { get; private set; } = ClientApplicationTheme.Light;

    public static void Initialize()
    {
        CurrentTheme = LoadTheme();
        Apply(CurrentTheme);
    }

    public static void SetTheme(ClientApplicationTheme theme)
    {
        if (!IsSupportedTheme(theme))
        {
            theme = ClientApplicationTheme.Light;
        }

        lock (SyncRoot)
        {
            CurrentTheme = theme;
            SaveTheme(theme);
            Apply(theme);
        }
    }

    private static ClientApplicationTheme LoadTheme()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return ClientApplicationTheme.Light;
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<ClientSettings>(json);
            return Enum.TryParse<ClientApplicationTheme>(settings?.Theme, ignoreCase: true, out var theme) && IsSupportedTheme(theme)
                ? theme
                : ClientApplicationTheme.Light;
        }
        catch
        {
            return ClientApplicationTheme.Light;
        }
    }

    private static void SaveTheme(ClientApplicationTheme theme)
    {
        Directory.CreateDirectory(SettingsDirectory);
        JsonObject settings;
        try
        {
            settings = File.Exists(SettingsPath)
                ? JsonNode.Parse(File.ReadAllText(SettingsPath)) as JsonObject ?? []
                : [];
        }
        catch
        {
            settings = [];
        }

        settings["Theme"] = theme.ToString();
        File.WriteAllText(SettingsPath, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Apply(ClientApplicationTheme theme)
    {
        var paletteHelper = new PaletteHelper();
        var materialTheme = paletteHelper.GetTheme();
        materialTheme.SetBaseTheme(theme is ClientApplicationTheme.Dark ? BaseTheme.Dark : BaseTheme.Light);
        materialTheme.SetPrimaryColor(theme is ClientApplicationTheme.Pink ? Color.FromRgb(214, 51, 108) : Color.FromRgb(30, 79, 138));
        materialTheme.SetSecondaryColor(theme is ClientApplicationTheme.Pink ? Color.FromRgb(244, 114, 182) : Color.FromRgb(0, 137, 123));
        paletteHelper.SetTheme(materialTheme);

        ApplyApplicationBrushes(theme);
    }

    private static bool IsSupportedTheme(ClientApplicationTheme theme)
    {
        return theme is ClientApplicationTheme.Light or ClientApplicationTheme.Dark or ClientApplicationTheme.Pink;
    }

    private static void ApplyApplicationBrushes(ClientApplicationTheme theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        var resources = Application.Current.Resources;
        var isDark = theme is ClientApplicationTheme.Dark;
        var isPink = theme is ClientApplicationTheme.Pink;

        resources["TenderPrimaryBrush"] = Brush(isPink ? "#D6336C" : "#1E4F8A");
        resources["TenderAccentBrush"] = Brush(isPink ? "#F472B6" : "#00897B");
        resources["TenderDangerBrush"] = Brush("#C62828");
        resources["TenderAppBackgroundBrush"] = Brush(isDark ? "#101418" : isPink ? "#FFF5F8" : "#F5F7FA");
        resources["TenderSurfaceBrush"] = Brush(isDark ? "#1B2128" : "#FFFFFF");
        resources["TenderSurfaceAltBrush"] = Brush(isDark ? "#232B34" : isPink ? "#FFF0F5" : "#F6F8FB");
        resources["TenderStrokeBrush"] = Brush(isDark ? "#34404C" : isPink ? "#F5B7CF" : "#D8E0EA");
        resources["TenderTextBrush"] = Brush(isDark ? "#EEF2F7" : "#1F2937");
        resources["TenderSubtleTextBrush"] = Brush(isDark ? "#AAB4C0" : isPink ? "#7A4A5B" : "#667085");
        resources["TenderNavigationBrush"] = Brush(isDark ? "#171D24" : "#FFFFFF");
        resources["TenderNavigationSelectedBrush"] = Brush(isDark ? "#26384F" : isPink ? "#FFE3EC" : "#E8F1FF");
        resources["TenderNavigationHoverBrush"] = Brush(isDark ? "#222C37" : isPink ? "#FFF0F5" : "#F2F6FC");
    }

    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private sealed record ClientSettings(string Theme);
}
