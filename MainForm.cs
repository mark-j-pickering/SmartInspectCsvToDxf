using SmartInspectCsvToDxf.Models;
using SmartInspectCsvToDxf.Services;
using SmartInspectCsvToDxf.UI;

namespace SmartInspectCsvToDxf;

public sealed class MainForm : Form
{
    private readonly TextBox _inputFolderTextBox = new();
    private readonly TextBox _outputFolderTextBox = new();
    private readonly TextBox _usbFolderTextBox = new();
    private readonly Button _browseInputButton = new();
    private readonly Button _browseOutputButton = new();
    private readonly Button _browseUsbButton = new();
    private readonly ListBox _fileListBox = new();
    private readonly PreviewPanel _previewPanel = new();
    private readonly CheckBox _mirrorCheckBox = new();
    private readonly Button _exportButton = new();
    private readonly Button _exportUsbButton = new();
    private readonly Button _refreshButton = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusLabel = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();

    private readonly AppSettings _settings;
    private FileSystemWatcher? _watcher;
    private string? _currentCsvPath;
    private string _savedInputFolder = string.Empty;
    private List<Feature> _currentFeatures = [];

    public MainForm()
    {
        _settings = AppSettings.Load();

        Text = "SmartInspect CSV to DXF";
        Width = 1220;
        Height = 820;
        MinimumSize = new Size(980, 640);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();

        _mirrorCheckBox.Checked = _settings.MirrorAboutYAxis;
        _inputFolderTextBox.Text = _settings.InputFolder;
        _outputFolderTextBox.Text = _settings.OutputFolder;
        _usbFolderTextBox.Text = _settings.UsbFolder;
        _savedInputFolder = _settings.InputFolder;

        _refreshTimer.Interval = 350;
        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            RefreshCsvFileListPreserveSelection();
            ReloadCurrentCsvIfStillPresent();
        };

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

    private void BuildUi()
    {
        var topPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 104,
            Padding = new Padding(8),
            ColumnCount = 3,
            RowCount = 3
        };
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        topPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        topPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        topPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        AddFolderRow(topPanel, 0, "CSV folder:", _inputFolderTextBox, _browseInputButton, "Browse...", () => BrowseForInputFolder());
        AddFolderRow(topPanel, 1, "DXF folder:", _outputFolderTextBox, _browseOutputButton, "Browse...", () => BrowseForOutputFolder());
        AddFolderRow(topPanel, 2, "USB folder:", _usbFolderTextBox, _browseUsbButton, "Browse...", () => BrowseForUsbFolder());

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 250,
            FixedPanel = FixedPanel.Panel1
        };

        var leftPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };

        var filesHeaderPanel = new Panel { Dock = DockStyle.Top, Height = 30 };
        var filesLabel = new Label
        {
            Text = "CSV Files",
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _refreshButton.Text = "Refresh";
        _refreshButton.Dock = DockStyle.Right;
        _refreshButton.Width = 78;
        _refreshButton.Click += (_, _) => RefreshCsvFileListPreserveSelection();
        filesHeaderPanel.Controls.Add(filesLabel);
        filesHeaderPanel.Controls.Add(_refreshButton);

        _fileListBox.Dock = DockStyle.Fill;
        _fileListBox.SelectedIndexChanged += (_, _) => LoadSelectedCsv();

        leftPanel.Controls.Add(_fileListBox);
        leftPanel.Controls.Add(filesHeaderPanel);

        var rightPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };

        var bottomControls = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 52
        };

        _mirrorCheckBox.Text = "Mirror about Y axis";
        _mirrorCheckBox.Dock = DockStyle.Left;
        _mirrorCheckBox.Width = 185;
        _mirrorCheckBox.CheckedChanged += (_, _) =>
        {
            RefreshPreview();
            SaveSettings();
        };

        _exportUsbButton.Text = "Write to USB";
        _exportUsbButton.Dock = DockStyle.Right;
        _exportUsbButton.Width = 120;
        _exportUsbButton.Enabled = false;
        _exportUsbButton.Click += (_, _) => ExportDxfToConfiguredFolder(_usbFolderTextBox.Text, "USB");

        _exportButton.Text = "Export DXF";
        _exportButton.Dock = DockStyle.Right;
        _exportButton.Width = 120;
        _exportButton.Enabled = false;
        _exportButton.Click += (_, _) => ExportDxfToConfiguredFolder(_outputFolderTextBox.Text, "DXF output");

        bottomControls.Controls.Add(_exportUsbButton);
        bottomControls.Controls.Add(_exportButton);
        bottomControls.Controls.Add(_mirrorCheckBox);

        _previewPanel.Dock = DockStyle.Fill;

        rightPanel.Controls.Add(_previewPanel);
        rightPanel.Controls.Add(bottomControls);

        split.Panel1.Controls.Add(leftPanel);
        split.Panel2.Controls.Add(rightPanel);

        _statusStrip.Items.Add(_statusLabel);
        _statusLabel.Text = "Ready";

        Controls.Add(split);
        Controls.Add(topPanel);
        Controls.Add(_statusStrip);
    }

    private void AddFolderRow(TableLayoutPanel panel, int row, string labelText, TextBox textBox, Button button, string buttonText, Action browseAction)
    {
        var label = new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        textBox.Dock = DockStyle.Fill;
        textBox.Leave += (_, _) => SaveFolderSettingsFromTextBoxes();
        textBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;
            SaveFolderSettingsFromTextBoxes();
        };

        button.Text = buttonText;
        button.Dock = DockStyle.Fill;
        button.Click += (_, _) => browseAction();

        panel.Controls.Add(label, 0, row);
        panel.Controls.Add(textBox, 1, row);
        panel.Controls.Add(button, 2, row);
    }

    private void BrowseForInputFolder()
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

    private void BrowseForOutputFolder()
    {
        var selected = BrowseForFolder("Select default DXF output folder", _outputFolderTextBox.Text);
        if (selected is null)
            return;

        _outputFolderTextBox.Text = selected;
        SaveSettings();
    }

    private void BrowseForUsbFolder()
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
            _previewPanel.SetFeatures([], _mirrorCheckBox.Checked);
            UpdateExportButtons();
            _statusLabel.Text = "Failed to load CSV";
            if (showErrors)
                MessageBox.Show(this, ex.Message, "CSV load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshPreview()
    {
        _previewPanel.SetFeatures(_currentFeatures, _mirrorCheckBox.Checked);
    }

    private void UpdateExportButtons()
    {
        var canExport = _currentCsvPath is not null && _currentFeatures.Count > 0;
        _exportButton.Enabled = canExport;
        _exportUsbButton.Enabled = canExport;
    }

    private void ExportDxfToConfiguredFolder(string folder, string targetName)
    {
        if (_currentCsvPath is null || _currentFeatures.Count == 0)
            return;

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            MessageBox.Show(this, $"Please configure a valid {targetName} folder first.", "Folder not configured", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var outputPath = BuildOutputPath(folder);

        try
        {
            DxfExporter.Export(outputPath, _currentFeatures, _mirrorCheckBox.Checked);
            _statusLabel.Text = $"Exported {Path.GetFileName(outputPath)} to {targetName}";
            MessageBox.Show(this, $"DXF exported successfully:\n\n{outputPath}", "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "DXF export failed";
            MessageBox.Show(this, ex.Message, "DXF export error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string BuildOutputPath(string folder)
    {
        var defaultName = Path.GetFileNameWithoutExtension(_currentCsvPath) ?? "features";
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
