using SmartInspectCsvToDxf.Models;
using SmartInspectCsvToDxf.Services;
using SmartInspectCsvToDxf.Services.Updates;
using SmartInspectCsvToDxf.UI;

namespace SmartInspectCsvToDxf;

public sealed partial class MainForm : Form
{
    private const int MinFileListWidth = 250;
    private const int FileListWidthPadding = 50;
    private const int MinPreviewWidth = 400;

    private readonly AppSettings _settings;
    private readonly UpdateService _updateService;
    private readonly List<FileSystemWatcher> _watchers = [];
    private string? _currentReportPath;
    private string _savedInputFolder = string.Empty;
    private List<Feature> _currentFeatures = [];

    public MainForm()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        ApplyDefaultFoldersIfMissing();
        _updateService = new UpdateService(new UpdateDiagnosticLog());

        _mirrorCheckBox.Checked = _settings.MirrorAboutYAxis;
        _inputFolderTextBox.Text = _settings.InputFolder;
        _outputFolderTextBox.Text = _settings.OutputFolder;
        _usbFolderTextBox.Text = _settings.UsbFolder;
        _savedInputFolder = _settings.InputFolder;

        if (Directory.Exists(_settings.InputFolder))
        {
            LoadReportFileList(_settings.InputFolder);
            StartWatching(_settings.InputFolder);
        }

