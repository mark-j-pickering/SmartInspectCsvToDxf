namespace SmartInspectCsvToDxf.UI;

public sealed class ProgressDialog : Form
{
    private readonly Label _label;
    private readonly ProgressBar _progressBar;

    public ProgressDialog(string title, int total)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ControlBox = false;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(360, 90);
        Padding = new Padding(16);

        _label = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 24,
            Minimum = 0,
            Maximum = Math.Max(total, 1),
            Style = ProgressBarStyle.Continuous,
        };

        Controls.Add(_progressBar);
        Controls.Add(_label);
    }

    public void ReportProgress(int current, int total, string fileName)
    {
        _progressBar.Maximum = Math.Max(total, 1);
        _progressBar.Value = Math.Clamp(current, _progressBar.Minimum, _progressBar.Maximum);
        _label.Text = $"Processing file {current} of {total}: {fileName}";
    }
}
