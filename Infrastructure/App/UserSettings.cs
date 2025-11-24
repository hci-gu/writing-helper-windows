using System;
using System.IO;
using System.Text.Json;

namespace GlobalTextHelper.Infrastructure.App;

internal sealed class UserSettings
{
    private const string AppDirectoryName = "GlobalTextHelper";
    private const string SettingsFileName = "settings.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string? OpenAiApiKey { get; set; }

    public string? PromptPreamble { get; set; }

    public static UserSettings Load()
    {
        try
        {
            var path = GetSettingsPath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch (Exception)
        {
            // Ignore corrupt settings and fall back to defaults.
        }

        return new UserSettings();
    }

    public void Save()
    {
        var path = GetSettingsPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static string GetSettingsPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string directory = Path.Combine(appData, AppDirectoryName);
        return Path.Combine(directory, SettingsFileName);
    }
}
