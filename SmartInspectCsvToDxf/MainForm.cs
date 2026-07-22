using SmartInspectCsvToDxf.Models;
using SmartInspectCsvToDxf.Services;

namespace SmartInspectCsvToDxf;

public sealed partial class MainForm : Form
{
    private readonly AppSettings _settings;
    private FileSystemWatcher? _watcher;
    private string? _currentCsvPath;
    private string _savedInputFolder = string.Empty;
    private List<Feature> _currentFeatures = [];

    public MainForm()
    {
        InitializeComponent();

        _settings = AppSettings.Load();

        _mirrorCheckBox.Checked = _settings.MirrorAboutYAxis;
        _inputFolderTextBox.Text = _settings.InputFolder;
        _outputFolderTextBox.Text = _settings.OutputFolder;
        _usbFolderTextBox.Text = _settings.UsbFolder;
        _savedInputFolder = _settings.InputFolder;

        if (Directory.Exists(_settings.InputFolder))
        {
            LoadCsvFileList(_settings.InputFolder);
            StartWatching(_settings.InputFolder);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SaveSettings();
        _watcher?.Dispose();
        base.OnFormClosing(e);
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
        RefreshCsvFileListPreserveSelection();
        ReloadCurrentCsvIfStillPresent();
    }

    private void FileListBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        LoadSelectedCsv();
        UpdateExportButtons();
    }

    private void RefreshButton_Click(object? sender, EventArgs e) => RefreshCsvFileListPreserveSelection();

    private void MirrorCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        RefreshPreview();
        SaveSettings();
    }

    private void ShowTextCheckBox_CheckedChanged(object? sender, EventArgs e) => RefreshPreview();

    private void ExportUsbButton_Click(object? sender, EventArgs e) => ExportDxfToConfiguredFolder(_usbFolderTextBox.Text, "USB");

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
        var selected = BrowseForFolder("Select folder containing SmartInspect CSV files", _inputFolderTextBox.Text);
        if (selected is null)
            return;

        _inputFolderTextBox.Text = selected;

        if (string.IsNullOrWhiteSpace(_outputFolderTextBox.Text))
            _outputFolderTextBox.Text = selected;

        SaveSettings();
        LoadCsvFileList(selected);
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
        _settings.Save();
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

        LoadCsvFileList(_settings.InputFolder);
        StartWatching(_settings.InputFolder);
    }

    private void StartWatching(string folder)
    {
        _watcher?.Dispose();
        _watcher = null;

        if (!Directory.Exists(folder))
            return;

        _watcher = new FileSystemWatcher(folder, "*.csv")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        _watcher.Created += (_, _) => QueueFileRefresh();
        _watcher.Deleted += (_, _) => QueueFileRefresh();
        _watcher.Renamed += (_, _) => QueueFileRefresh();
        _watcher.Changed += (_, _) => QueueFileRefresh();
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

    private void LoadCsvFileList(string folder, string? preferredSelection = null)
    {
        _fileListBox.BeginUpdate();
        try
        {
            _fileListBox.Items.Clear();

            if (!Directory.Exists(folder))
            {
                _statusLabel.Text = "CSV folder not found";
                return;
            }

            var files = Directory.GetFiles(folder, "*.csv")
                .OrderByDescending(File.GetLastWriteTime)
                .ThenBy(Path.GetFileName)
                .ToList();

            foreach (var file in files)
                _fileListBox.Items.Add(new CsvFileItem(file));

            if (preferredSelection is not null)
                SelectCsvFile(preferredSelection);

            _statusLabel.Text = files.Count == 1 ? "1 CSV file found" : $"{files.Count} CSV files found";
        }
        finally
        {
            _fileListBox.EndUpdate();
        }

        UpdateExportButtons();
    }

    private void RefreshCsvFileListPreserveSelection()
    {
        var previous = _currentCsvPath;
        LoadCsvFileList(_inputFolderTextBox.Text, previous);
    }

    private void SelectCsvFile(string fullPath)
    {
        for (var i = 0; i < _fileListBox.Items.Count; i++)
        {
            if (_fileListBox.Items[i] is CsvFileItem item && string.Equals(item.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                _fileListBox.SelectedIndex = i;
                return;
            }
        }
    }

    private void LoadSelectedCsv()
    {
        if (_fileListBox.SelectedItem is not CsvFileItem item)
            return;

        LoadCsv(item.FullPath, showErrors: true);
    }

    private void ReloadCurrentCsvIfStillPresent()
    {
        if (_currentCsvPath is null || !File.Exists(_currentCsvPath))
            return;

        LoadCsv(_currentCsvPath, showErrors: false);
    }

    private void LoadCsv(string path, bool showErrors)
    {
        try
        {
            _currentCsvPath = path;
            _currentFeatures = CsvFeatureReader.Read(path);
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
            _currentCsvPath = null;
            _currentFeatures = [];
            _previewPanel.SetFeatures([], _mirrorCheckBox.Checked, _showTextCheckBox.Checked);
            UpdateExportButtons();
            _statusLabel.Text = "Failed to load CSV";
            if (showErrors)
                MessageBox.Show(this, ex.Message, "CSV load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        var items = _fileListBox.SelectedItems.Cast<CsvFileItem>().ToList();
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
                var features = CsvFeatureReader.Read(item.FullPath);
                if (features.Count == 0)
                {
                    failures.Add((Path.GetFileName(item.FullPath), "No valid features found"));
                    continue;
                }

                var outputPath = BuildOutputPath(folder, item.FullPath);
                DxfExporter.Export(outputPath, features, _mirrorCheckBox.Checked);
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

    private string BuildOutputPath(string folder, string csvPath)
    {
        var defaultName = Path.GetFileNameWithoutExtension(csvPath) ?? "features";
        if (_mirrorCheckBox.Checked)
            defaultName += "_mirrored_y";

        var path = Path.Combine(folder, defaultName + ".dxf");
        if (!File.Exists(path))
            return path;

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(folder, $"{defaultName}_{stamp}.dxf");
    }

    private sealed class CsvFileItem
    {
        public CsvFileItem(string fullPath) => FullPath = fullPath;
        public string FullPath { get; }
        public override string ToString() => Path.GetFileName(FullPath);
    }
}
