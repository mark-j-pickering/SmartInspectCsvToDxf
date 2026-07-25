using Velopack.Logging;

namespace SmartInspectCsvToDxf.Services.Updates;

// There's no logging framework anywhere else in this project, so this is a minimal,
// dependency-free file logger - mirrors AppSettings' "best effort, never throw" approach.
// Doubles as the sink for Velopack's own internal diagnostics (passed to
// VelopackApp.Build().SetLogger(...) in Program.cs) and for UpdateService's messages.
public sealed class UpdateDiagnosticLog : IVelopackLogger
{
    private static readonly object WriteLock = new();

    private static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SmartInspectCsvToDxf",
        "update.log");

    public void Log(VelopackLogLevel logLevel, string? message, Exception? exception)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel}] {message}";
        if (exception is not null)
            line += Environment.NewLine + exception;

        Write(line);
    }

    public void Info(string message) => Log(VelopackLogLevel.Information, message, null);

    public void Error(string message, Exception? exception = null) => Log(VelopackLogLevel.Error, message, exception);

    private static void Write(string line)
    {
        try
        {
            lock (WriteLock)
            {
                var path = LogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // Diagnostic logging must never be the reason the app crashes.
        }
    }
}
