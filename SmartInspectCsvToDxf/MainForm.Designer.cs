using SmartInspectCsvToDxf.UI;

#nullable disable

namespace SmartInspectCsvToDxf;

partial class MainForm
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        _topPanel = new TableLayoutPanel();
        _csvFolderLabel = new Label();
        _inputFolderTextBox = new TextBox();
        _browseInputButton = new Button();
        _dxfFolderLabel = new Label();
        _outputFolderTextBox = new TextBox();
        _browseOutputButton = new Button();
        _usbFolderLabel = new Label();
        _usbFolderTextBox = new TextBox();
        _browseUsbButton = new Button();
        _splitContainer = new SplitContainer();
        _leftPanel = new Panel();
        _fileListBox = new ListBox();
        _filesHeaderPanel = new Panel();
        _filesLabel = new Label();
        _refreshButton = new Button();
        _rightPanel = new Panel();
        _previewPanel = new PreviewPanel();
        _bottomControlsPanel = new Panel();
        _exportUsbButton = new Button();
        _exportButton = new Button();
        _showTextCheckBox = new CheckBox();
        _mirrorXButton = new Button();
        _mirrorYButton = new Button();
        _rotateLeftButton = new Button();
        _rotateRightButton = new Button();
        _alignModeCheckBox = new CheckBox();
        _setOriginCheckBox = new CheckBox();
        _resetButton = new Button();
        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel();
        _refreshTimer = new System.Windows.Forms.Timer(components);
        _fileListToolTip = new ToolTip(components);
        _menuStrip = new MenuStrip();
        _helpMenuItem = new ToolStripMenuItem();
        _checkForUpdatesMenuItem = new ToolStripMenuItem();
        _aboutMenuItem = new ToolStripMenuItem();
        _menuStrip.SuspendLayout();
        _topPanel.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_splitContainer).BeginInit();
        _splitContainer.Panel1.SuspendLayout();
        _splitContainer.Panel2.SuspendLayout();
        _splitContainer.SuspendLayout();
        _leftPanel.SuspendLayout();
        _filesHeaderPanel.SuspendLayout();
        _rightPanel.SuspendLayout();
        _bottomControlsPanel.SuspendLayout();
        _statusStrip.SuspendLayout();
        SuspendLayout();
        // 
        // _topPanel
        // 
        _topPanel.ColumnCount = 3;
        _topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        _topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115F));
        _topPanel.Controls.Add(_csvFolderLabel, 0, 0);
        _topPanel.Controls.Add(_inputFolderTextBox, 1, 0);
        _topPanel.Controls.Add(_browseInputButton, 2, 0);
        _topPanel.Controls.Add(_dxfFolderLabel, 0, 1);
        _topPanel.Controls.Add(_outputFolderTextBox, 1, 1);
        _topPanel.Controls.Add(_browseOutputButton, 2, 1);
        _topPanel.Controls.Add(_usbFolderLabel, 0, 2);
        _topPanel.Controls.Add(_usbFolderTextBox, 1, 2);
        _topPanel.Controls.Add(_browseUsbButton, 2, 2);
        _topPanel.Dock = DockStyle.Top;
        _topPanel.Location = new Point(0, 0);
        _topPanel.Name = "_topPanel";
        _topPanel.Padding = new Padding(8);
        _topPanel.RowCount = 3;
        _topPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        _topPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        _topPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        _topPanel.Size = new Size(1204, 136);
        _topPanel.TabIndex = 1;
        // 
        // _csvFolderLabel
        // 
        _csvFolderLabel.Dock = DockStyle.Fill;
        _csvFolderLabel.Location = new Point(11, 8);
        _csvFolderLabel.Name = "_csvFolderLabel";
        _csvFolderLabel.Size = new Size(84, 30);
        _csvFolderLabel.TabIndex = 0;
        _csvFolderLabel.Text = "Report folder:";
        _csvFolderLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _inputFolderTextBox
        // 
        _inputFolderTextBox.Dock = DockStyle.Fill;
        _inputFolderTextBox.Location = new Point(101, 11);
        _inputFolderTextBox.Name = "_inputFolderTextBox";
        _inputFolderTextBox.Size = new Size(977, 23);
        _inputFolderTextBox.TabIndex = 1;
        _inputFolderTextBox.KeyDown += FolderTextBox_KeyDown;
        _inputFolderTextBox.Leave += FolderTextBox_Leave;
        // 
        // _browseInputButton
        // 
        _browseInputButton.Dock = DockStyle.Fill;
        _browseInputButton.Location = new Point(1084, 11);
        _browseInputButton.Name = "_browseInputButton";
        _browseInputButton.Size = new Size(109, 24);
        _browseInputButton.TabIndex = 2;
        _browseInputButton.Text = "Browse...";
        _browseInputButton.Click += BrowseInputButton_Click;
        // 
        // _dxfFolderLabel
        // 
        _dxfFolderLabel.Dock = DockStyle.Fill;
        _dxfFolderLabel.Location = new Point(11, 38);
        _dxfFolderLabel.Name = "_dxfFolderLabel";
        _dxfFolderLabel.Size = new Size(84, 30);
        _dxfFolderLabel.TabIndex = 3;
        _dxfFolderLabel.Text = "DXF folder:";
        _dxfFolderLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _outputFolderTextBox
        // 
        _outputFolderTextBox.Dock = DockStyle.Fill;
        _outputFolderTextBox.Location = new Point(101, 41);
        _outputFolderTextBox.Name = "_outputFolderTextBox";
        _outputFolderTextBox.Size = new Size(977, 23);
        _outputFolderTextBox.TabIndex = 4;
        _outputFolderTextBox.KeyDown += FolderTextBox_KeyDown;
        _outputFolderTextBox.Leave += FolderTextBox_Leave;
        // 
        // _browseOutputButton
        // 
        _browseOutputButton.Dock = DockStyle.Fill;
        _browseOutputButton.Location = new Point(1084, 41);
        _browseOutputButton.Name = "_browseOutputButton";
        _browseOutputButton.Size = new Size(109, 24);
        _browseOutputButton.TabIndex = 5;
        _browseOutputButton.Text = "Browse...";
        _browseOutputButton.Click += BrowseOutputButton_Click;
        // 
        // _usbFolderLabel
        // 
        _usbFolderLabel.Dock = DockStyle.Fill;
        _usbFolderLabel.Location = new Point(11, 68);
        _usbFolderLabel.Name = "_usbFolderLabel";
        _usbFolderLabel.Size = new Size(84, 30);
        _usbFolderLabel.TabIndex = 6;
        _usbFolderLabel.Text = "USB folder:";
        _usbFolderLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _usbFolderTextBox
        // 
        _usbFolderTextBox.Dock = DockStyle.Fill;
        _usbFolderTextBox.Location = new Point(101, 71);
        _usbFolderTextBox.Name = "_usbFolderTextBox";
        _usbFolderTextBox.Size = new Size(977, 23);
        _usbFolderTextBox.TabIndex = 7;
        _usbFolderTextBox.KeyDown += FolderTextBox_KeyDown;
        _usbFolderTextBox.Leave += FolderTextBox_Leave;
        // 
        // _browseUsbButton
        // 
        _browseUsbButton.Dock = DockStyle.Fill;
        _browseUsbButton.Location = new Point(1084, 71);
        _browseUsbButton.Name = "_browseUsbButton";
        _browseUsbButton.Size = new Size(109, 24);
        _browseUsbButton.TabIndex = 8;
        _browseUsbButton.Text = "Browse...";
        _browseUsbButton.Click += BrowseUsbButton_Click;
        // 
        // _splitContainer
        // 
        _splitContainer.Dock = DockStyle.Fill;
        _splitContainer.FixedPanel = FixedPanel.Panel1;
        _splitContainer.Location = new Point(0, 104);
        _splitContainer.Name = "_splitContainer";
        // 
        // _splitContainer.Panel1
        // 
        _splitContainer.Panel1.Controls.Add(_leftPanel);
        // 
        // _splitContainer.Panel2
        // 
        _splitContainer.Panel2.Controls.Add(_rightPanel);
        _splitContainer.Size = new Size(1204, 655);
        _splitContainer.SplitterDistance = 250;
        _splitContainer.TabIndex = 0;
        // 
        // _leftPanel
        // 
        _leftPanel.Controls.Add(_fileListBox);
        _leftPanel.Controls.Add(_filesHeaderPanel);
        _leftPanel.Dock = DockStyle.Fill;
        _leftPanel.Location = new Point(0, 0);
        _leftPanel.Name = "_leftPanel";
        _leftPanel.Padding = new Padding(8);
        _leftPanel.Size = new Size(250, 655);
        _leftPanel.TabIndex = 0;
        // 
        // _fileListBox
        // 
        _fileListBox.Dock = DockStyle.Fill;
        _fileListBox.DrawMode = DrawMode.OwnerDrawFixed;
        _fileListBox.ItemHeight = 15;
        _fileListBox.Location = new Point(8, 38);
        _fileListBox.Name = "_fileListBox";
        _fileListBox.SelectionMode = SelectionMode.MultiExtended;
        _fileListBox.Size = new Size(234, 609);
        _fileListBox.TabIndex = 0;
        _fileListToolTip.SetToolTip(_fileListBox, "Ctrl+Click or Shift+Click to select multiple files for batch export");
        _fileListBox.SelectedIndexChanged += FileListBox_SelectedIndexChanged;
        _fileListBox.MouseDown += FileListBox_MouseDown;
        _fileListBox.DrawItem += FileListBox_DrawItem;
        // 
        // _filesHeaderPanel
        // 
        _filesHeaderPanel.Controls.Add(_filesLabel);
        _filesHeaderPanel.Controls.Add(_refreshButton);
        _filesHeaderPanel.Dock = DockStyle.Top;
        _filesHeaderPanel.Location = new Point(8, 8);
        _filesHeaderPanel.Name = "_filesHeaderPanel";
        _filesHeaderPanel.Size = new Size(234, 30);
        _filesHeaderPanel.TabIndex = 1;
        // 
        // _filesLabel
        // 
        _filesLabel.Dock = DockStyle.Fill;
        _filesLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _filesLabel.Location = new Point(0, 0);
        _filesLabel.Name = "_filesLabel";
        _filesLabel.Size = new Size(156, 30);
        _filesLabel.TabIndex = 0;
        _filesLabel.Text = "Report Files";
        _filesLabel.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // _refreshButton
        // 
        _refreshButton.Dock = DockStyle.Right;
        _refreshButton.Location = new Point(156, 0);
        _refreshButton.Name = "_refreshButton";
        _refreshButton.Size = new Size(78, 30);
        _refreshButton.TabIndex = 1;
        _refreshButton.Text = "Refresh";
        _refreshButton.Click += RefreshButton_Click;
        // 
        // _rightPanel
        // 
        _rightPanel.Controls.Add(_previewPanel);
        _rightPanel.Controls.Add(_bottomControlsPanel);
        _rightPanel.Dock = DockStyle.Fill;
        _rightPanel.Location = new Point(0, 0);
        _rightPanel.Name = "_rightPanel";
        _rightPanel.Padding = new Padding(8);
        _rightPanel.Size = new Size(950, 655);
        _rightPanel.TabIndex = 0;
        // 
        // _previewPanel
        // 
        _previewPanel.BackColor = Color.White;
        _previewPanel.Dock = DockStyle.Fill;
        _previewPanel.Location = new Point(8, 8);
        _previewPanel.Name = "_previewPanel";
        _previewPanel.Size = new Size(934, 587);
        _previewPanel.TabIndex = 0;
        // 
        // _bottomControlsPanel
        // 
        _bottomControlsPanel.Controls.Add(_exportUsbButton);
        _bottomControlsPanel.Controls.Add(_exportButton);
        // Dock.Left controls are laid out with the *last*-added one closest to the true
        // left edge, so this list is in reverse of the intended left-to-right visual
        // order (Reset, Align, Set Origin, Rotate Left, Rotate Right, Mirror X, Mirror Y,
        // Show Labels).
        _bottomControlsPanel.Controls.Add(_showTextCheckBox);
        _bottomControlsPanel.Controls.Add(_mirrorYButton);
        _bottomControlsPanel.Controls.Add(_mirrorXButton);
        _bottomControlsPanel.Controls.Add(_rotateRightButton);
        _bottomControlsPanel.Controls.Add(_rotateLeftButton);
        _bottomControlsPanel.Controls.Add(_setOriginCheckBox);
        _bottomControlsPanel.Controls.Add(_alignModeCheckBox);
        _bottomControlsPanel.Controls.Add(_resetButton);
        _bottomControlsPanel.Dock = DockStyle.Bottom;
        _bottomControlsPanel.Location = new Point(8, 595);
        _bottomControlsPanel.Name = "_bottomControlsPanel";
        _bottomControlsPanel.Size = new Size(934, 52);
        _bottomControlsPanel.TabIndex = 1;
        // 
        // _exportUsbButton
        // 
        _exportUsbButton.Dock = DockStyle.Right;
        _exportUsbButton.Enabled = false;
        _exportUsbButton.Location = new Point(724, 0);
        _exportUsbButton.Name = "_exportUsbButton";
        _exportUsbButton.Size = new Size(105, 52);
        _exportUsbButton.TabIndex = 0;
        _exportUsbButton.Text = "Write to USB";
        _exportUsbButton.Click += ExportUsbButton_Click;
        //
        // _exportButton
        //
        _exportButton.Dock = DockStyle.Right;
        _exportButton.Enabled = false;
        _exportButton.Location = new Point(829, 0);
        _exportButton.Name = "_exportButton";
        _exportButton.Size = new Size(105, 52);
        _exportButton.TabIndex = 1;
        _exportButton.Text = "Export DXF";
        _exportButton.Click += ExportButton_Click;
        // 
        // _showTextCheckBox
        //
        _showTextCheckBox.Appearance = Appearance.Button;
        _showTextCheckBox.Checked = true;
        _showTextCheckBox.CheckState = CheckState.Checked;
        _showTextCheckBox.Dock = DockStyle.Left;
        _showTextCheckBox.Location = new Point(663, 0);
        _showTextCheckBox.Name = "_showTextCheckBox";
        _showTextCheckBox.Size = new Size(110, 52);
        _showTextCheckBox.TabIndex = 2;
        _showTextCheckBox.Text = "Show Labels";
        _showTextCheckBox.CheckedChanged += ShowTextCheckBox_CheckedChanged;
        //
        // _mirrorYButton
        //
        _mirrorYButton.Dock = DockStyle.Left;
        _mirrorYButton.Location = new Point(573, 0);
        _mirrorYButton.Name = "_mirrorYButton";
        _mirrorYButton.Size = new Size(90, 52);
        _mirrorYButton.TabIndex = 3;
        _mirrorYButton.Text = "Mirror Y";
        _mirrorYButton.Click += MirrorYButton_Click;
        //
        // _mirrorXButton
        //
        _mirrorXButton.Dock = DockStyle.Left;
        _mirrorXButton.Location = new Point(483, 0);
        _mirrorXButton.Name = "_mirrorXButton";
        _mirrorXButton.Size = new Size(90, 52);
        _mirrorXButton.TabIndex = 4;
        _mirrorXButton.Text = "Mirror X";
        _mirrorXButton.Click += MirrorXButton_Click;
        //
        // _rotateRightButton
        //
        _rotateRightButton.Dock = DockStyle.Left;
        _rotateRightButton.Location = new Point(368, 0);
        _rotateRightButton.Name = "_rotateRightButton";
        _rotateRightButton.Size = new Size(115, 52);
        _rotateRightButton.TabIndex = 5;
        _rotateRightButton.Text = "Rotate Right 90°";
        _rotateRightButton.Click += RotateRightButton_Click;
        //
        // _rotateLeftButton
        //
        _rotateLeftButton.Dock = DockStyle.Left;
        _rotateLeftButton.Location = new Point(253, 0);
        _rotateLeftButton.Name = "_rotateLeftButton";
        _rotateLeftButton.Size = new Size(115, 52);
        _rotateLeftButton.TabIndex = 6;
        _rotateLeftButton.Text = "Rotate Left 90°";
        _rotateLeftButton.Click += RotateLeftButton_Click;
        //
        // _setOriginCheckBox
        //
        _setOriginCheckBox.Appearance = Appearance.Button;
        _setOriginCheckBox.Dock = DockStyle.Left;
        _setOriginCheckBox.Location = new Point(143, 0);
        _setOriginCheckBox.Name = "_setOriginCheckBox";
        _setOriginCheckBox.Size = new Size(110, 52);
        _setOriginCheckBox.TabIndex = 7;
        _setOriginCheckBox.Text = "Set Origin";
        _setOriginCheckBox.CheckedChanged += SetOriginCheckBox_CheckedChanged;
        //
        // _alignModeCheckBox
        //
        _alignModeCheckBox.Appearance = Appearance.Button;
        _alignModeCheckBox.Dock = DockStyle.Left;
        _alignModeCheckBox.Location = new Point(68, 0);
        _alignModeCheckBox.Name = "_alignModeCheckBox";
        _alignModeCheckBox.Size = new Size(75, 52);
        _alignModeCheckBox.TabIndex = 8;
        _alignModeCheckBox.Text = "Align";
        _alignModeCheckBox.CheckedChanged += AlignModeCheckBox_CheckedChanged;
        //
        // _resetButton
        //
        _resetButton.Dock = DockStyle.Left;
        _resetButton.Location = new Point(0, 0);
        _resetButton.Name = "_resetButton";
        _resetButton.Size = new Size(68, 52);
        _resetButton.TabIndex = 9;
        _resetButton.Text = "Reset";
        _resetButton.Click += ResetButton_Click;
        //
        // _statusStrip
        // 
        _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel });
        _statusStrip.Location = new Point(0, 759);
        _statusStrip.Name = "_statusStrip";
        _statusStrip.Size = new Size(1204, 22);
        _statusStrip.TabIndex = 2;
        // 
        // _statusLabel
        // 
        _statusLabel.Name = "_statusLabel";
        _statusLabel.Size = new Size(39, 17);
        _statusLabel.Text = "Ready";
        // 
        // _refreshTimer
        //
        _refreshTimer.Interval = 350;
        _refreshTimer.Tick += RefreshTimer_Tick;
        //
        // _menuStrip
        //
        _menuStrip.Items.AddRange(new ToolStripItem[] { _helpMenuItem });
        _menuStrip.Location = new Point(0, 0);
        _menuStrip.Name = "_menuStrip";
        _menuStrip.Size = new Size(1204, 24);
        _menuStrip.TabIndex = 3;
        //
        // _helpMenuItem
        //
        _helpMenuItem.DropDownItems.AddRange(new ToolStripItem[] { _checkForUpdatesMenuItem, _aboutMenuItem });
        _helpMenuItem.Name = "_helpMenuItem";
        _helpMenuItem.Text = "&Help";
        //
        // _checkForUpdatesMenuItem
        //
        _checkForUpdatesMenuItem.Name = "_checkForUpdatesMenuItem";
        _checkForUpdatesMenuItem.Text = "Check for Updates...";
        _checkForUpdatesMenuItem.Click += CheckForUpdatesMenuItem_Click;
        //
        // _aboutMenuItem
        //
        _aboutMenuItem.Name = "_aboutMenuItem";
        _aboutMenuItem.Text = "About...";
        _aboutMenuItem.Click += AboutMenuItem_Click;
        //
        // MainForm
        //
        ClientSize = new Size(1478, 1144);
        Controls.Add(_splitContainer);
        Controls.Add(_topPanel);
        Controls.Add(_statusStrip);
        Controls.Add(_menuStrip);
        MainMenuStrip = _menuStrip;
        MinimumSize = new Size(980, 640);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "SmartInspect CSV to DXF";
        _menuStrip.ResumeLayout(false);
        _menuStrip.PerformLayout();
        _topPanel.ResumeLayout(false);
        _topPanel.PerformLayout();
        _splitContainer.Panel1.ResumeLayout(false);
        _splitContainer.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_splitContainer).EndInit();
        _splitContainer.ResumeLayout(false);
        _leftPanel.ResumeLayout(false);
        _filesHeaderPanel.ResumeLayout(false);
        _rightPanel.ResumeLayout(false);
        _bottomControlsPanel.ResumeLayout(false);
        _statusStrip.ResumeLayout(false);
        _statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private TableLayoutPanel _topPanel;
    private Label _csvFolderLabel;
    private TextBox _inputFolderTextBox;
    private Button _browseInputButton;
    private Label _dxfFolderLabel;
    private TextBox _outputFolderTextBox;
    private Button _browseOutputButton;
    private Label _usbFolderLabel;
    private TextBox _usbFolderTextBox;
    private Button _browseUsbButton;
    private SplitContainer _splitContainer;
    private Panel _leftPanel;
    private Panel _filesHeaderPanel;
    private Label _filesLabel;
    private Button _refreshButton;
    private ListBox _fileListBox;
    private Panel _rightPanel;
    private PreviewPanel _previewPanel;
    private Panel _bottomControlsPanel;
    private Button _mirrorXButton;
    private Button _mirrorYButton;
    private Button _rotateLeftButton;
    private Button _rotateRightButton;
    private CheckBox _alignModeCheckBox;
    private CheckBox _setOriginCheckBox;
    private Button _resetButton;
    private CheckBox _showTextCheckBox;
    private Button _exportButton;
    private Button _exportUsbButton;
    private StatusStrip _statusStrip;
    private ToolStripStatusLabel _statusLabel;
    private System.Windows.Forms.Timer _refreshTimer;
    private ToolTip _fileListToolTip;
    private MenuStrip _menuStrip;
    private ToolStripMenuItem _helpMenuItem;
    private ToolStripMenuItem _checkForUpdatesMenuItem;
    private ToolStripMenuItem _aboutMenuItem;
}
