using SmartInspectCsvToDxf.Services.Updates;

namespace SmartInspectCsvToDxf.UI;

// Hand-built rather than designer-generated (no Visual Studio designer pass available here) -
// kept deliberately small: a message, a progress bar that's hidden until download starts, and
// two buttons. MainForm owns the UpdateService call; this dialog only renders state and lets
// the user decide whether to install now or later.
public sealed class UpdateAvailableDialog : Form
{
    private readonly UpdateService _updateService;
    private readonly UpdateInfoView _update;
    private readonly Label _messageLabel;
    private readonly ProgressBar _progressBar;
    private readonly Button _installButton;
    private readonly Button _laterButton;

    public UpdateAvailableDialog(UpdateService updateService, UpdateInfoView update)
    {
        _updateService = updateService;
        _update = update;

        Text = "Update available";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 170);
        Padding = new Padding(16);

        _messageLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 90,
            Text = $"A new version of BUM SmartInspect Converter is available.{Environment.NewLine}{Environment.NewLine}" +
                   $"Current version: {update.CurrentVersion}{Environment.NewLine}" +
                   $"Available version: {update.AvailableVersion}",
        };

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 20,
            Minimum = 0,
            Maximum = 100,
            Visible = false,
        };

        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
        };

        _laterButton = new Button
        {
            Text = "Later",
            DialogResult = DialogResult.Cancel,
            Dock = DockStyle.Right,
            Width = 100,
        };

        _installButton = new Button
        {
            Text = "Install and Restart",
            Dock = DockStyle.Right,
            Width = 160,
        };
        _installButton.Click += InstallButton_Click;

        buttonPanel.Controls.Add(_laterButton);
        buttonPanel.Controls.Add(_installButton);

        Controls.Add(_messageLabel);
        Controls.Add(_progressBar);
        Controls.Add(buttonPanel);

        AcceptButton = _installButton;
        CancelButton = _laterButton;
    }

    private async void InstallButton_Click(object? sender, EventArgs e)
    {
        _installButton.Enabled = false;
        _laterButton.Enabled = false;
        _progressBar.Visible = true;

        try
        {
            await _updateService.DownloadUpdateAsync(_update.UpdateInfo, ReportProgress, CancellationToken.None);
            _updateService.ApplyUpdateAndRestart(_update.UpdateInfo);
            Close();
        }
        catch (Exception ex)
        {
            CenteredMessageBox.Show(
                this,
                $"The update could not be installed: {ex.Message}",
                "Update failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            _installButton.Enabled = true;
            _laterButton.Enabled = true;
            _progressBar.Visible = false;
        }
    }

    private void ReportProgress(int percent)
    {
        if (IsDisposed)
            return;

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ReportProgress(percent)));
            return;
        }

        _progressBar.Value = Math.Clamp(percent, _progressBar.Minimum, _progressBar.Maximum);
    }
}

public sealed record UpdateInfoView(string CurrentVersion, string AvailableVersion, Velopack.UpdateInfo UpdateInfo);
