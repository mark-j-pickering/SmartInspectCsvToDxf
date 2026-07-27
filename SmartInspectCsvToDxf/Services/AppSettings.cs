using System.Text.Json;

namespace SmartInspectCsvToDxf.Services;

public sealed class AppSettings
{
    public string InputFolder { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public string UsbFolder { get; set; } = string.Empty;
    public bool MirrorX { get; set; }
    public bool MirrorY { get; set; }
    public int RotationSteps { get; set; }

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

            // Write-then-rename instead of writing SettingsPath directly: File.WriteAllText
            // truncates the destination before writing its new content, so a second process
            // reading the file at exactly the wrong moment (e.g. two copies of the app
            // launched at once) can catch it mid-truncation and silently fall back to blank
            // defaults in Load()'s catch block. File.Move onto an existing path is a single
            // atomic rename - a concurrent reader always sees either the old file or the new
            // one in full, never a partial write.
            var tempPath = SettingsPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, SettingsPath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
