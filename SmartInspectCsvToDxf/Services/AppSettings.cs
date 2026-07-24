using System.Text.Json;

namespace SmartInspectCsvToDxf.Services;

public sealed class AppSettings
{
    public string InputFolder { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public string UsbFolder { get; set; } = string.Empty;
    public bool MirrorAboutYAxis { get; set; }

    private static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SmartInspectCsvToDxf");

    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    // Mirrors Load()'s resilience: a locked-down or redirected AppData folder (permissions,
    // Group Policy, AV/EDR, an unsynced roaming profile) can make this throw. Returning false
    // instead of letting it propagate means callers can surface a clear "didn't save" message
    // instead of the write silently failing and the stale settings.json persisting unnoticed.
    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
