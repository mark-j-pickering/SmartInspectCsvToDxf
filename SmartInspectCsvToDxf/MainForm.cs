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
    private const int TimestampColumnGap = 24;

    private readonly AppSettings _settings;
    private readonly UpdateService _updateService;
    private readonly List<FileSystemWatcher> _watchers = [];
    private string? _currentReportPath;
    private string _savedInputFolder = string.Empty;

    // _currentFeatures: raw, exactly as read from the currently loaded file - never mutated.
    // _previewFeatures: the live, current state shown in the preview and exported for this
    // file - a fresh list produced by whichever rotate/mirror/align transform was just
    // clicked, applied directly to what was already there. There's no undo/redo and no
    // "recipe" describing how to get from one to the other - each click is a one-shot,
    // discrete edit of the current picture, not a replayable step to reapply elsewhere
    // (batch-exporting other files uses their own untransformed data - see
    // ExportDxfToConfiguredFolder). The counters below exist purely to build a short,
    // human-readable summary for the footer/export filename, not to reconstruct anything.
    private List<Feature> _currentFeatures = [];
    private List<Feature> _previewFeatures = [];
    private int _mirrorXCount;
    private int _mirrorYCount;
    private int _rotateRightCount;
    private int _rotateLeftCount;
    private (double Angle, DrawingPlane Plane)? _lastAlign;

    private bool _suppressSelectionChangedHandling;

    private bool HasActiveOrientation =>
        _mirrorXCount != 0 || _mirrorYCount != 0 || _rotateRightCount != 0 || _rotateLeftCount != 0 || _lastAlign is not null;

    public MainForm()
    {
        InitializeComponent();

        _settings = AppSettings.Load();
        ApplyDefaultFoldersIfMissing();
        ApplyWindowBoundsFromSettings();
        _updateService = new UpdateService(new UpdateDiagnosticLog());

        ApplyOrientationButtonIcons();
        _previewPanel.LineAligned += PreviewPanel_LineAligned;
        _previewPanel.AlignModeExited += PreviewPanel_AlignModeExited;
        _previewPanel.OriginPickModeExited += PreviewPanel_OriginPickModeExited;
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

    // Restores the previous session's window position/size/maximized state, if one was
    // saved. Guarded against a saved position that's no longer reachable (e.g. the monitor
    // it was on has since been disconnected or reconfigured) by requiring it to overlap at
    // least one currently-connected screen - otherwise falls back to the Designer's default
    // CenterScreen placement rather than opening off-screen where the user can't reach it.
    private void ApplyWindowBoundsFromSettings()
    {
        if (_settings.WindowX.HasValue && _settings.WindowY.HasValue
            && _settings.WindowWidth > 0 && _settings.WindowHeight > 0)
        {
            var bounds = new Rectangle(_settings.WindowX.Value, _settings.WindowY.Value, _settings.WindowWidth, _settings.WindowHeight);
            if (Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(bounds)))
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = bounds;
            }
        }

        if (_settings.WindowMaximized)
            WindowState = FormWindowState.Maximized;
    }

    // FormStartPosition.CenterParent + Owner has proven unreliable in practice (observed
    // landing at an unrelated cascade position instead of centering over this window,
    // seemingly a WinForms quirk in wide/multi-monitor layouts) - computing the centered
    // location directly against this form's own Bounds sidesteps whatever that issue is.
    private void CenterOnMainForm(Form dialog)
    {
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new Point(
            Location.X + (Width - dialog.Width) / 2,
            Location.Y + (Height - dialog.Height) / 2);
    }

    private void ApplyOrientationButtonIcons()
    {
        const int iconSize = 22;

        void SetIcon(ButtonBase button, Bitmap icon)
        {
            button.Image = icon;
            button.ImageAlign = ContentAlignment.TopCenter;
            button.TextAlign = ContentAlignment.BottomCenter;
            button.TextImageRelation = TextImageRelation.ImageAboveText;
            button.Padding = new Padding(0, 4, 0, 0);
        }

        SetIcon(_resetButton, ButtonIcons.Reset(iconSize));
        SetIcon(_mirrorXButton, ButtonIcons.MirrorX(iconSize));
        SetIcon(_mirrorYButton, ButtonIcons.MirrorY(iconSize));
        SetIcon(_rotateLeftButton, ButtonIcons.RotateLeft(iconSize));
        SetIcon(_rotateRightButton, ButtonIcons.RotateRight(iconSize));
        SetIcon(_showTextCheckBox, ButtonIcons.ShowText(iconSize));
        SetIcon(_alignModeCheckBox, ButtonIcons.Align(iconSize));
        SetIcon(_setOriginCheckBox, ButtonIcons.SetOrigin(iconSize));
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
                    CenteredMessageBox.Show(
                        this,
                        $"You're running the latest version (v{result.CurrentVersion}).",
                        "No updates available",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    break;
                default:
                    CenteredMessageBox.Show(
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
        CenterOnMainForm(dialog);
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
        // Suppressed while SelectReportFile is restoring the previously-selected item after
        // the list was rebuilt (Refresh, or the file-watcher's automatic list refresh) - that
        // re-selection is just visual bookkeeping, not a user picking a (possibly different)
        // file, so it must not cascade into a full reload/orientation reset. A genuine change
        // of file (or the watcher noticing the current file itself changed) still goes
        // through LoadSelectedReport/ReloadCurrentReportIfStillPresent explicitly elsewhere.
        if (_suppressSelectionChangedHandling)
            return;

        LoadSelectedReport();
        UpdateExportButtons();
    }

    // A plain click on the item that's already the sole selection doesn't raise
    // SelectedIndexChanged (the selected index doesn't actually change), so re-loading
    // (and resetting orientation) on such a click needs to be triggered here instead.
    // Comparing against _currentReportPath - which only changes once a load actually
    // completes - rather than _fileListBox.SelectedIndices is deliberate: by the time this
    // handler runs, the ListBox has already applied the click to its selection, so checking
    // "is the clicked item now the sole selection" would be true for every plain click
    // (including a normal click on a not-yet-selected file), double-firing alongside the
    // SelectedIndexChanged this same click also raises.
    private void FileListBox_MouseDown(object? sender, MouseEventArgs e)
    {
        if (ModifierKeys != Keys.None)
            return;

        var clickedIndex = _fileListBox.IndexFromPoint(e.Location);
        if (clickedIndex < 0 || clickedIndex >= _fileListBox.Items.Count)
            return;

        if (_fileListBox.Items[clickedIndex] is ReportFileItem item
            && string.Equals(item.FullPath, _currentReportPath, StringComparison.OrdinalIgnoreCase))
        {
            LoadSelectedReport();
        }
    }

    private void RefreshButton_Click(object? sender, EventArgs e) => RefreshReportFileListPreserveSelection();

    private static string FormatTimestamp(DateTime value) => value.ToString("g");

    // Owner-drawn so the last-modified timestamp can sit right-justified on the same row as
    // the file name, rather than as a second line or a separate column control.
    private void FileListBox_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _fileListBox.Items.Count)
            return;

        e.DrawBackground();

        if (_fileListBox.Items[e.Index] is ReportFileItem item)
        {
            var isSelected = (e.State & DrawItemState.Selected) != 0;
            var textColor = isSelected ? SystemColors.HighlightText : _fileListBox.ForeColor;
            var timestampColor = isSelected ? SystemColors.HighlightText : Color.DimGray;
            var timestampText = FormatTimestamp(item.LastWriteTime);
            var timestampWidth = TextRenderer.MeasureText(e.Graphics, timestampText, e.Font).Width;

            var timestampBounds = new Rectangle(e.Bounds.Right - timestampWidth - 4, e.Bounds.Top, timestampWidth, e.Bounds.Height);
            var nameBounds = new Rectangle(e.Bounds.Left + 2, e.Bounds.Top, e.Bounds.Width - timestampWidth - 8, e.Bounds.Height);

            TextRenderer.DrawText(e.Graphics, item.ToString(), e.Font, nameBounds, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, timestampText, e.Font, timestampBounds, timestampColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        e.DrawFocusRectangle();
    }

    // Mirror is a one-shot transform, not a persistent toggle - every click flips whatever's
    // currently displayed about that axis, right now, rather than setting an "is mirrored"
    // flag that gets reapplied fresh in some fixed order relative to rotate/align. There's
    // no undo/redo, so there's nothing to replay either - each click directly transforms the
    // live feature list once and that's the new current state.
    private void MirrorXButton_Click(object? sender, EventArgs e)
    {
        _previewFeatures = _previewFeatures.Select(f => f.WithMirrorX()).ToList();
        _mirrorXCount++;
        _previewPanel.AnimateMirror(mirrorX: true, _previewFeatures, _currentFeatures, _showTextCheckBox.Checked, BuildOrientationDescription());
    }

    private void MirrorYButton_Click(object? sender, EventArgs e)
    {
        _previewFeatures = _previewFeatures.Select(f => f.WithMirrorY()).ToList();
        _mirrorYCount++;
        _previewPanel.AnimateMirror(mirrorX: false, _previewFeatures, _currentFeatures, _showTextCheckBox.Checked, BuildOrientationDescription());
    }

    private void RotateLeftButton_Click(object? sender, EventArgs e)
    {
        _previewFeatures = _previewFeatures.Select(f => f.WithRotatedLeft90()).ToList();
        _rotateLeftCount++;
        // Hardcoded to XY, not the panel's currently-viewed plane: WithRotatedLeft90 always
        // rotates raw X/Y, so the animation must match that regardless of which plane is
        // being viewed, or it'd visibly spin the wrong axis pair and then pop to the correct
        // final pose (Align below is different - it's already plane-aware).
        _previewPanel.AnimateRotation(-90, DrawingPlane.XY, _previewFeatures, _currentFeatures, _showTextCheckBox.Checked, BuildOrientationDescription());
    }

    private void RotateRightButton_Click(object? sender, EventArgs e)
    {
        _previewFeatures = _previewFeatures.Select(f => f.WithRotatedRight90()).ToList();
        _rotateRightCount++;
        _previewPanel.AnimateRotation(90, DrawingPlane.XY, _previewFeatures, _currentFeatures, _showTextCheckBox.Checked, BuildOrientationDescription());
    }

    private void ShowTextCheckBox_CheckedChanged(object? sender, EventArgs e) => RefreshPreview();

    private void AlignModeCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_alignModeCheckBox.Checked)
            _setOriginCheckBox.Checked = false; // mutually exclusive preview pick-modes

        _previewPanel.AlignModeActive = _alignModeCheckBox.Checked;
    }

    private void PreviewPanel_LineAligned(double angleDegrees, DrawingPlane plane)
    {
        _previewFeatures = _previewFeatures.Select(f => f.WithRotatedInPlane(angleDegrees, plane)).ToList();
        _lastAlign = (angleDegrees, plane);
        _previewPanel.AnimateRotation(angleDegrees, plane, _previewFeatures, _currentFeatures, _showTextCheckBox.Checked, BuildOrientationDescription());
    }

    private void PreviewPanel_AlignModeExited()
    {
        _alignModeCheckBox.Checked = false;
    }

    // Set Origin is a display-only, single-shot pick just like Align: click the button, click
    // a key point in the preview, done. Unlike Mirror/Rotate/Align it never touches Feature
    // data - it only shifts where PreviewPanel draws its axes and reports coordinates - so
    // there's no counter/description here to track, nothing added to BuildOrientationDescription
    // or BuildFileNameSuffix, and it plays no part in HasActiveOrientation.
    private void SetOriginCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        if (_setOriginCheckBox.Checked)
            _alignModeCheckBox.Checked = false; // mutually exclusive preview pick-modes

        _previewPanel.OriginPickModeActive = _setOriginCheckBox.Checked;
    }

    private void PreviewPanel_OriginPickModeExited()
    {
        _setOriginCheckBox.Checked = false;
    }

    private void ResetButton_Click(object? sender, EventArgs e)
    {
        ResetOrientation();
        _previewPanel.ResetOrigin();
        _previewPanel.ResetPlane();
        RefreshPreview();
    }

    private void ResetOrientation()
    {
        _previewFeatures = new List<Feature>(_currentFeatures);
        _mirrorXCount = 0;
        _mirrorYCount = 0;
        _rotateRightCount = 0;
        _rotateLeftCount = 0;
        _lastAlign = null;
    }

    // Purely informational summary of the counters above, shown in the preview footer.
    private string BuildOrientationDescription()
    {
        var parts = new List<string>();

        if (_rotateRightCount == 1)
            parts.Add("rotated 90° CW");
        else if (_rotateRightCount > 1)
            parts.Add($"rotated 90° CW ×{_rotateRightCount}");

        if (_rotateLeftCount == 1)
            parts.Add("rotated 90° CCW");
        else if (_rotateLeftCount > 1)
            parts.Add($"rotated 90° CCW ×{_rotateLeftCount}");

        if (_mirrorXCount == 1)
            parts.Add("mirrored about X axis");
        else if (_mirrorXCount > 1)
            parts.Add($"mirrored about X axis ×{_mirrorXCount}");

        if (_mirrorYCount == 1)
            parts.Add("mirrored about Y axis");
        else if (_mirrorYCount > 1)
            parts.Add($"mirrored about Y axis ×{_mirrorYCount}");

        if (_lastAlign is { } align)
            parts.Add($"aligned {align.Angle:0.##}° ({align.Plane})");

        return string.Join(", ", parts);
    }

    // Same counters, formatted as a filesystem-safe filename suffix for the exported DXF.
    private string BuildFileNameSuffix()
    {
        var suffix = string.Empty;

        if (_rotateRightCount == 1)
            suffix += "_rotR90";
        else if (_rotateRightCount > 1)
            suffix += $"_rotR90x{_rotateRightCount}";

        if (_rotateLeftCount == 1)
            suffix += "_rotL90";
        else if (_rotateLeftCount > 1)
            suffix += $"_rotL90x{_rotateLeftCount}";

        if (_mirrorXCount == 1)
            suffix += "_mirrored_x";
        else if (_mirrorXCount > 1)
            suffix += $"_mirrored_xx{_mirrorXCount}";

        if (_mirrorYCount == 1)
            suffix += "_mirrored_y";
        else if (_mirrorYCount > 1)
            suffix += $"_mirrored_yx{_mirrorYCount}";

        if (_lastAlign is { } align)
        {
            var sign = align.Angle < 0 ? "m" : string.Empty;
            suffix += $"_aligned{sign}{Math.Abs(Math.Round(align.Angle)):0}";
        }

        return suffix;
    }

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

        CenteredMessageBox.Show(this, message, "USB drive not detected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        // RestoreBounds - not Bounds - when minimized/maximized: it holds the last normal
        // (non-minimized/maximized) size and position, which is what should be reapplied on
        // the next launch rather than the full-screen or taskbar-sized current bounds.
        var windowBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        if (windowBounds is { Width: > 0, Height: > 0 })
        {
            _settings.WindowX = windowBounds.X;
            _settings.WindowY = windowBounds.Y;
            _settings.WindowWidth = windowBounds.Width;
            _settings.WindowHeight = windowBounds.Height;
        }

        _settings.WindowMaximized = WindowState == FormWindowState.Maximized;

        if (!_settings.Save())
        {
            const string message = "Could not save settings — check permissions on %APPDATA%\\SmartInspectCsvToDxf";
            _statusLabel.Text = message;
            CenteredMessageBox.Show(this, message, "Settings not saved", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    var width = TextRenderer.MeasureText(g, item?.ToString() ?? string.Empty, _fileListBox.Font).Width;
                    if (item is ReportFileItem reportItem)
                        width += TextRenderer.MeasureText(g, FormatTimestamp(reportItem.LastWriteTime), _fileListBox.Font).Width + TimestampColumnGap;

                    if (width > maxTextWidth)
                        maxTextWidth = width;
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
                // Just restoring visual selection after a list rebuild (see
                // FileListBox_SelectedIndexChanged) - suppress the SelectedIndexChanged this
                // assignment would otherwise raise, since nothing the user did actually
                // changed which file is loaded.
                _suppressSelectionChangedHandling = true;
                try
                {
                    _fileListBox.SelectedIndex = i;
                }
                finally
                {
                    _suppressSelectionChangedHandling = false;
                }

                return;
            }
        }
    }

    private async void LoadSelectedReport()
    {
        if (_fileListBox.SelectedItem is not ReportFileItem item)
            return;

        // Explicit, user-driven selection: show a progress dialog and read off the UI
        // thread, since PDF reports can take a moment to parse. LoadReportAsync resets any
        // rotate/mirror/align state on success - selecting (or re-clicking) a file is a
        // deliberate "start fresh with this file" action, not something to silently carry
        // over from whatever was previously viewed.
        await LoadReportAsync(item.FullPath, showErrors: true, showProgress: true);

        // Move focus to the preview so the arrow keys immediately drive the drawing-
        // plane override, without requiring an extra click. Only on this explicit,
        // user-driven selection - not on the file-watcher's automatic reloads (see
        // ReloadCurrentReportIfStillPresent), where stealing focus would be disruptive.
        _previewPanel.Focus();
    }

    private async void ReloadCurrentReportIfStillPresent()
    {
        if (_currentReportPath is null || !File.Exists(_currentReportPath))
            return;

        // The file changed on disk (SmartInspect re-exported it) while the user has an
        // active rotate/mirror/align applied - reloading would silently discard that, so
        // ask first. Nothing to lose (and nothing to ask about) when nothing's been applied.
        if (HasActiveOrientation)
        {
            var reload = CenteredMessageBox.Show(
                this,
                "The report file has been modified. Do you want to reload it?\n\nDoing so will reset the rotation, mirror, and align settings.",
                "Report file changed",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (reload != DialogResult.Yes)
                return;
        }

        // Automatic file-watcher reload: no progress dialog, matches prior silent behavior.
        // LoadReportAsync resets rotate/mirror/align on success.
        await LoadReportAsync(_currentReportPath, showErrors: false, showProgress: false);
    }

    private async Task LoadReportAsync(string path, bool showErrors, bool showProgress)
    {
        ProgressDialog? progress = null;
        if (showProgress)
        {
            progress = new ProgressDialog("Loading report", 1) { Owner = this };
            CenterOnMainForm(progress);
            progress.ReportProgress(1, 1, Path.GetFileName(path));
            progress.Show();
            // Give the dialog a moment to actually be seen - a small report reads fast
            // enough that it would otherwise flash and close before it's visible.
            await Task.Delay(500);
        }

        try
        {
            _currentReportPath = path;
            _currentFeatures = showProgress
                ? await Task.Run(() => ReportFileReader.Read(path))
                : ReportFileReader.Read(path);
            ResetOrientation();
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
            ResetOrientation();
            RefreshPreview();
            UpdateExportButtons();
            _statusLabel.Text = "Failed to load report";

            // Close the progress dialog before showing the error, rather than leaving it
            // open behind the error dialog until the finally block below runs.
            progress?.Close();
            progress?.Dispose();
            progress = null;

            if (showErrors)
                CenteredMessageBox.Show(this, ex.Message, "Report load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            progress?.Close();
            progress?.Dispose();
        }
    }

    private void RefreshPreview()
    {
        _previewPanel.SetFeatures(_previewFeatures, _currentFeatures, _showTextCheckBox.Checked, BuildOrientationDescription());
    }

    private void UpdateExportButtons()
    {
        var count = _fileListBox.SelectedItems.Count;
        _exportButton.Enabled = count > 0;
        _exportUsbButton.Enabled = count > 0;
        _exportButton.Text = count > 1 ? $"Export DXF ({count})" : "Export DXF";
        _exportUsbButton.Text = count > 1 ? $"Write to USB ({count})" : "Write to USB";
    }

    private async void ExportDxfToConfiguredFolder(string folder, string targetName)
    {
        var items = _fileListBox.SelectedItems.Cast<ReportFileItem>().ToList();
        if (items.Count == 0)
            return;

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            CenteredMessageBox.Show(this, $"Please configure a valid {targetName} folder first.", "Folder not configured", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var exported = new List<string>();
        var skipped = new List<string>();
        var failures = new List<(string File, string Error)>();

        _exportButton.Enabled = false;
        _exportUsbButton.Enabled = false;

        using var progress = new ProgressDialog("Exporting DXF", items.Count) { Owner = this };
        CenterOnMainForm(progress);
        progress.Show();
        // Give the dialog a moment to actually be seen — a single small report reads and
        // exports fast enough that it would otherwise flash and close before it's visible.
        await Task.Delay(500);
        try
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var fileName = Path.GetFileName(item.FullPath);
                progress.ReportProgress(i + 1, items.Count, fileName);

                try
                {
                    // Only the file currently shown in the preview carries the live rotate/
                    // mirror/align edits - every other file in a batch export is written out
                    // exactly as it exists on disk, untransformed (see the class-level field
                    // comment on _previewFeatures for why: there's no reusable "recipe" to
                    // replay onto a different file's data, only the current result for this
                    // one).
                    var isPreviewedFile = string.Equals(item.FullPath, _currentReportPath, StringComparison.OrdinalIgnoreCase);
                    var outputPath = BuildOutputPath(folder, item.FullPath, isPreviewedFile);
                    if (File.Exists(outputPath))
                    {
                        var overwrite = CenteredMessageBox.Show(
                            this,
                            $"{Path.GetFileName(outputPath)} already exists.\n\nDo you want to replace it?",
                            "Confirm Save As",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);

                        if (overwrite != DialogResult.Yes)
                        {
                            skipped.Add(fileName);
                            continue;
                        }
                    }

                    var features = isPreviewedFile
                        ? _previewFeatures
                        : await Task.Run(() => ReportFileReader.Read(item.FullPath));

                    if (features.Count == 0)
                    {
                        failures.Add((fileName, "No valid features found"));
                        continue;
                    }

                    // Only the file currently shown in the preview can have a manual plane
                    // override attached to it; every other file in the batch gets its own
                    // auto-detected plane.
                    DrawingPlane? plane = isPreviewedFile && _previewPanel.IsPlaneOverridden
                        ? _previewPanel.DrawingPlane
                        : null;

                    await Task.Run(() => DxfExporter.Export(outputPath, features, plane));
                    exported.Add(Path.GetFileName(outputPath));
                }
                catch (Exception ex)
                {
                    failures.Add((fileName, ex.Message));
                }
            }
        }
        finally
        {
            progress.Close();
            UpdateExportButtons();
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

        if (skipped.Count > 0)
        {
            if (summary.Length > 0)
                summary.AppendLine();

            summary.AppendLine($"Skipped ({skipped.Count}):");
            summary.AppendLine(string.Join(Environment.NewLine, skipped));
        }

        if (failures.Count > 0)
        {
            if (summary.Length > 0)
                summary.AppendLine();

            summary.AppendLine($"Failed ({failures.Count}):");
            foreach (var (file, error) in failures)
                summary.AppendLine($"{file}: {error}");
        }

        CenteredMessageBox.Show(
            this,
            summary.ToString().TrimEnd(),
            failures.Count == 0 ? "Export complete" : "Export completed with errors",
            MessageBoxButtons.OK,
            failures.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private string BuildOutputPath(string folder, string reportPath, bool includeOrientationSuffix)
    {
        var defaultName = Path.GetFileNameWithoutExtension(reportPath) ?? "features";
        if (includeOrientationSuffix)
            defaultName += BuildFileNameSuffix();

        return Path.Combine(folder, defaultName + ".dxf");
    }

    private sealed class ReportFileItem
    {
        public ReportFileItem(string fullPath)
        {
            FullPath = fullPath;
            LastWriteTime = File.GetLastWriteTime(fullPath);
        }

        public string FullPath { get; }
        public DateTime LastWriteTime { get; }

        public override string ToString()
        {
            var name = Path.GetFileNameWithoutExtension(FullPath);
            var extension = Path.GetExtension(FullPath).TrimStart('.').ToUpperInvariant();
            return extension.Length == 0 ? name : $"{name}  [{extension}]";
        }
    }
}
