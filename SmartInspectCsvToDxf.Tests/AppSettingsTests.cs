using SmartInspectCsvToDxf.Services;

namespace SmartInspectCsvToDxf.Tests;

// AppSettings has no injectable path - it always reads/writes the real
// %APPDATA%\SmartInspectCsvToDxf\settings.json, so this backs up whatever is
// already there and restores it afterward rather than clobbering the user's
// actual saved folders.
public sealed class AppSettingsTests : IDisposable
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SmartInspectCsvToDxf",
        "settings.json");

    private readonly string? _backup;

    public AppSettingsTests()
    {
        _backup = File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : null;
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAllFields()
    {
        var settings = new AppSettings
        {
            InputFolder = @"C:\Reports\In",
            OutputFolder = @"C:\Reports\Out",
            UsbFolder = @"D:\Usb",
            MirrorAboutYAxis = true
        };

        Assert.True(settings.Save());

        var loaded = AppSettings.Load();

        Assert.Equal(settings.InputFolder, loaded.InputFolder);
        Assert.Equal(settings.OutputFolder, loaded.OutputFolder);
        Assert.Equal(settings.UsbFolder, loaded.UsbFolder);
        Assert.Equal(settings.MirrorAboutYAxis, loaded.MirrorAboutYAxis);
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        // Save() writes to a ".tmp" sibling and renames it onto settings.json (a single
        // atomic rename, so a concurrent Load() from a second instance of the app can
        // never observe a truncated/partial file) - confirm that temp file doesn't linger.
        var settings = new AppSettings { InputFolder = @"C:\Reports\In" };

        Assert.True(settings.Save());

        Assert.False(File.Exists(SettingsPath + ".tmp"));
        Assert.True(File.Exists(SettingsPath));
    }

    public void Dispose()
    {
        if (_backup is not null)
            File.WriteAllText(SettingsPath, _backup);
        else if (File.Exists(SettingsPath))
            File.Delete(SettingsPath);

        var tempPath = SettingsPath + ".tmp";
        if (File.Exists(tempPath))
            File.Delete(tempPath);
    }
}
