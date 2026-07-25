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
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "SmartInspectCsvToDxf is already running. Close the other window before opening a new one.",
                "Already running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
