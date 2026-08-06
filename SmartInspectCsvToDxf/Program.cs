using SmartInspectCsvToDxf.Services.Updates;
using Velopack;

namespace SmartInspectCsvToDxf;

internal static class Program
{
    // Two running copies otherwise both hold their own in-memory settings and each writes its
    // full settings.json on close - whichever one closes last silently overwrites the other's
    // changes (e.g. an output folder set in one window "disappears" after the other closes).
    private const string SingleInstanceMutexName = "SmartInspectCsvToDxf-SingleInstance-9F3E6B2A-4C1D-4E7A-9A5B-9C7F3E6B2A4C";

    [STAThread]
    private static void Main()
    {
        // Must run before any other startup code: on first launch after an install/update,
        // Velopack briefly re-invokes the exe with internal hook arguments (create/remove
        // shortcuts, etc.) and expects to exit immediately afterwards. Handing control to the
        // mutex/WinForms pipeline first would show our UI (or block on the single-instance
        // mutex) during that hook invocation.
        VelopackApp.Build().SetLogger(new UpdateDiagnosticLog()).Run();

        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "The VAG Inspector is already running. Close the other window before opening a new one.",
                "Already running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
