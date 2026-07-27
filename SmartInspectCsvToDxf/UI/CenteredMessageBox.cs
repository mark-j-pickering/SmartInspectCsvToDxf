namespace SmartInspectCsvToDxf.UI;

// MessageBox.Show(owner, ...) does not reliably center over its owner in this environment -
// observed landing right next to a completely unrelated window instead of the app, for
// dialogs shown after an awaited operation (during which real OS input focus can drift
// elsewhere - e.g. back to whatever the user was looking at while waiting - and Windows'
// own MessageBox placement appears to follow that rather than the owner's actual bounds).
// This is a drop-in replacement built as a plain Form, so its position is always computed
// directly against the owner's own Bounds instead of relying on that.
internal static class CenteredMessageBox
{
    public static DialogResult Show(Form owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        using var dialog = new MessageDialogForm(text, caption, buttons, icon);
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new Point(
            owner.Location.X + (owner.Width - dialog.Width) / 2,
            owner.Location.Y + (owner.Height - dialog.Height) / 2);
        return dialog.ShowDialog(owner);
    }

    private sealed class MessageDialogForm : Form
    {
        public MessageDialogForm(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            Text = caption;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            MaximumSize = new Size(640, 640);
            Padding = new Padding(20);

            var iconBox = new PictureBox
            {
                Image = GetIcon(icon).ToBitmap(),
                SizeMode = PictureBoxSizeMode.AutoSize,
                Margin = new Padding(0, 0, 16, 0),
            };

            var label = new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(420, 0),
                Margin = new Padding(0, 3, 0, 0),
            };

            var contentPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Dock = DockStyle.Top,
            };
            contentPanel.Controls.Add(iconBox);
            contentPanel.Controls.Add(label);

            var buttonPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                Margin = new Padding(0),
                Padding = new Padding(0, 16, 0, 0),
            };

            void AddButton(string buttonText, DialogResult result, bool isDefault = false, bool isCancel = false)
            {
                var button = new Button { Text = buttonText, DialogResult = result, AutoSize = true, MinimumSize = new Size(90, 28) };
                buttonPanel.Controls.Add(button);
                if (isDefault) AcceptButton = button;
                if (isCancel) CancelButton = button;
            }

            if (buttons == MessageBoxButtons.YesNo)
            {
                AddButton("No", DialogResult.No, isCancel: true);
                AddButton("Yes", DialogResult.Yes, isDefault: true);
            }
            else
            {
                AddButton("OK", DialogResult.OK, isDefault: true, isCancel: true);
            }

            var root = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            root.Controls.Add(contentPanel, 0, 0);
            root.Controls.Add(buttonPanel, 0, 1);

            Controls.Add(root);
        }

        private static Icon GetIcon(MessageBoxIcon icon) => icon switch
        {
            MessageBoxIcon.Error => SystemIcons.Error,
            MessageBoxIcon.Warning => SystemIcons.Warning,
            MessageBoxIcon.Question => SystemIcons.Question,
            MessageBoxIcon.Information => SystemIcons.Information,
            _ => SystemIcons.Information
        };
    }
}
