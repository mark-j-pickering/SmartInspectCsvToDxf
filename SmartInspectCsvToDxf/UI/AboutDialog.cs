namespace SmartInspectCsvToDxf.UI;

public sealed class AboutDialog : Form
{
    public AboutDialog(string versionText)
    {
        Text = "About The VAG Inspector";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(360, 140);
        Padding = new Padding(16);

        var label = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "The VAG Inspector" + Environment.NewLine +
                   $"Version {versionText}" + Environment.NewLine + Environment.NewLine +
                   "Converts FARO CAM2 SmartInspect report exports to DXF.",
        };

        var closeButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Bottom,
        };

        Controls.Add(label);
        Controls.Add(closeButton);
        AcceptButton = closeButton;
        CancelButton = closeButton;
    }
}