        Shown += MainForm_Shown;
    }

    // First-run convenience: SmartInspect itself creates this folder layout under the
    // current user's Documents, so defaulting to it (and creating it if a fresh install
    // hasn't yet) saves having to browse manually before the app is usable.
    private void ApplyDefaultFoldersIfMissing()
    {
        var changed = false;
        var baseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "CAM2 SmartInspect 1.2");

        if (string.IsNullOrWhiteSpace(_settings.InputFolder))
        {
            _settings.InputFolder = Path.Combine(baseFolder, "My Reports");
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(_settings.OutputFolder))
        {
            _settings.OutputFolder = Path.Combine(baseFolder, "My Exports");
            changed = true;
        }

        if (!changed)
            return;

        TryCreateDirectory(_settings.InputFolder);
        TryCreateDirectory(_settings.OutputFolder);
        _settings.Save();
    }

    private static void TryCreateDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
        }
        catch
        {
            // Best-effort default — if creation fails (permissions, etc.), the existing
            // Directory.Exists checks elsewhere surface "folder not found" instead of crashing startup.
        }
    }

    private async void MainForm_Shown(object? sender, EventArgs e)
    {
        if (UpdateService.ShouldSkipAutomaticStartupCheck)
            return;

        // Quiet on purpose: no dialog for "up to date" or for failures (offline, GitHub
        // unreachable, etc.) during the automatic startup check - only an available update
        // is worth interrupting the user for. Failures still land in update.log.
        var result = await _updateService.CheckForUpdatesAsync();
        if (result is { Status: UpdateCheckStatus.UpdateAvailable, UpdateInfo: not null })
            ShowUpdateAvailableDialog(result);
    }

    private async void CheckForUpdatesMenuItem_Click(object? sender, EventArgs e)
    {
        _checkForUpdatesMenuItem.Enabled = false;
        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable when result.UpdateInfo is not null:
                    ShowUpdateAvailableDialog(result);
                    break;
                case UpdateCheckStatus.UpToDate:
                    MessageBox.Show(
                        this,
                        $"You're running the latest version (v{result.CurrentVersion}).",
                        "No updates available",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    break;
                default:
                    MessageBox.Show(
                        this,
                        result.ErrorMessage ?? "The update check could not be completed.",
                        "Update check failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    break;
            }
        }
        finally
        {
            _checkForUpdatesMenuItem.Enabled = true;
        }
    }

    private void AboutMenuItem_Click(object? sender, EventArgs e)
    {
        using var dialog = new AboutDialog(_updateService.GetCurrentVersionText());
        dialog.ShowDialog(this);
    }

    private void ShowUpdateAvailableDialog(UpdateCheckResult result)
    {
        var view = new UpdateInfoView(result.CurrentVersion, result.AvailableVersion!, result.UpdateInfo!);
        using var dialog = new UpdateAvailableDialog(_updateService, view);
        dialog.ShowDialog(this);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveSettings();
        DisposeWatchers();
        base.OnFormClosing(e);
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
        RefreshReportFileListPreserveSelection();
        ReloadCurrentReportIfStillPresent();
    }

    private void FileListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        LoadSelectedReport();
        UpdateExportButtons();
    }

    private void RefreshButton_Click(object? sender, EventArgs e) => RefreshReportFileListPreserveSelection();

    private void MirrorCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        RefreshPreview();
        SaveSettings();
    }

    private void ShowTextCheckBox_CheckedChanged(object? sender, EventArgs e) => RefreshPreview();

    private void ExportUsbButton_Click(object? sender, EventArgs e)
    {
        var folder = ResolveUsbFolder();
        if (folder is null)
            return;

        ExportDxfToConfiguredFolder(folder, "USB");
    }

    // USB drive letters aren't stable between insertions, so a saved UsbFolder path can go
    // stale as soon as the drive is unplugged. If the configured folder isn't currently
    // reachable, fall back to auto-detecting a single removable drive rather than making the
    // user re-browse every time; ambiguous cases (none or several plugged in) still require
    // a manual choice via Browse.
    private string? ResolveUsbFolder()
    {
        var configured = _usbFolderTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
            return configured;

        var removableDrives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
            .ToList();

        if (removableDrives.Count == 1)
        {
            var detected = removableDrives[0].RootDirectory.FullName;
            _usbFolderTextBox.Text = detected;
            SaveSettings();
            return detected;
        }

        var message = removableDrives.Count == 0
            ? "No USB drive detected. Plug one in, or choose a folder manually with Browse."
            : "Multiple USB drives detected. Please choose the correct one manually with Browse.";

        MessageBox.Show(this, message, "USB drive not detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return null;
    }

    private void ExportButton_Click(object? sender, EventArgs e) => ExportDxfToConfiguredFolder(_outputFolderTextBox.Text, "DXF output");

    private void FolderTextBox_Leave(object? sender, EventArgs e) => SaveFolderSettingsFromTextBoxes();

    private void FolderTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter)
            return;

        e.SuppressKeyPress = true;
        SaveFolderSettingsFromTextBoxes();
    }

    private void BrowseInputButton_Click(object? sender, EventArgs e)
    {
        var selected = BrowseForFolder("Select folder containing SmartInspect report files", _inputFolderTextBox.Text);
        if (selected is null)
            return;

        _inputFolderTextBox.Text = selected;

        if (string.IsNullOrWhiteSpace(_outputFolderTextBox.Text))
            _outputFolderTextBox.Text = selected;

        SaveSettings();
        LoadReportFileList(selected);
        StartWatching(selected);
    }

    private void BrowseOutputButton_Click(object? sender, EventArgs e)
    {
        var selected = BrowseForFolder("Select default DXF output folder", _outputFolderTextBox.Text);
        if (selected is null)
            return;

        _outputFolderTextBox.Text = selected;
        SaveSettings();
    }

    private void BrowseUsbButton_Click(object? sender, EventArgs e)
    {
        var selected = BrowseForFolder("Select USB/export folder", _usbFolderTextBox.Text);
        if (selected is null)
            return;

        _usbFolderTextBox.Text = selected;
        SaveSettings();
    }

    private static string? BrowseForFolder(string description, string initialPath)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(initialPath) ? initialPath : string.Empty
        };

        return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
    }

    private void SaveSettings()
    {
        _settings.InputFolder = _inputFolderTextBox.Text.Trim();
        _settings.OutputFolder = _outputFolderTextBox.Text.Trim();
        _settings.UsbFolder = _usbFolderTextBox.Text.Trim();
        _settings.MirrorAboutYAxis = _mirrorCheckBox.Checked;

        if (!_settings.Save())
        {
            const string message = "Could not save settings — check permissions on %APPDATA%\\SmartInspectCsvToDxf";
            _statusLabel.Text = message;
            MessageBox.Show(this, message, "Settings not saved", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        _savedInputFolder = _settings.InputFolder;
    }

    private void SaveFolderSettingsFromTextBoxes()
    {
        var previousInputFolder = _savedInputFolder;
        SaveSettings();

        _inputFolderTextBox.Text = _settings.InputFolder;
        _outputFolderTextBox.Text = _settings.OutputFolder;
        _usbFolderTextBox.Text = _settings.UsbFolder;

        if (string.Equals(previousInputFolder, _settings.InputFolder, StringComparison.OrdinalIgnoreCase))
            return;

        LoadReportFileList(_settings.InputFolder);
        StartWatching(_settings.InputFolder);
    }

    private void StartWatching(string folder)
    {
        DisposeWatchers();

        if (!Directory.Exists(folder))
            return;

        foreach (var pattern in ReportFileReader.FilePatterns)
            _watchers.Add(CreateWatcher(folder, pattern));
    }

    private FileSystemWatcher CreateWatcher(string folder, string pattern)
    {
        var watcher = new FileSystemWatcher(folder, pattern)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        watcher.Created += (_, _) => QueueFileRefresh();
        watcher.Deleted += (_, _) => QueueFileRefresh();
        watcher.Renamed += (_, _) => QueueFileRefresh();
        watcher.Changed += (_, _) => QueueFileRefresh();

        return watcher;
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in _watchers)
            watcher.Dispose();
        _watchers.Clear();
    }

    private void QueueFileRefresh()
    {
        if (IsDisposed)
            return;

        BeginInvoke(new Action(() =>
        {
            _refreshTimer.Stop();
            _refreshTimer.Start();
        }));
    }

    private void LoadReportFileList(string folder, string? preferredSelection = null)
    {
        _fileListBox.BeginUpdate();
        try
        {
            _fileListBox.Items.Clear();

            if (!Directory.Exists(folder))
            {
                _statusLabel.Text = "Report folder not found";
                return;
            }

            var files = ReportFileReader.FilePatterns
                .SelectMany(pattern => Directory.GetFiles(folder, pattern))
                .OrderByDescending(File.GetLastWriteTime)
                .ThenBy(Path.GetFileName)
                .ToList();

            foreach (var file in files)
                _fileListBox.Items.Add(new ReportFileItem(file));

            if (preferredSelection is not null)
                SelectReportFile(preferredSelection);

            _statusLabel.Text = files.Count == 1 ? "1 report file found" : $"{files.Count} report files found";
        }
        finally
        {
            _fileListBox.EndUpdate();
        }

        AutoSizeFileList();
        UpdateExportButtons();
    }

    private void AutoSizeFileList()
    {
        var desiredWidth = MinFileListWidth;

        if (_fileListBox.Items.Count > 0)
        {
            var maxTextWidth = 0;
            using (var g = _fileListBox.CreateGraphics())
            {
                foreach (var item in _fileListBox.Items)
                {
                    var size = TextRenderer.MeasureText(g, item?.ToString() ?? string.Empty, _fileListBox.Font);
                    if (size.Width > maxTextWidth)
                        maxTextWidth = size.Width;
                }
            }

            var maxAllowedWidth = Math.Max(MinFileListWidth, _splitContainer.ClientSize.Width - MinPreviewWidth);
            desiredWidth = Math.Clamp(maxTextWidth + FileListWidthPadding, MinFileListWidth, maxAllowedWidth);
        }

        // Clamp against the SplitContainer's own valid range so this never throws if the
        // control hasn't finished its initial layout yet (e.g. called during construction).
        var lowerBound = _splitContainer.Panel1MinSize;
        var upperBound = _splitContainer.Width - _splitContainer.Panel2MinSize - _splitContainer.SplitterWidth;
        if (upperBound < lowerBound)
            return;

        _splitContainer.SplitterDistance = Math.Clamp(desiredWidth, lowerBound, upperBound);
    }

    private void RefreshReportFileListPreserveSelection()
    {
        var previous = _currentReportPath;
        LoadReportFileList(_inputFolderTextBox.Text, previous);
    }

    private void SelectReportFile(string fullPath)
    {
        for (var i = 0; i < _fileListBox.Items.Count; i++)
        {
            if (_fileListBox.Items[i] is ReportFileItem item && string.Equals(item.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                _fileListBox.SelectedIndex = i;
                return;
            }
        }
    }

    private void LoadSelectedReport()
    {
        if (_fileListBox.SelectedItem is not ReportFileItem item)
            return;

        LoadReport(item.FullPath, showErrors: true);

        // Move focus to the preview so the arrow keys immediately drive the drawing-
        // plane override, without requiring an extra click. Only on this explicit,
        // user-driven selection - not on the file-watcher's automatic reloads (see
        // ReloadCurrentReportIfStillPresent), where stealing focus would be disruptive.
        _previewPanel.Focus();
    }

    private void ReloadCurrentReportIfStillPresent()
    {
        if (_currentReportPath is null || !File.Exists(_currentReportPath))
            return;

        LoadReport(_currentReportPath, showErrors: false);
    }

    private void LoadReport(string path, bool showErrors)
    {
        try
        {
            _currentReportPath = path;
            _currentFeatures = ReportFileReader.Read(path);
            RefreshPreview();
            UpdateExportButtons();
            _statusLabel.Text = $"Loaded {Path.GetFileName(path)} — {_currentFeatures.Count} features";
        }
        catch (IOException)
        {
            // SmartInspect or another process may still be writing the file.
            QueueFileRefresh();
        }
        catch (Exception ex)
        {
            _currentReportPath = null;
            _currentFeatures = [];
            _previewPanel.SetFeatures([], _mirrorCheckBox.Checked, _showTextCheckBox.Checked);
            UpdateExportButtons();
            _statusLabel.Text = "Failed to load report";
            if (showErrors)
                MessageBox.Show(this, ex.Message, "Report load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshPreview()
    {
        _previewPanel.SetFeatures(_currentFeatures, _mirrorCheckBox.Checked, _showTextCheckBox.Checked);
    }

    private void UpdateExportButtons()
    {
        var count = _fileListBox.SelectedItems.Count;
        _exportButton.Enabled = count > 0;
        _exportUsbButton.Enabled = count > 0;
        _exportButton.Text = count > 1 ? $"Export DXF ({count})" : "Export DXF";
        _exportUsbButton.Text = count > 1 ? $"Write to USB ({count})" : "Write to USB";
    }

    private void ExportDxfToConfiguredFolder(string folder, string targetName)
    {
        var items = _fileListBox.SelectedItems.Cast<ReportFileItem>().ToList();
        if (items.Count == 0)
            return;

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            MessageBox.Show(this, $"Please configure a valid {targetName} folder first.", "Folder not configured", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var exported = new List<string>();
        var failures = new List<(string File, string Error)>();

        foreach (var item in items)
        {
            try
            {
                var features = ReportFileReader.Read(item.FullPath);
                if (features.Count == 0)
                {
                    failures.Add((Path.GetFileName(item.FullPath), "No valid features found"));
                    continue;
                }

                // Only the file currently shown in the preview can have a manual plane
                // override attached to it; every other file in the batch (and this one,
                // if the plane is still auto-detected) gets its own auto-detected plane.
                DrawingPlane? plane = _previewPanel.IsPlaneOverridden
                    && string.Equals(item.FullPath, _currentReportPath, StringComparison.OrdinalIgnoreCase)
                        ? _previewPanel.DrawingPlane
                        : null;

                var outputPath = BuildOutputPath(folder, item.FullPath);
                DxfExporter.Export(outputPath, features, _mirrorCheckBox.Checked, plane);
                exported.Add(Path.GetFileName(outputPath));
            }
            catch (Exception ex)
            {
                failures.Add((Path.GetFileName(item.FullPath), ex.Message));
            }
        }

        _statusLabel.Text = failures.Count == 0
            ? $"Exported {exported.Count} file(s) to {targetName}"
            : $"Exported {exported.Count} of {items.Count} file(s) to {targetName} ({failures.Count} failed)";

        var summary = new System.Text.StringBuilder();
        if (exported.Count > 0)
        {
            summary.AppendLine($"Exported {exported.Count} file(s):");
            summary.AppendLine(string.Join(Environment.NewLine, exported));
        }

        if (failures.Count > 0)
        {
            if (summary.Length > 0)
                summary.AppendLine();

            summary.AppendLine($"Failed ({failures.Count}):");
            foreach (var (file, error) in failures)
                summary.AppendLine($"{file}: {error}");
        }

        MessageBox.Show(
            this,
            summary.ToString().TrimEnd(),
            failures.Count == 0 ? "Export complete" : "Export completed with errors",
            MessageBoxButtons.OK,
            failures.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private string BuildOutputPath(string folder, string reportPath)
    {
        var defaultName = Path.GetFileNameWithoutExtension(reportPath) ?? "features";
        if (_mirrorCheckBox.Checked)
            defaultName += "_mirrored_y";

        var path = Path.Combine(folder, defaultName + ".dxf");
        if (!File.Exists(path))
            return path;

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(folder, $"{defaultName}_{stamp}.dxf");
    }

    private sealed class ReportFileItem
    {
        public ReportFileItem(string fullPath) => FullPath = fullPath;
        public string FullPath { get; }

        public override string ToString()
        {
            var name = Path.GetFileNameWithoutExtension(FullPath);
            var extension = Path.GetExtension(FullPath).TrimStart('.').ToUpperInvariant();
            return extension.Length == 0 ? name : $"{name}  [{extension}]";
        }
    }
}
