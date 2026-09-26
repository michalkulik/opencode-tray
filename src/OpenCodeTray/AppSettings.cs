using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenCodeTray;

/// <summary>User settings persisted to %APPDATA%\OpenCodeTray\settings.json.</summary>
public sealed class AppSettings
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("refreshMinutes")]
    public int RefreshMinutes { get; set; } = 5;

    [JsonPropertyName("startWithWindows")]
    public bool StartWithWindows { get; set; }

    [JsonPropertyName("showNotifications")]
    public bool ShowNotifications { get; set; } = true;

    [JsonIgnore]
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "OpenCodeTray");

    [JsonIgnore]
    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (loaded is not null)
                {
                    loaded.RefreshMinutes = Math.Clamp(loaded.RefreshMinutes, 1, 1440);
                    return loaded;
                }
            }
        }
        catch
        {
            // Fall back to defaults on any read/parse error.
        }

        var settings = new AppSettings();
        settings.StartWithWindows = StartupManager.IsEnabled();
        return settings;
    }

    public void Save()
    {
        System.IO.Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
    }

    public AppSettings Clone() => new()
    {
        ApiKey = ApiKey,
        RefreshMinutes = RefreshMinutes,
        StartWithWindows = StartWithWindows,
        ShowNotifications = ShowNotifications,
    };
}
